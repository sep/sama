using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama.Models;
using sama.Services;
using System;
using System.Collections.Generic;

namespace TestSama.Services
{
    [TestClass]
    public class AggregateNotificationServiceTests
    {
        private INotificationService _notifier1;
        private INotificationService _notifier2;
        private AggregateNotificationService _service;

        [TestInitialize]
        public void Setup()
        {
            _notifier1 = Substitute.For<INotificationService>();
            _notifier2 = Substitute.For<INotificationService>();
            _service = new AggregateNotificationService(new List<INotificationService> { _notifier1, _notifier2 });
        }

        [TestMethod]
        public void ShouldNotifySingleResult()
        {
            var ep = new Endpoint();
            var ecr = new EndpointCheckResult();

            _service.NotifySingleResult(ep, ecr);

            _notifier1.Received().NotifySingleResult(ep, ecr);
            _notifier2.Received().NotifySingleResult(ep, ecr);
        }

        [TestMethod]
        public void ShouldNotifyUp()
        {
            var ep = new Endpoint();
            var dao = DateTimeOffset.Now;

            _service.NotifyUp(ep, dao);

            _notifier1.Received().NotifyUp(ep, dao);
            _notifier2.Received().NotifyUp(ep, dao);
        }

        [TestMethod]
        public void ShouldNotifyDown()
        {
            var ep = new Endpoint();
            var dao = DateTimeOffset.Now;
            var reason = new Exception();

            _service.NotifyDown(ep, dao, reason);

            _notifier1.Received().NotifyDown(ep, dao, reason);
            _notifier2.Received().NotifyDown(ep, dao, reason);
        }

        [TestMethod]
        public void ShouldNotifyMisc()
        {
            var ep = new Endpoint();
            var type = NotificationType.EndpointReconfigured;

            _service.NotifyMisc(ep, type);

            _notifier1.Received().NotifyMisc(ep, type);
            _notifier2.Received().NotifyMisc(ep, type);
        }
    }
}
