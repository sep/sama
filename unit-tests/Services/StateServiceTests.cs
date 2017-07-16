using Microsoft.VisualStudio.TestTools.UnitTesting;
using sama.Models;
using sama.Services;
using System;

namespace TestSama.Services
{
    [TestClass]
    public class StateServiceTests
    {
        private StateService _service;

        private Endpoint Ep1 = new Endpoint { Id = 1, Name = "A" };
        private Endpoint Ep2 = new Endpoint { Id = 2, Name = "B" };
        private Endpoint Ep3 = new Endpoint { Id = 3, Name = "C" };

        [TestInitialize]
        public void Setup()
        {
            _service = new StateService();

            _service.SetState(Ep1, null, null);
            _service.SetState(Ep2, true, null);
            _service.SetState(Ep3, false, new Exception("ERROR"));
        }

        [TestMethod]
        public void ShouldSetAndRetrieveEndpointState()
        {
            AssertStateEquality(null, null, _service.GetState(1));
            AssertStateEquality(true, null, _service.GetState(2));
            AssertStateEquality(false, new Exception("ERROR"), _service.GetState(3));

            var allStates = _service.GetAllStates();
            Assert.AreEqual(3, allStates.Count);
            AssertStateEquality(null, null, allStates[Ep1]);
            AssertStateEquality(true, null, allStates[Ep2]);
            AssertStateEquality(false, new Exception("ERROR"), allStates[Ep3]);
        }

        [TestMethod]
        public void ShouldUpdateEndpointState()
        {
            AssertStateEquality(true, null, _service.GetState(2));
            AssertStateEquality(false, new Exception("ERROR"), _service.GetState(3));

            _service.SetState(Ep2, null, null);

            AssertStateEquality(null, null, _service.GetState(2));
            AssertStateEquality(false, new Exception("ERROR"), _service.GetState(3));

            var allStates = _service.GetAllStates();
            Assert.AreEqual(3, allStates.Count);
            AssertStateEquality(null, null, allStates[Ep2]);
        }

        [TestMethod]
        public void ShouldRemoveEndpointState()
        {
            _service.RemoveState(2);

            Assert.IsNotNull(_service.GetState(1));
            Assert.IsNull(_service.GetState(2));
            Assert.IsNotNull(_service.GetState(3));
            Assert.AreEqual(2, _service.GetAllStates().Count);

            _service.RemoveState(2);

            Assert.AreEqual(2, _service.GetAllStates().Count);

            _service.RemoveState(1);

            Assert.AreEqual(1, _service.GetAllStates().Count);

            _service.RemoveState(3);

            Assert.AreEqual(0, _service.GetAllStates().Count);
        }

        private void AssertStateEquality(bool? expectedIsUp, Exception expectedException, StateService.EndpointState actualState)
        {
            Assert.AreEqual(expectedIsUp, actualState.IsUp);
            
            if (expectedException == null)
            {
                Assert.IsNull(actualState.Exception);
            }
            else
            {
                Assert.AreEqual(expectedException.GetType(), actualState.Exception.GetType());
                Assert.AreEqual(expectedException.Message, actualState.Exception.Message);
            }
        }
    }
}
