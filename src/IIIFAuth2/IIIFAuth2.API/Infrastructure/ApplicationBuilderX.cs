using IIIFAuth2.API.Infrastructure.Web;

namespace IIIFAuth2.API.Infrastructure;

public static class ApplicationBuilderX
{
    /// <summary>
    /// Use forwarded headers middleware, recording the x-forwarded-for header value received beforehand so that
    /// <see cref="ClientIpProvider"/> can determine whether the client IP was set from it.
    /// </summary>
    /// <param name="app">Current <see cref="IApplicationBuilder"/> instance</param>
    /// <returns>Current <see cref="IApplicationBuilder"/> instance</returns>
    public static IApplicationBuilder UseForwardedHeadersWithClientIp(this IApplicationBuilder app)
        => app
            .Use((context, next) =>
            {
                ClientIpProvider.RecordReceivedForwardedFor(context);
                return next(context);
            })
            .UseForwardedHeaders();

    /// <summary>
    /// Configure app to use pathBase, if specified. If pathBase null or whitespace then no-op.
    /// </summary>
    /// <param name="app">Current <see cref="IApplicationBuilder"/> instance</param>
    /// <param name="pathBase">PathBase value.</param>
    /// <param name="logger">Current <see cref="ILogger"/> instance</param>
    /// <returns>Current <see cref="IApplicationBuilder"/> instance</returns>
    public static IApplicationBuilder HandlePathBase(this IApplicationBuilder app, string? pathBase, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(pathBase))
        {
            logger.LogDebug("No PathBase specified");
            return app;
        }

        logger.LogInformation("Using PathBase '{PathBase}'", pathBase);
        app.UsePathBase($"/{pathBase}");
        return app;
    }
}