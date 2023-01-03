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

            services.AddScoped<SearchService>();
            services.AddScoped<SCIService>();
            services.AddScoped<TranslateService>();

            services.AddSingleton<RunnersService>();

            if (!Configuration.GetValue("DisableJobs", false))
            {
                services.AddQuartz(q =>
                {
                    q.SchedulerId = "Scheduler-Core";
                    q.UseMicrosoftDependencyInjectionJobFactory();
                    q.UseSimpleTypeLoader();
                    q.UseInMemoryStore();

                    ResourceExtractor.Schedule(q);
                    VideoTextMatcher.Schedule(q);
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

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
