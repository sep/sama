using System;
using System.Net.Http;
using System.Net.Security;

namespace sama;

public class HttpHandlerFactory
{
	public virtual HttpMessageHandler Create(bool allowAutoRedirect, SslClientAuthenticationOptions? sslOptions)
	{
		var handler = new SocketsHttpHandler
		{
			PooledConnectionLifetime = TimeSpan.Zero,
			AllowAutoRedirect = allowAutoRedirect,
		};
		if (sslOptions != null)
		{
			handler.SslOptions = sslOptions;
		}
		return handler;
	}
}
