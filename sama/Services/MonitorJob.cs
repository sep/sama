using FluentScheduler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace sama.Services
{
    public class MonitorJob : IJob
    {
        private static readonly ParallelOptions TplOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = (Environment.ProcessorCount < 3 ? Environment.ProcessorCount : 3)
        };

        private readonly IServiceProvider _provider;
        private readonly EndpointProcessService _processService;

        public virtual void InitializeScheduler(IServiceProvider provider)
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var settingsService = provider.GetRequiredService<SettingsService>();

            JobManager.Initialize(new Registry());
            JobManager.JobException += (info) =>
            {
                loggerFactory.CreateLogger(typeof(JobManager)).LogError(0, info.Exception, $"Job '{info.Name}' failed");
            };
            var interval = settingsService.Monitor_IntervalSeconds;
            JobManager.AddJob(this, s => s.WithName("DefaultMonitorJob").NonReentrant().ToRunNow().AndEvery(interval).Seconds());
        }

        public virtual void ReloadSchedule(IServiceProvider provider)
        {
            var settingsService = provider.GetRequiredService<SettingsService>();

            JobManager.Stop();

            JobManager.RemoveJob("DefaultMonitorJob");

            var interval = settingsService.Monitor_IntervalSeconds;
            JobManager.AddJob(this, s => s.WithName("DefaultMonitorJob").NonReentrant().ToRunNow().AndEvery(interval).Seconds());

            JobManager.Start();
        }

        public MonitorJob(IServiceProvider provider, EndpointProcessService processService)
        {
            _provider = provider;
            _processService = processService;
        }

        public virtual void Execute()
        {
            using (var scope = _provider.CreateScope())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
            {
                var endpoints = dbContext.Endpoints.AsQueryable().Where(e => e.Enabled).ToList();
                Parallel.ForEach(endpoints, TplOptions, e => _processService.ProcessEndpoint(e, 0));
            }
        }
    }
}
