using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama.Models;
using sama.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace TestSama.Services
{
    [TestClass]
    public class IcmpCheckServiceTests
    {
        private PingWrapper _pingWrapper;
        private IcmpCheckService _service;

        [TestInitialize]
        public void Setup()
        {
            _pingWrapper = Substitute.For<PingWrapper>();
            _service = new IcmpCheckService(_pingWrapper);
        }

        [TestMethod]
        public void ShouldOnlyHandleIcmpEndpoints()
        {
            Assert.IsTrue(_service.CanHandle(new Endpoint { Kind = Endpoint.EndpointKind.Icmp }));
            Assert.IsFalse(_service.CanHandle(new Endpoint { Kind = Endpoint.EndpointKind.Http }));
        }

        [TestMethod]
        public void ShouldReturnSuccessWhenPingSucceeds()
        {
            _pingWrapper.SendPing("fdsa").Returns(System.Net.NetworkInformation.IPStatus.Success);

            var result = _service.Check(TestUtility.CreateIcmpEndpoint("A", icmpAddress: "fdsa"), out string msg);
            Assert.IsTrue(result);
            Assert.IsNull(msg);
        }

        [TestMethod]
        public void ShouldReturnFailureWithCorrectMessagesWhenPingFails()
        {
            _pingWrapper.SendPing("fdsa").Returns(System.Net.NetworkInformation.IPStatus.TimedOut);
            _pingWrapper.SendPing("asdf").Returns(System.Net.NetworkInformation.IPStatus.DestinationHostUnreachable);
            _pingWrapper.When(call => call.SendPing("asdffdsa"))
                .Throw(new Exception("OH NO"));

            var result1 = _service.Check(TestUtility.CreateIcmpEndpoint("A", icmpAddress: "fdsa"), out string msg1);
            Assert.IsFalse(result1);
            Assert.AreEqual("The ping request timed out.", msg1);

            var result2 = _service.Check(TestUtility.CreateIcmpEndpoint("A", icmpAddress: "asdf"), out string msg2);
            Assert.IsFalse(result2);
            Assert.AreEqual("The destination host is unreachable.", msg2);

            var result3 = _service.Check(TestUtility.CreateIcmpEndpoint("A", icmpAddress: "asdffdsa"), out string msg3);
            Assert.IsFalse(result3);
            Assert.AreEqual("Unable to ping: OH NO.", msg3);
        }
    }
}
