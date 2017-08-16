using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using sama.Services;
using sama.Models;
using Microsoft.AspNetCore.Identity;

namespace sama
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();

            // Adds a default in-memory implementation of IDistributedCache.
            services.AddDistributedMemoryCache();
            services.AddSession();

            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(Configuration.GetConnectionString("DefaultConnection")));

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddDefaultTokenProviders();
            services.AddTransient<IUserStore<ApplicationUser>, UserManagementService>();
            services.AddTransient<IRoleStore<IdentityRole>, UserManagementService>();
            services.AddTransient<UserManagementService>();
            services.AddTransient<LdapService>();
            services.AddTransient<LdapAuthWrapper>();

            services.AddSingleton(Configuration);

            services.AddSingleton<StateService>();
            services.AddSingleton<SlackNotificationService>();
            services.AddSingleton<EndpointCheckService>();
            services.AddSingleton<MonitorJob>();

            services.AddTransient(provider => new System.Net.Http.HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    // Ignore SSL/TLS errors (for now)
                    return true;
                }
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, DbContextOptions<ApplicationDbContext> dbContextOptions, MonitorJob monitorJob)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseSession();

            app.UseAuthentication();

            var behindReverseProxy = Configuration.GetSection("SAMA").GetValue<bool>("BehindReverseProxy");
            if (behindReverseProxy)
            {
                app.UseForwardedHeaders(new ForwardedHeadersOptions
                {
                    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
                });
            }

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Endpoints}/{action=IndexRedirect}/{id?}");
            });

            using (var dbContext = new ApplicationDbContext(dbContextOptions))
            {
                dbContext.Database.Migrate();
            }

            FluentScheduler.JobManager.Initialize(new FluentScheduler.Registry());
            FluentScheduler.JobManager.JobException += (info) =>
            {
                loggerFactory.CreateLogger(typeof(FluentScheduler.JobManager)).LogError(0, info.Exception, $"Job '{info.Name}' failed");
            };
            var interval = Configuration.GetSection("SAMA").GetValue<int>("MonitorIntervalSeconds");
            FluentScheduler.JobManager.AddJob(monitorJob, s => s.NonReentrant().ToRunNow().AndEvery(interval).Seconds());
        }
    }
}
