using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama;
using sama.Controllers;
using sama.Models;
using sama.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace TestSama.Controllers
{
    [TestClass]
    public class EndpointsControllerTests
    {
        private EndpointsController _controller;
        private IServiceProvider _provider;
        private ApplicationDbContext _testDbContext;
        private StateService _stateService;

        [TestInitialize]
        public void Setup()
        {
            _provider = TestUtility.InitDI();
            _testDbContext = new ApplicationDbContext(_provider.GetRequiredService<DbContextOptions<ApplicationDbContext>>());
            _stateService = Substitute.For<StateService>();
            _controller = new EndpointsController(_provider.GetRequiredService<ApplicationDbContext>(), _stateService);

            SeedEndpoints();
        }

        [TestMethod]
        public async Task IndexShouldDisplayAllEndpoints()
        {
            var states = new ReadOnlyDictionary<Endpoint, StateService.EndpointState>(new Dictionary<Endpoint, StateService.EndpointState>());
            _stateService.GetAllStates().Returns(states);

            var result = await _controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreSame(states, result.ViewData["CurrentStates"]);
            Assert.AreEqual(2, (result.Model as List<Endpoint>).Count);
        }

        [TestMethod]
        public async Task DetailsShouldDisplayEndpointInfo()
        {
            var state = new StateService.EndpointState();
            _stateService.GetState(2).Returns(state);

            var result = await _controller.Details(2) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreSame(state, result.ViewData["State"]);
            Assert.AreEqual("C", (result.Model as Endpoint).Name);
        }

        [TestMethod]
        public async Task DetailsShouldReturn404WhenNoValidIdSpecified()
        {
            Assert.IsNotNull(await _controller.Details(3) as NotFoundResult);
            Assert.IsNotNull(await _controller.Details(null) as NotFoundResult);
        }

        [TestMethod]
        public async Task ShouldCreateEndpointWhenModelIsValid()
        {
            var result = await _controller.Create(new Endpoint { Name = "Q", Location = "W" }) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.AreEqual(1, _testDbContext.Endpoints.Where(e => e.Name == "Q").Count());
        }

        [TestMethod]
        public async Task ShouldNotCreateEndpointWhenModelIsNotValid()
        {
            _controller.ModelState.AddModelError("Location", "Location is required");

            var result = await _controller.Create(new Endpoint { Name = "Q" }) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(0, _testDbContext.Endpoints.Where(e => e.Name == "Q").Count());
        }

        [TestMethod]
        public async Task EditShouldUpdateEndpointAndResetStateWhenModelIsValid()
        {
            var endpoint = new Endpoint { Id = 1, Name = "W", Location = "E" };

            Assert.AreEqual(1, _testDbContext.Endpoints.Where(e => e.Name == "A").Count());

            var result = await _controller.Edit(1, endpoint) as RedirectToActionResult;
            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.AreEqual(1, _testDbContext.Endpoints.Where(e => e.Name == "W").Count());
            Assert.AreEqual(0, _testDbContext.Endpoints.Where(e => e.Name == "A").Count());
            _stateService.Received().SetState(endpoint, null, null);
        }

        [TestMethod]
        public async Task ShouldDeleteEndpoint()
        {
            Assert.AreEqual(1, _testDbContext.Endpoints.Where(e => e.Name == "A").Count());

            var result = await _controller.DeleteConfirmed(1) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.AreEqual(0, _testDbContext.Endpoints.Where(e => e.Name == "A").Count());
            _stateService.Received().RemoveState(1);
        }

        private void SeedEndpoints()
        {
            _testDbContext.Endpoints.Add(new Endpoint { Id = 1, Name = "A", Location = "B", Enabled = false });
            _testDbContext.Endpoints.Add(new Endpoint { Id = 2, Name = "C", Location = "D", Enabled = true });
            _testDbContext.SaveChanges();
        }
    }
}
