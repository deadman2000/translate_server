using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using TranslateServer.Hosted;
using TranslateServer.Mongo;
using TranslateServer.Services;

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
                });

            services.Configure<ServerConfig>(Configuration.GetSection("Server"));

            services.AddScoped<MongoService>();
            services.AddScoped<UsersService>();
            services.AddScoped<ProjectsService>();
            services.AddScoped<VolumesService>();
            services.AddScoped<TextsService>();
            services.AddScoped<TranslateService>();
            services.AddScoped<SearchService>();
            services.AddScoped<SCIService>();

            services.AddHostedService<ResourceExtractor>();

            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

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
