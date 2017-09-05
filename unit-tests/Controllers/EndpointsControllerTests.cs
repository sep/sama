using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama;
using sama.Controllers;
using sama.Extensions;
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
        private IServiceScope _scope;
        private ApplicationDbContext _testDbContext;
        private StateService _stateService;
        private UserManagementService _userService;

        [TestInitialize]
        public void Setup()
        {
            _provider = TestUtility.InitDI();
            _scope = _provider.CreateScope();
            _testDbContext = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            _stateService = Substitute.For<StateService>();
            _userService = Substitute.For<UserManagementService>(null, null);
            _controller = new EndpointsController(_scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), _stateService, _userService);

            SeedHttpEndpoints();
        }

        [TestMethod]
        public async Task IndexShouldDisplayAllEndpoints()
        {
            var states = new ReadOnlyDictionary<Endpoint, StateService.EndpointState>(new Dictionary<Endpoint, StateService.EndpointState>());
            _stateService.GetAllStates().Returns(states);

            var result = await _controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreSame(states, result.ViewData["CurrentStates"]);
            Assert.AreEqual(3, (result.Model as IEnumerable<EndpointViewModel>).Count());
        }

        [TestMethod]
        public async Task ListShouldDisplayAllEndpoints()
        {
            var states = new ReadOnlyDictionary<Endpoint, StateService.EndpointState>(new Dictionary<Endpoint, StateService.EndpointState>());
            _stateService.GetAllStates().Returns(states);

            var result = await _controller.List() as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreSame(states, result.ViewData["CurrentStates"]);
            Assert.AreEqual(3, (result.Model as IEnumerable<EndpointViewModel>).Count());
        }

        [TestMethod]
        public async Task DetailsShouldDisplayEndpointInfo()
        {
            var state = new StateService.EndpointState();
            _stateService.GetState(2).Returns(state);

            var result = await _controller.Details(2) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreSame(state, result.ViewData["State"]);
            Assert.AreEqual("C", (result.Model as EndpointViewModel).Name);
        }

        [TestMethod]
        public async Task DetailsShouldReturn404WhenNoValidIdSpecified()
        {
            Assert.IsNotNull(await _controller.Details(90) as NotFoundResult);
            Assert.IsNotNull(await _controller.Details(null) as NotFoundResult);
        }

        [TestMethod]
        public async Task ShouldCreateHttpEndpointWhenModelIsValid()
        {
            var result = await _controller.Create(new HttpEndpointViewModel { Name = "Q", Kind = Endpoint.EndpointKind.Http, Location = "W" }) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("List", result.ActionName);
            Assert.AreEqual(1, _testDbContext.Endpoints.Where(e => e.Name == "Q" && e.Kind == Endpoint.EndpointKind.Http).Count());
        }

        [TestMethod]
        public async Task ShouldCreateIcmpEndpointWhenModelIsValid()
        {
            var result = await _controller.Create(new IcmpEndpointViewModel { Name = "Q", Kind = Endpoint.EndpointKind.Icmp, Address = "W" }) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("List", result.ActionName);
            Assert.AreEqual(1, _testDbContext.Endpoints.Where(e => e.Name == "Q" && e.Kind == Endpoint.EndpointKind.Icmp).Count());
        }

        [TestMethod]
        public async Task ShouldNotCreateHttpEndpointWhenModelIsNotValid()
        {
            _controller.ModelState.AddModelError("Location", "Location is required");

            var result = await _controller.Create(new HttpEndpointViewModel { Name = "Q", Kind = Endpoint.EndpointKind.Http }) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(0, _testDbContext.Endpoints.Where(e => e.Name == "Q").Count());
        }

        [TestMethod]
        public async Task ShouldNotCreateIcmpEndpointWhenModelIsNotValid()
        {
            _controller.ModelState.AddModelError("Location", "Location is required");

            var result = await _controller.Create(new IcmpEndpointViewModel { Name = "Q", Kind = Endpoint.EndpointKind.Icmp }) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(0, _testDbContext.Endpoints.Where(e => e.Name == "Q").Count());
        }

        [TestMethod]
        public async Task EditShouldUpdateHttpEndpointAndResetStateWhenModelIsValid()
        {
            var endpoint = _testDbContext.Endpoints.Where(e => e.Name == "A").Single();
            endpoint.Name = "W";
            _testDbContext.Entry(endpoint).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

            var result = await _controller.Edit(1, (HttpEndpointViewModel)endpoint.ToEndpointViewModel()) as RedirectToActionResult;
            Assert.IsNotNull(result);
            Assert.AreEqual("List", result.ActionName);
            Assert.AreEqual(1, _testDbContext.Endpoints.Where(e => e.Name == "W").Count());
            Assert.AreEqual(0, _testDbContext.Endpoints.Where(e => e.Name == "A").Count());
            _stateService.Received().SetState(Arg.Is<Endpoint>(e => e.Id == endpoint.Id), null, null);
        }

        [TestMethod]
        public async Task EditShouldUpdateIcmpEndpointAndResetStateWhenModelIsValid()
        {
            var endpoint = _testDbContext.Endpoints.Where(e => e.Name == "E").Single();
            endpoint.Name = "W";
            _testDbContext.Entry(endpoint).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

            var result = await _controller.Edit(3, (IcmpEndpointViewModel)endpoint.ToEndpointViewModel()) as RedirectToActionResult;
            Assert.IsNotNull(result);
            Assert.AreEqual("List", result.ActionName);
            Assert.AreEqual(1, _testDbContext.Endpoints.Where(e => e.Name == "W").Count());
            Assert.AreEqual(0, _testDbContext.Endpoints.Where(e => e.Name == "E").Count());
            _stateService.Received().SetState(Arg.Is<Endpoint>(e => e.Id == endpoint.Id), null, null);
        }

        [TestMethod]
        public async Task ShouldDeleteEndpoint()
        {
            Assert.AreEqual(1, _testDbContext.Endpoints.Where(e => e.Name == "A").Count());

            var result = await _controller.DeleteConfirmed(1) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("List", result.ActionName);
            Assert.AreEqual(0, _testDbContext.Endpoints.Where(e => e.Name == "A").Count());
            _stateService.Received().RemoveState(1);
        }

        private void SeedHttpEndpoints()
        {
            _testDbContext.Endpoints.Add(TestUtility.CreateHttpEndpoint("A", false, 1, "B"));
            _testDbContext.Endpoints.Add(TestUtility.CreateHttpEndpoint("C", true, 2, "D"));
            _testDbContext.Endpoints.Add(TestUtility.CreateIcmpEndpoint("E", true, 3, "F"));
            _testDbContext.SaveChanges();
        }
    }
}
