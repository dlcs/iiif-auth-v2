using System.Net;
using IIIFAuth2.Management.Infrastructure.Auth;

namespace IIIFAuth2.Management.Infrastructure.Dlcs;

/// <summary>
/// Delegating handler that adds any incoming authorization headers to outgoing request.
/// If no Auth header found, request is shortcut with 401 response.
/// </summary>
public class AmbientAuthHandler(IHttpContextAccessor contextAccessor, ILogger<AmbientAuthHandler> logger)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var authHeader = contextAccessor.HttpContext?.Request.TryGetValidAuthHeader();
        if (authHeader == null)
        {
            // If we have no auth don't bother making the downstream request as it'll fail
            logger.LogDebug("Authentication header not found, aborting request for {Path}", request.RequestUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        }

        request.Headers.Authorization = authHeader;
        return base.SendAsync(request, cancellationToken);
    }
}
