using Microsoft.VisualStudio.TestTools.UnitTesting;
using sama.Extensions;
using sama.Models;
using System;

namespace TestSama.Extensions
{
    [TestClass]
    public class EndpointIcmpExtensionsTests
    {
        [TestMethod]
        public void ShouldNotGetOrSetIcmpDataOnHttpEndpoint()
        {
            var icmpEndpoint = new Endpoint { Kind = Endpoint.EndpointKind.Http };
            Assert.ThrowsException<ArgumentException>(() => icmpEndpoint.GetIcmpAddress());
            Assert.ThrowsException<ArgumentException>(() => icmpEndpoint.SetIcmpAddress(""));
        }

        [TestMethod]
        public void ShouldSetIcmpAddress()
        {
            var ep1 = new Endpoint { Kind = Endpoint.EndpointKind.Icmp };
            ep1.SetIcmpAddress("asdf");
            Assert.AreEqual(@"{""Address"":""asdf""}", ep1.JsonConfig);

            var ep2 = new Endpoint { Kind = Endpoint.EndpointKind.Icmp, JsonConfig = @"{""Stuff"": ""things""}" };
            ep2.SetIcmpAddress("asdf");
            Assert.AreEqual(@"{""Stuff"":""things"",""Address"":""asdf""}", ep2.JsonConfig);

            var ep3 = new Endpoint { Kind = Endpoint.EndpointKind.Icmp, JsonConfig = @"{""Address"":""something""}" };
            ep3.SetIcmpAddress("asdf");
            Assert.AreEqual(@"{""Address"":""asdf""}", ep3.JsonConfig);
        }

        [TestMethod]
        public void ShouldGetIcmpAddress()
        {
            var ep1 = new Endpoint { Kind = Endpoint.EndpointKind.Icmp, JsonConfig = null };
            Assert.IsNull(ep1.GetIcmpAddress());

            var ep2 = new Endpoint { Kind = Endpoint.EndpointKind.Icmp, JsonConfig = @"{""Address"":null}" };
            Assert.IsNull(ep2.GetIcmpAddress());

            var ep3 = new Endpoint { Kind = Endpoint.EndpointKind.Icmp, JsonConfig = @"{""Stuff"":""things""}" };
            Assert.IsNull(ep3.GetIcmpAddress());

            var ep4 = new Endpoint { Kind = Endpoint.EndpointKind.Icmp, JsonConfig = @"{""Address"":""asdf""}" };
            Assert.AreEqual("asdf", ep4.GetIcmpAddress());
        }
    }
}
