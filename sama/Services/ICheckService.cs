using sama.Models;

namespace sama.Services
{
    public interface ICheckService
    {
        bool CanHandle(Endpoint endpoint);
        EndpointCheckResult Check(Endpoint endpoint);
    }
}
