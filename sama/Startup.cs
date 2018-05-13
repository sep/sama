using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using sama.Services;
using sama.Models;
using Microsoft.AspNetCore.Identity;
using System;

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
            services.AddSingleton<EndpointProcessService>();
            services.AddSingleton<MonitorJob>();
            services.AddSingleton<SettingsService>();
            services.AddSingleton<PingWrapper>();
            services.AddSingleton<TcpClientWrapper>();
            services.AddSingleton<CertificateValidationService>();

            services.AddSingleton<ICheckService, HttpCheckService>();
            services.AddSingleton<ICheckService, IcmpCheckService>();

            services.AddSingleton<INotificationService, SlackNotificationService>();
            services.AddSingleton<INotificationService, GraphiteNotificationService>();
            services.AddSingleton<AggregateNotificationService>();

            services.AddTransient<System.Net.Http.HttpClientHandler>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
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

            app.UseSession(new SessionOptions { IdleTimeout = TimeSpan.FromMinutes(30) });

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
        }
    }
}
