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
using ModelContextProtocol.AspNetCore;
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

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
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

            services.Configure<ServerConfig>(Configuration.GetSection("Server"));
            services.Configure<TranslateServer.Mcp.McpOptions>(Configuration.GetSection("Mcp"));

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

            // MCP
            services.AddScoped<McpAuthorizationFilter>();
            services.AddScoped<McpAgentService>();
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

            // Protect the real MCP endpoint (/mcp) with the same token as the REST API.
            // Clients must send the token via header "X-MCP-Token" or "Authorization: Bearer ..."
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/mcp"))
                {
                    var mcpOptions = context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<TranslateServer.Mcp.McpOptions>>().Value;

                    if (!mcpOptions.Enabled)
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        await context.Response.WriteAsJsonAsync(new { error = "MCP is disabled" });
                        return;
                    }

                    string provided = null;
                    if (context.Request.Headers.TryGetValue("X-MCP-Token", out var h1))
                        provided = h1;
                    else if (context.Request.Headers.TryGetValue("Authorization", out var h2))
                    {
                        var val = h2.ToString();
                        if (val.StartsWith("Bearer ", System.StringComparison.OrdinalIgnoreCase))
                            provided = val.Substring(7).Trim();
                        else if (val.StartsWith("Token ", System.StringComparison.OrdinalIgnoreCase))
                            provided = val.Substring(6).Trim();
                    }

                    if (string.IsNullOrEmpty(provided) || provided != mcpOptions.Token)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(new { error = "Invalid or missing MCP token" });
                        return;
                    }
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
