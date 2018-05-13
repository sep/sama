using Microsoft.VisualStudio.TestTools.UnitTesting;
using sama.Extensions;
using sama.Models;
using System;
using System.Collections.Generic;

namespace TestSama.Extensions
{
    [TestClass]
    public class EndpointConversionExtensionsTests
    {
        [TestMethod]
        public void ShouldConvertHttpEndpointToViewModel()
        {
            var ep1 = new Endpoint { Id = 3, Enabled = true, Kind = Endpoint.EndpointKind.Http, Name = "asdf" };
            ep1.SetHttpLocation("fdsa");
            ep1.SetHttpResponseMatch("qwerty");
            ep1.SetHttpStatusCodes(new List<int> { 302 });
            ep1.SetHttpIgnoreTlsCerts(true);
            ep1.SetHttpCustomTlsCert("zxcvbnm");

            var vm1 = ep1.ToEndpointViewModel();
            Assert.IsInstanceOfType(vm1, typeof(HttpEndpointViewModel));
            Assert.AreEqual(3, vm1.Id);
            Assert.IsTrue(vm1.Enabled);
            Assert.AreEqual(Endpoint.EndpointKind.Http, vm1.Kind);
            Assert.AreEqual("HTTP", vm1.KindString);
            Assert.AreEqual("asdf", vm1.Name);
            var http1 = (HttpEndpointViewModel)vm1;
            Assert.AreEqual("fdsa", http1.Location);
            Assert.AreEqual("qwerty", http1.ResponseMatch);
            Assert.AreEqual("302", http1.StatusCodes);
            Assert.IsTrue(http1.IgnoreCerts);
            Assert.AreEqual("zxcvbnm", http1.CustomCert);

            var ep2 = new Endpoint { Kind = Endpoint.EndpointKind.Http };

            var vm2 = ep2.ToEndpointViewModel();
            Assert.IsInstanceOfType(vm2, typeof(HttpEndpointViewModel));
        }

        [TestMethod]
        public void ShouldConvertIcmpEndpointToViewModel()
        {
            var ep1 = new Endpoint { Id = 3, Enabled = true, Kind = Endpoint.EndpointKind.Icmp, Name = "asdf" };
            ep1.SetIcmpAddress("fdsa");

            var vm1 = ep1.ToEndpointViewModel();
            Assert.IsInstanceOfType(vm1, typeof(IcmpEndpointViewModel));
            Assert.AreEqual(3, vm1.Id);
            Assert.IsTrue(vm1.Enabled);
            Assert.AreEqual(Endpoint.EndpointKind.Icmp, vm1.Kind);
            Assert.AreEqual("Ping", vm1.KindString);
            Assert.AreEqual("asdf", vm1.Name);
            var icmp1 = (IcmpEndpointViewModel)vm1;
            Assert.AreEqual("fdsa", icmp1.Address);

            var ep2 = new Endpoint { Kind = Endpoint.EndpointKind.Icmp };

            var vm2 = ep2.ToEndpointViewModel();
            Assert.IsInstanceOfType(vm2, typeof(IcmpEndpointViewModel));
        }

        [TestMethod]
        public void ShouldConvertHttpViewModelToEndpoint()
        {
            var vm1 = new HttpEndpointViewModel { Id = 5, Enabled = true, Kind = Endpoint.EndpointKind.Http, Name = "fdsa", Location = "asdf", ResponseMatch = "qwerty", StatusCodes = " 201, 500", IgnoreCerts = true, CustomCert = "zxcvbnm" };
            var ep1 = vm1.ToEndpoint();
            Assert.AreEqual(5, ep1.Id);
            Assert.IsTrue(ep1.Enabled);
            Assert.AreEqual(Endpoint.EndpointKind.Http, ep1.Kind);
            Assert.AreEqual("fdsa", ep1.Name);
            Assert.AreEqual("asdf", ep1.GetHttpLocation());
            Assert.AreEqual("qwerty", ep1.GetHttpResponseMatch());
            CollectionAssert.AreEquivalent(new List<int> { 201, 500 }, ep1.GetHttpStatusCodes());
            Assert.IsTrue(ep1.GetHttpIgnoreTlsCerts());
            Assert.AreEqual("zxcvbnm", ep1.GetHttpCustomTlsCert());
            Assert.IsTrue(DateTimeOffset.UtcNow.AddMinutes(-1) < ep1.LastUpdated);

            var vm2 = new HttpEndpointViewModel { Kind = Endpoint.EndpointKind.Http };
            var ep2 = vm2.ToEndpoint();
            Assert.AreEqual(Endpoint.EndpointKind.Http, ep2.Kind);
            Assert.IsTrue(DateTimeOffset.UtcNow.AddMinutes(-1) < ep2.LastUpdated);
        }

        [TestMethod]
        public void ShouldConvertIcmpViewModelToEndpoint()
        {
            var vm1 = new IcmpEndpointViewModel { Id = 5, Enabled = true, Kind = Endpoint.EndpointKind.Icmp, Name = "fdsa", Address = "asdf" };
            var ep1 = vm1.ToEndpoint();
            Assert.AreEqual(5, ep1.Id);
            Assert.IsTrue(ep1.Enabled);
            Assert.AreEqual(Endpoint.EndpointKind.Icmp, ep1.Kind);
            Assert.AreEqual("fdsa", ep1.Name);
            Assert.AreEqual("asdf", ep1.GetIcmpAddress());
            Assert.IsTrue(DateTimeOffset.UtcNow.AddMinutes(-1) < ep1.LastUpdated);

            var vm2 = new IcmpEndpointViewModel { Kind = Endpoint.EndpointKind.Icmp };
            var ep2 = vm2.ToEndpoint();
            Assert.AreEqual(Endpoint.EndpointKind.Icmp, ep2.Kind);
            Assert.IsTrue(DateTimeOffset.UtcNow.AddMinutes(-1) < ep2.LastUpdated);
        }
    }
}
