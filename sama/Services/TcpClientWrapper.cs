using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;

namespace sama.Services
{
    /// <summary>
    /// This is a wrapper around TcpClient, which cannot be (easily) tested.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class TcpClientWrapper
    {
        public virtual void SendData(string address, int port, byte[] data)
        {
            using var client = new TcpClient(address, port);
            using var stream = client.GetStream();
            stream.Write(data, 0, data.Length);
        }
    }
}
