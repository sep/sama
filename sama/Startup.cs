using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using sama.Models;
using sama.Services;
using System;

namespace sama
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env)
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
            services.AddLogging(builder =>
            {
                builder.AddConfiguration(Configuration.GetSection("Logging"));
                builder.AddConsole();
                builder.AddDebug();
            });

            services.AddControllersWithViews()
                .AddRazorRuntimeCompilation();

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
            services.AddSingleton<EndpointProcessService>();
            services.AddSingleton<MonitorJob>();
            services.AddSingleton<SettingsService>();
            services.AddSingleton<PingWrapper>();
            services.AddSingleton<TcpClientWrapper>();
            services.AddSingleton<SqlConnectionWrapper>();
            services.AddSingleton<CertificateValidationService>();
            services.AddSingleton<BackgroundExecutionWrapper>();

            services.AddSingleton<ICheckService, HttpCheckService>();
            services.AddSingleton<ICheckService, IcmpCheckService>();

            services.AddSingleton<INotificationService, SlackNotificationService>();
            services.AddSingleton<INotificationService, GraphiteNotificationService>();
            services.AddSingleton<INotificationService, SqlServerNotificationService>();
            services.AddSingleton<AggregateNotificationService>();

            services.AddTransient<System.Net.Http.HttpClientHandler>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseSession(new SessionOptions { IdleTimeout = TimeSpan.FromMinutes(30) });

            app.UseAuthentication();

            app.UseAuthorization();

            var behindReverseProxy = Configuration.GetSection("SAMA").GetValue<bool>("BehindReverseProxy");
            if (behindReverseProxy)
            {
                app.UseForwardedHeaders(new ForwardedHeadersOptions
                {
                    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
                });
            }

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute("default", "{controller=Endpoints}/{action=IndexRedirect}/{id?}");
            });
        }
    }
}
