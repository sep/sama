using Microsoft.VisualStudio.TestTools.UnitTesting;
using sama.Extensions;
using sama.Models;
using System;
using System.Collections.Generic;

namespace TestSama.Extensions
{
    [TestClass]
    public class EndpointHttpExtensionsTests
    {
        [TestMethod]
        public void ShouldNotGetOrSetHttpDataOnIcmpEndpoint()
        {
            var icmpEndpoint = new Endpoint { Kind = Endpoint.EndpointKind.Icmp };
            Assert.ThrowsException<ArgumentException>(() => icmpEndpoint.GetHttpLocation());
            Assert.ThrowsException<ArgumentException>(() => icmpEndpoint.GetHttpResponseMatch());
            Assert.ThrowsException<ArgumentException>(() => icmpEndpoint.GetHttpStatusCodes());
            Assert.ThrowsException<ArgumentException>(() => icmpEndpoint.SetHttpLocation(""));
            Assert.ThrowsException<ArgumentException>(() => icmpEndpoint.SetHttpResponseMatch(""));
            Assert.ThrowsException<ArgumentException>(() => icmpEndpoint.SetHttpStatusCodes(null));
        }

        [TestMethod]
        public void ShouldSetHttpLocation()
        {
            var ep1 = new Endpoint { Kind = Endpoint.EndpointKind.Http };
            ep1.SetHttpLocation("http://asdf.example.com/fdsa");
            Assert.AreEqual(@"{""Location"":""http://asdf.example.com/fdsa""}", ep1.JsonConfig);

            var ep2 = new Endpoint { Kind = Endpoint.EndpointKind.Http, JsonConfig = @"{""Stuff"": ""things""}" };
            ep2.SetHttpLocation("http://asdf.example.com/fdsa");
            Assert.AreEqual(@"{""Stuff"":""things"",""Location"":""http://asdf.example.com/fdsa""}", ep2.JsonConfig);
            
            var ep3 = new Endpoint { Kind = Endpoint.EndpointKind.Http, JsonConfig = @"{""Location"":""something""}" };
            ep3.SetHttpLocation("http://asdf.example.com/fdsa");
            Assert.AreEqual(@"{""Location"":""http://asdf.example.com/fdsa""}", ep3.JsonConfig);
        }

        [TestMethod]
        public void ShouldGetHttpLocation()
        {
            var ep1 = new Endpoint { Kind = Endpoint.EndpointKind.Http, JsonConfig = null };
            Assert.IsNull(ep1.GetHttpLocation());

            var ep2 = new Endpoint { Kind = Endpoint.EndpointKind.Http, JsonConfig = @"{""Location"":null}" };
            Assert.IsNull(ep2.GetHttpLocation());

            var ep3 = new Endpoint { Kind = Endpoint.EndpointKind.Http, JsonConfig = @"{""Stuff"":""things""}" };
            Assert.IsNull(ep3.GetHttpLocation());

            var ep4 = new Endpoint { Kind = Endpoint.EndpointKind.Http, JsonConfig = @"{""Location"":""asdf""}" };
            Assert.AreEqual("asdf", ep4.GetHttpLocation());
        }

        [TestMethod]
        public void ShouldSetHttpResponseMatch()
        {
            var ep1 = new Endpoint { Kind = Endpoint.EndpointKind.Http };
            ep1.SetHttpResponseMatch("ok");
            Assert.AreEqual(@"{""ResponseMatch"":""ok""}", ep1.JsonConfig);

            var ep2 = new Endpoint { Kind = Endpoint.EndpointKind.Http, JsonConfig = @"{""Stuff"": ""things""}" };
            ep2.SetHttpResponseMatch("ok");
            Assert.AreEqual(@"{""Stuff"":""things"",""ResponseMatch"":""ok""}", ep2.JsonConfig);

            var ep3 = new Endpoint { Kind = Endpoint.EndpointKind.Http, JsonConfig = @"{""ResponseMatch"":""something""}" };
            ep3.SetHttpResponseMatch("ok");
            Assert.AreEqual(@"{""ResponseMatch"":""ok""}", ep3.JsonConfig);
        }

        [TestMethod]
        public void ShouldGetHttpResponseMatch()
        {
            var ep1 = new Endpoint { Kind = Endpoint.EndpointKind.Http, JsonConfig = null };
            Assert.IsNull(ep1.GetHttpResponseMatch());

            var ep2 = new Endpoint { Kind = Endpoint.EndpointKind.Http, JsonConfig = @"{""ResponseMatch"":null}" };
            Assert.IsNull(ep2.GetHttpResponseMatch());

            var ep3 = new Endpoint { Kind = Endpoint.EndpointKind.Http, JsonConfig = @"{""Stuff"":""things""}" };
            Assert.IsNull(ep3.GetHttpResponseMatch());

            var ep4 = new Endpoint { Kind = Endpoint.EndpointKind.Http, JsonConfig = @"{""ResponseMatch"":""asdf""}" };
            Assert.AreEqual("asdf", ep4.GetHttpResponseMatch());
        }

        [TestMethod]
        public void ShouldSetHttpStatusCodes()
        {
            var ep1 = new Endpoint { Kind = Endpoint.EndpointKind.Http };
            ep1.SetHttpStatusCodes(null);
            Assert.AreEqual(@"{""StatusCodes"":null}", ep1.JsonConfig);

            var ep2 = new Endpoint { Kind = Endpoint.EndpointKind.Http, JsonConfig = @"{""Stuff"": ""things""}" };
            ep2.SetHttpStatusCodes(new List<int> { 404 });
            Assert.AreEqual(@"{""Stuff"":""things"",""StatusCodes"":[404]}", ep2.JsonConfig);

            var ep3 = new Endpoint { Kind = Endpoint.EndpointKind.Http, JsonConfig = @"{""StatusCodes"":""something""}" };
            ep3.SetHttpStatusCodes(new List<int> { 200, 201 });
            Assert.AreEqual(@"{""StatusCodes"":[200,201]}", ep3.JsonConfig);

            var ep4 = new Endpoint { Kind = Endpoint.EndpointKind.Http };
            ep3.SetHttpStatusCodes(new List<int>());
            Assert.AreEqual(@"{""StatusCodes"":[]}", ep3.JsonConfig);
        }

        [TestMethod]
        public void ShouldGetHttpStatusCodes()
        {
            var ep1 = new Endpoint { Kind = Endpoint.EndpointKind.Http, JsonConfig = null };
            Assert.IsNull(ep1.GetHttpStatusCodes());

            var ep2 = new Endpoint { Kind = Endpoint.EndpointKind.Http, JsonConfig = @"{""StatusCodes"":null}" };
            Assert.IsNull(ep2.GetHttpStatusCodes());

            var ep3 = new Endpoint { Kind = Endpoint.EndpointKind.Http, JsonConfig = @"{""Stuff"":""things""}" };
            Assert.IsNull(ep3.GetHttpStatusCodes());

            var ep4 = new Endpoint { Kind = Endpoint.EndpointKind.Http, JsonConfig = @"{""StatusCodes"":[]}" };
            Assert.AreEqual(0, ep4.GetHttpStatusCodes().Count);

            var ep5 = new Endpoint { Kind = Endpoint.EndpointKind.Http, JsonConfig = @"{""StatusCodes"":[200,404,500]}" };
            CollectionAssert.AreEquivalent(new List<int> { 200, 404, 500 }, ep5.GetHttpStatusCodes());
        }
    }
}
