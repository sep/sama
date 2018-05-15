using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama;
using sama.Models;
using sama.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestSama.Services
{
    [TestClass]
    public class StateServiceTests
    {
        private IServiceProvider _provider;
        private AggregateNotificationService _notifier;
        private StateService _service;

        private Endpoint _ep1 = new Endpoint { Id = 100, Name = "A", Kind = Endpoint.EndpointKind.Http, JsonConfig = "{}" };
        private Endpoint _ep2 = new Endpoint { Id = 200, Name = "B", Kind = Endpoint.EndpointKind.Http, JsonConfig = "{}" };
        private Endpoint _ep3 = new Endpoint { Id = 300, Name = "C", Kind = Endpoint.EndpointKind.Http, JsonConfig = "{}" };

        [TestInitialize]
        public void Setup()
        {
            _provider = TestUtility.InitDI();
            _notifier = Substitute.For<AggregateNotificationService>(new List<INotificationService>(), null);
            _service = new StateService(_provider, _notifier);

            using (var scope = _provider.CreateScope())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
            {
                dbContext.Endpoints.AddRange(_ep1, _ep2, _ep3);
                dbContext.SaveChanges();
            }

            _service.AddEndpointCheckResult(_ep2.Id, new EndpointCheckResult { Start = DateTimeOffset.UtcNow, Stop = DateTimeOffset.UtcNow, Success = true }, true);
            _service.AddEndpointCheckResult(_ep3.Id, new EndpointCheckResult { Start = DateTimeOffset.UtcNow, Stop = DateTimeOffset.UtcNow, Success = false, Error = new Exception("ERROR") }, true);

            _notifier.ClearReceivedCalls();
        }

        [TestMethod]
        public void InProgressUpdateShouldNotChangeReportedState()
        {
            AssertStateEquality(true, null, _service.GetStatus(_ep2.Id));

            var result = new EndpointCheckResult { Start = DateTimeOffset.UtcNow, Stop = DateTimeOffset.UtcNow, Success = false, Error = new Exception("ERR") };
            _service.AddEndpointCheckResult(_ep2.Id, result, false);

            Assert.IsTrue(_service.GetStatus(_ep2.Id).IsInProgress);
            AssertStateEquality(true, null, _service.GetStatus(_ep2.Id));
            AssertResultNotification(_ep2.Id, result);
            AssertNoUpNotifications();
            AssertNoDownNotifications();
        }

        [TestMethod]
        public void ShouldSetEndpointCheckInProgress()
        {
            Assert.IsFalse(_service.GetStatus(_ep2.Id).IsInProgress);

            _service.SetEndpointCheckInProgress(_ep2.Id);

            Assert.IsTrue(_service.GetStatus(_ep2.Id).IsInProgress);

            AssertNoResultNotifications();
            AssertNoUpNotifications();
            AssertNoDownNotifications();
        }

        [TestMethod]
        public void ShouldRetrieveEndpointState()
        {
            AssertStateEquality(null, null, _service.GetStatus(_ep1.Id));
            AssertStateEquality(true, null, _service.GetStatus(_ep2.Id));
            AssertStateEquality(false, "ERROR", _service.GetStatus(_ep3.Id));

            var allStates = GetAllNonNull();
            Assert.AreEqual(2, allStates.Count);
            AssertStateEquality(true, null, allStates.Single(kvp => kvp.Key.Id == _ep2.Id).Value);
            AssertStateEquality(false, "ERROR", allStates.Single(kvp => kvp.Key.Id == _ep3.Id).Value);
        }

        [TestMethod]
        public void ShouldNotifyWhenEndpointGoesUpFromDownStateOnly()
        {
            var result = new EndpointCheckResult { Start = DateTimeOffset.UtcNow, Stop = DateTimeOffset.UtcNow, Success = true };

            _service.AddEndpointCheckResult(_ep2.Id, result, true);
            AssertResultNotification(_ep2.Id, result);
            AssertNoUpNotifications();
            AssertNoDownNotifications();
            
            _service.AddEndpointCheckResult(_ep3.Id, result, true);
            AssertResultNotification(_ep3.Id, result);
            AssertUpNotification(_ep3.Id);
            AssertNoDownNotifications();
        }

        [TestMethod]
        public void ShouldNotifyWhenEndpointGoesDownFromUpOrUnknownStates()
        {
            var result = new EndpointCheckResult { Start = DateTimeOffset.UtcNow, Stop = DateTimeOffset.UtcNow, Success = false, Error = new Exception("ERROR") };

            _service.AddEndpointCheckResult(_ep1.Id, result, true);
            AssertResultNotification(_ep1.Id, result);
            AssertDownNotification(_ep1.Id, "ERROR");
            AssertNoUpNotifications();

            _notifier.ClearReceivedCalls();

            _service.AddEndpointCheckResult(_ep2.Id, result, true);
            AssertResultNotification(_ep2.Id, result);
            AssertDownNotification(_ep2.Id, "ERROR");
            AssertNoUpNotifications();

            _notifier.ClearReceivedCalls();

            _service.AddEndpointCheckResult(_ep3.Id, result, true);
            AssertResultNotification(_ep3.Id, result);
            AssertNoDownNotifications();
            AssertNoUpNotifications();
        }

        [TestMethod]
        public void ShouldNotifyWhenEndpointIsStillDownButWithDifferentErrorMessage()
        {
            var result = new EndpointCheckResult { Start = DateTimeOffset.UtcNow, Stop = DateTimeOffset.UtcNow, Success = false, Error = new Exception("err1") };

            _service.AddEndpointCheckResult(_ep3.Id, result, true);

            AssertResultNotification(_ep3.Id, result);
            AssertNoUpNotifications();
            AssertDownNotification(_ep3.Id, "err1");
        }

        [TestMethod]
        public void ShouldRemoveEndpointState()
        {
            _service.RemoveStatus(_ep2.Id);

            Assert.IsNull(_service.GetStatus(_ep1.Id));
            Assert.IsNull(_service.GetStatus(_ep2.Id));
            Assert.IsNotNull(_service.GetStatus(_ep3.Id));
            Assert.AreEqual(1, GetAllNonNull().Count);

            _service.RemoveStatus(_ep2.Id);

            Assert.AreEqual(1, GetAllNonNull().Count);

            _service.RemoveStatus(_ep1.Id);

            Assert.AreEqual(1, GetAllNonNull().Count);

            _service.RemoveStatus(_ep3.Id);

            Assert.AreEqual(0, GetAllNonNull().Count);
        }

        private void AssertStateEquality(bool? expectedIsUp, string expectedMessage, EndpointStatus actualStatus)
        {
            Assert.AreEqual(expectedIsUp, actualStatus?.IsUp);
            Assert.AreEqual(expectedMessage, actualStatus?.Error?.Message);
        }

        private IReadOnlyDictionary<Endpoint, EndpointStatus> GetAllNonNull()
        {
            return _service.GetAll().Where(s => s.Value != null).ToDictionary(s => s.Key, s => s.Value);
        }

        private void AssertResultNotification(int endpointId, EndpointCheckResult result)
        {
            _notifier.Received(1).NotifySingleResult(Arg.Is<Endpoint>(ep => ep.Id == endpointId), Arg.Any<EndpointCheckResult>());
        }

        private void AssertNoResultNotifications()
        {
            _notifier.DidNotReceiveWithAnyArgs().NotifySingleResult(null, null);
        }

        private void AssertUpNotification(int endpointId)
        {
            _notifier.Received(1).NotifyUp(Arg.Is<Endpoint>(ep => ep.Id == endpointId), Arg.Any<DateTimeOffset?>());
        }

        private void AssertNoUpNotifications()
        {
            _notifier.DidNotReceiveWithAnyArgs().NotifyUp(null, null);
        }

        private void AssertDownNotification(int endpointId, string errorMessage)
        {
            _notifier.Received(1).NotifyDown(Arg.Is<Endpoint>(ep => ep.Id == endpointId), Arg.Any<DateTimeOffset>(), Arg.Is<Exception>(ex => ex.Message == errorMessage));
        }

        private void AssertNoDownNotifications()
        {
            _notifier.DidNotReceiveWithAnyArgs().NotifyDown(null, DateTimeOffset.MinValue, null);
        }
    }
}
