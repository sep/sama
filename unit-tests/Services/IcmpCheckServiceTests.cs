using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama.Models;
using sama.Services;
using System;
using System.Net.NetworkInformation;

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
            _pingWrapper.SendPing("fdsa").Returns((IPStatus.Success, TimeSpan.FromMilliseconds(5)));

            var result = _service.Check(TestUtility.CreateIcmpEndpoint("A", icmpAddress: "fdsa"));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(TimeSpan.FromMilliseconds(5), result.ResponseTime);
            Assert.IsNull(result.Error);
        }

        [TestMethod]
        public void ShouldReturnFailureWithCorrectMessagesWhenPingFails()
        {
            _pingWrapper.SendPing("fdsa").Returns((IPStatus.TimedOut, TimeSpan.MinValue));
            _pingWrapper.SendPing("asdf").Returns((IPStatus.DestinationHostUnreachable, TimeSpan.MinValue));
            _pingWrapper.When(call => call.SendPing("asdffdsa"))
                .Throw(new Exception("OH NO"));

            var result1 = _service.Check(TestUtility.CreateIcmpEndpoint("A", icmpAddress: "fdsa"));
            Assert.IsFalse(result1.Success);
            Assert.AreEqual("The ping request timed out", result1.Error.Message);

            var result2 = _service.Check(TestUtility.CreateIcmpEndpoint("A", icmpAddress: "asdf"));
            Assert.IsFalse(result2.Success);
            Assert.AreEqual("The destination host is unreachable", result2.Error.Message);

            var result3 = _service.Check(TestUtility.CreateIcmpEndpoint("A", icmpAddress: "asdffdsa"));
            Assert.IsFalse(result3.Success);
            Assert.AreEqual("Unable to ping: OH NO", result3.Error.Message);
        }
    }
}
