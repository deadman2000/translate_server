using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Quartz;
using System.IO;
using System.Threading.Tasks;
using TranslateServer.Jobs;
using TranslateServer.Mongo;
using TranslateServer.Services;
using TranslateServer.Store;
using TranslateServer.Mcp;

namespace TranslateServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy(name: "debug",
                                  builder =>
                                  {
                                      builder.WithOrigins("http://localhost:3000");
                                  });
            });

            var jwtOptions = Configuration.GetSection("Mcp:Jwt").Get<McpJwtOptions>();

            var authBuilder = services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(o => {
                    o.Events.OnRedirectToLogin = c =>
                    {
                        c.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    };
                    o.Events.OnRedirectToAccessDenied = c =>
                    {
                        c.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    };
                });

            // Add JWT Bearer for MCP OAuth clients (Grok, etc.)
            if (jwtOptions?.Enabled == true)
            {
                authBuilder.AddJwtBearer("McpJwt", options =>
                {
                    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                    {
                        ValidateIssuer = jwtOptions.ValidateIssuer,
                        ValidateAudience = jwtOptions.ValidateAudience,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidAudience = jwtOptions.Audience,
                    };

                    if (!string.IsNullOrEmpty(jwtOptions.Authority))
                    {
                        options.Authority = jwtOptions.Authority;
                    }

                    if (!string.IsNullOrEmpty(jwtOptions.SigningKey))
                    {
                        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                            System.Text.Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
                        options.TokenValidationParameters.IssuerSigningKey = key;
                    }
                });
            }

            services.Configure<ServerConfig>(Configuration.GetSection("Server"));
            services.Configure<McpOptions>(Configuration.GetSection("Mcp"));

            services.AddScoped<MongoService>();
            services.AddScoped<UsersStore>();
            services.AddScoped<ProjectsStore>();
            services.AddScoped<VolumesStore>();
            services.AddScoped<TextsStore>();
            services.AddScoped<TranslateStore>();
            services.AddScoped<CommentsStore>();
            services.AddScoped<PatchesStore>();
            services.AddScoped<InvitesStore>();
            services.AddScoped<VideoStore>();
            services.AddScoped<VideoTasksStore>();
            services.AddScoped<VideoTextStore>();
            services.AddScoped<VideoReferenceStore>();
            services.AddScoped<CommentNotifyStore>();
            services.AddScoped<WordsStore>();
            services.AddScoped<SuffixesStore>();
            services.AddScoped<SaidStore>();
            services.AddScoped<SynonymStore>();

            services.AddScoped<SearchService>();
            services.AddScoped<SCIService>();
            services.AddScoped<TranslateService>();
            services.AddScoped<YandexSpellcheck>();
            services.AddScoped<YandexTranslateService>();

            services.AddSingleton<RunnersService>();
            services.AddSingleton<ResCache>();
            services.AddSingleton<SpellcheckCache>();

            // MCP / AI agent support (isolated from main cookie auth)
            services.AddScoped<McpAuthorizationFilter>();
            services.AddScoped<McpAgentContext>();
            services.AddScoped<McpAgentService>();
            services.AddSingleton<OAuthServerService>();

            // Real MCP server (official SDK) - exposed at /mcp
            services
                .AddMcpServer()
                .WithHttpTransport()
                .WithTools<TranslateMcpTools>();

            if (!Configuration.GetValue("DisableJobs", false))
            {
                services.AddQuartz(q =>
                {
                    q.SchedulerId = "Scheduler-Core";
                    q.UseMicrosoftDependencyInjectionJobFactory();
                    q.UseSimpleTypeLoader();
                    q.UseInMemoryStore();
                    q.UseDedicatedThreadPool(tp =>
                    {
                        tp.MaxConcurrency = 1;
                    });

                    InitJob.Schedule(q);
                    ResourceExtractorJob.Schedule(q);
                    VideoTextMatcherJob.Schedule(q);
                });

                services.AddQuartzHostedService();
            }

            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            Directory.CreateDirectory("resources");
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(env.ContentRootPath, "resources")),
                RequestPath = "/api/resources"
            });

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            // Protect the real MCP endpoint (/mcp) and set current agent context.
            // Supports two authentication methods:
            // 1. Static tokens (X-MCP-Token or simple Bearer) - from Mcp:Agents[]
            // 2. JWT Bearer tokens - for OAuth clients like Grok (when Mcp:Jwt.Enabled = true)
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/mcp"))
                {
                    var mcpOptions = context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<TranslateServer.Mcp.McpOptions>>().Value;
                    var agentContext = context.RequestServices.GetRequiredService<TranslateServer.Mcp.McpAgentContext>();

                    if (!mcpOptions.Enabled)
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        await context.Response.WriteAsJsonAsync(new { error = "MCP is disabled" });
                        return;
                    }

                    McpAgent agent = null;

                    // === Method 1: Static token (existing behavior) ===
                    string providedToken = null;
                    if (context.Request.Headers.TryGetValue("X-MCP-Token", out var h1))
                        providedToken = h1;
                    else if (context.Request.Headers.TryGetValue("Authorization", out var h2))
                    {
                        var val = h2.ToString();
                        if (val.StartsWith("Bearer ", System.StringComparison.OrdinalIgnoreCase))
                        {
                            var bearerValue = val.Substring(7).Trim();
                            // Only treat as static token if it's not a JWT (no dots)
                            if (!bearerValue.Contains('.'))
                                providedToken = bearerValue;
                        }
                        else if (val.StartsWith("Token ", System.StringComparison.OrdinalIgnoreCase))
                        {
                            providedToken = val.Substring(6).Trim();
                        }
                    }

                    if (!string.IsNullOrEmpty(providedToken))
                    {
                        agent = mcpOptions.GetAgentByToken(providedToken);
                    }

                    // === Method 2: JWT Bearer (OAuth / Grok support) ===
                    if (agent == null && mcpOptions.Jwt?.Enabled == true)
                    {
                        var authService = context.RequestServices.GetRequiredService<Microsoft.AspNetCore.Authentication.IAuthenticationService>();
                        var authResult = await authService.AuthenticateAsync(context, "McpJwt");

                        if (authResult?.Succeeded == true && authResult.Principal != null)
                        {
                            var claims = authResult.Principal;

                            // Try to extract a nice agent name from claims
                            string agentName = claims.FindFirst(mcpOptions.Jwt.AgentNameClaim)?.Value
                                            ?? claims.FindFirst(mcpOptions.Jwt.FallbackAgentNameClaim)?.Value
                                            ?? claims.FindFirst("client_id")?.Value
                                            ?? "OAuth-Agent";

                            agent = new McpAgent
                            {
                                Token = "jwt-oauth",
                                AgentName = agentName,
                                Description = "Authenticated via OAuth/JWT",
                                Enabled = true,
                                ReadOnly = false // Can be extended later based on scopes
                            };
                        }
                    }

                    if (agent == null)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                        var wwwAuthenticate = "Bearer realm=\"MCP\", error=\"invalid_token\"";
                        if (mcpOptions.Jwt?.Enabled == true)
                        {
                            wwwAuthenticate += $", resource_metadata=\"{context.Request.Scheme}://{context.Request.Host}/api/.well-known/oauth-protected-resource\"";
                        }

                        context.Response.Headers["WWW-Authenticate"] = wwwAuthenticate;

                        await context.Response.WriteAsJsonAsync(new
                        {
                            error = "Invalid or missing MCP authentication (token or JWT)",
                            resource_metadata = mcpOptions.Jwt?.Enabled == true
                                ? $"{context.Request.Scheme}://{context.Request.Host}/api/.well-known/oauth-protected-resource"
                                : (string)null
                        });
                        return;
                    }

                    // Set the authenticated agent for this request
                    agentContext.SetCurrentAgent(agent);
                }

                await next();
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();

                // Real MCP protocol endpoint (for LM Studio, Claude Desktop, Cursor, etc.)
                // Clients must authenticate using the MCP token (same as /api/mcp).
                endpoints.MapMcp("/mcp");
            });
        }
    }
}
