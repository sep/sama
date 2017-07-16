using FluentScheduler;
using Microsoft.EntityFrameworkCore;
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

        private readonly DbContextOptions<ApplicationDbContext> _dbContextOptions;
        private readonly EndpointCheckService _checkService;

        public MonitorJob(DbContextOptions<ApplicationDbContext> db, EndpointCheckService checkService)
        {
            _dbContextOptions = db;
            _checkService = checkService;
        }

        public virtual void Execute()
        {
            using (var dbContext = new ApplicationDbContext(_dbContextOptions))
            {
                var endpoints = dbContext.Endpoints.Where(e => e.Enabled).ToList();
                Parallel.ForEach(endpoints, TplOptions, e => _checkService.ProcessEndpoint(e, 0));
            }
        }
    }
}
