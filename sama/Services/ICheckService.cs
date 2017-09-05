using sama.Models;

namespace sama.Services
{
    public interface ICheckService
    {
        bool CanHandle(Endpoint endpoint);
        bool Check(Endpoint endpoint, out string failureMessage);
    }
}
