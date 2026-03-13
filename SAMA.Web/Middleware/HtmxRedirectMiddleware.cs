namespace SAMA.Web.Middleware;

/// <summary>
/// Middleware that converts HTTP redirects (302, 303, etc.) to HTMX-compatible responses.
/// When an HTMX request receives a redirect, this middleware intercepts it and converts
/// it to a 200 OK with an HX-Redirect header, which HTMX understands.
/// </summary>
public class HtmxRedirectMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Early exit if not an HTMX request - no overhead for normal requests
        var isHtmxRequest = context.Request.Headers["HX-Request"] == "true";

        if (!isHtmxRequest)
        {
            await next(context);
            return;
        }

        // For HTMX requests, intercept before response headers are sent
        context.Response.OnStarting(() =>
        {
            // Check if response is a redirect
            var isRedirect = context.Response.StatusCode is >= 300 and < 400;

            if (isRedirect)
            {
                var location = context.Response.Headers.Location.ToString();

                if (!string.IsNullOrEmpty(location))
                {
                    // Convert to HTMX-compatible response
                    context.Response.StatusCode = 200;
                    _ = context.Response.Headers.Remove("Location");
                    context.Response.Headers["HX-Redirect"] = location;
                }
            }

            return Task.CompletedTask;
        });

        await next(context);
    }
}
