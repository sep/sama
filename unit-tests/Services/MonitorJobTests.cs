using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama;
using sama.Models;
using sama.Services;
using System;

namespace TestSama.Services
{
    [TestClass]
    public class MonitorJobTests
    {
        private MonitorJob _service;
        private IServiceProvider _provider;
        private IServiceScope _scope;
        private ApplicationDbContext _testDbContext;
        private EndpointCheckService _checkService;

        [TestInitialize]
        public void Setup()
        {
            _provider = TestUtility.InitDI();
            _scope = _provider.CreateScope();
            _testDbContext = new ApplicationDbContext(_scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>());
            _checkService = Substitute.For<EndpointCheckService>(null, null, null, null);
            _service = new MonitorJob(_provider, _checkService);

            SeedEndpoints();
        }

        [TestMethod]
        public void ExecuteShouldProcessEachEnabledEndpoint()
        {
            _service.Execute();

            _checkService.Received(2).ProcessEndpoint(Arg.Any<Endpoint>(), Arg.Any<int>());
            _checkService.Received().ProcessEndpoint(Arg.Is<Endpoint>(e => e.Id == 2), 0);
            _checkService.Received().ProcessEndpoint(Arg.Is<Endpoint>(e => e.Id == 3), 0);
        }

        private void SeedEndpoints()
        {
            _testDbContext.Endpoints.Add(new Endpoint { Id = 1, Name = "A", Location = "B", Enabled = false });
            _testDbContext.Endpoints.Add(new Endpoint { Id = 2, Name = "C", Location = "D", Enabled = true });
            _testDbContext.Endpoints.Add(new Endpoint { Id = 3, Name = "D", Location = "E", Enabled = true });
            _testDbContext.Endpoints.Add(new Endpoint { Id = 4, Name = "F", Location = "G", Enabled = false });
            _testDbContext.SaveChanges();
        }
    }
}
