using Microsoft.VisualStudio.TestTools.UnitTesting;
using sama.Extensions;
using sama.Models;
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

            var ep2 = new Endpoint { Kind = Endpoint.EndpointKind.Http };

            var vm2 = ep2.ToEndpointViewModel();
            Assert.IsInstanceOfType(vm2, typeof(HttpEndpointViewModel));
        }

        [TestMethod]
        public void ShouldConvertHttpViewModelToEndpoint()
        {
            var vm1 = new HttpEndpointViewModel { Id = 5, Enabled = true, Kind = Endpoint.EndpointKind.Http, Name = "fdsa", Location = "asdf", ResponseMatch = "qwerty", StatusCodes = " 201, 500" };
            var ep1 = vm1.ToEndpoint();
            Assert.AreEqual(5, ep1.Id);
            Assert.IsTrue(ep1.Enabled);
            Assert.AreEqual(Endpoint.EndpointKind.Http, ep1.Kind);
            Assert.AreEqual("fdsa", ep1.Name);
            Assert.AreEqual("asdf", ep1.GetHttpLocation());
            Assert.AreEqual("qwerty", ep1.GetHttpResponseMatch());
            CollectionAssert.AreEquivalent(new List<int> { 201, 500 }, ep1.GetHttpStatusCodes());

            var vm2 = new HttpEndpointViewModel { Kind = Endpoint.EndpointKind.Http };
            var ep2 = vm2.ToEndpoint();
            Assert.AreEqual(Endpoint.EndpointKind.Http, ep2.Kind);
        }
    }
}
