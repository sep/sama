using FluentScheduler;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly EndpointCheckService _checkService;

        public MonitorJob(IServiceProvider provider, EndpointCheckService checkService)
        {
            _provider = provider;
            _checkService = checkService;
        }

        public virtual void Execute()
        {
            var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
            using (var scope = scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var endpoints = dbContext.Endpoints.Where(e => e.Enabled).ToList();
                Parallel.ForEach(endpoints, TplOptions, e => _checkService.ProcessEndpoint(e, 0));
            }
        }
    }
}
