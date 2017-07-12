using sama.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace sama.Services
{
    public class StateService
    {
        public class EndpointState
        {
            public DateTimeOffset LastUpdated { get; set; }
            public bool? IsUp { get; set; }
            public Exception Exception { get; set; }
        }

        private readonly Dictionary<Endpoint, EndpointState> _endpointStates = new Dictionary<Endpoint, EndpointState>();

        public void SetState(Endpoint endpoint, bool? isUp, Exception exception)
        {
            lock (this)
            {
                RemoveState(endpoint.Id);
                _endpointStates.Add(endpoint, new EndpointState { IsUp = isUp, Exception = exception, LastUpdated = DateTimeOffset.UtcNow });
            }
        }

        public IReadOnlyDictionary<Endpoint, EndpointState> GetAllStates()
        {
            lock (this)
            {
                return new ReadOnlyDictionary<Endpoint, EndpointState>(_endpointStates);
            }
        }

        public EndpointState GetState(int id)
        {
            lock (this)
            {
                var key = _endpointStates.Keys.FirstOrDefault(e => e.Id == id);
                return (key == null ? null : _endpointStates[key]);
            }
        }

        internal void RemoveState(int id)
        {
            lock (this)
            {
                while(_endpointStates.Keys.Any(e => e.Id == id))
                {
                    _endpointStates.Remove(_endpointStates.Keys.First(e => e.Id == id));
                }
            }
        }
    }
}
