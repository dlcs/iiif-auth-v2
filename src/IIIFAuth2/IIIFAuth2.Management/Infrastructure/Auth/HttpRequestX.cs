using System.Net.Http.Headers;

namespace IIIFAuth2.Management.Infrastructure.Auth;

public static class HttpRequestX
{
    private const string AuthHeader = "Authorization";

    /// <summary>
    /// Attempt to get and parse "Authorization" header from specified httpRequest
    /// </summary>
    public static AuthenticationHeaderValue? TryGetValidAuthHeader(this HttpRequest request)
    {
        if (!request.Headers.TryGetValue(AuthHeader, out var value))
        {
            // Authorization header not in request
            return null;
        }

        if (!AuthenticationHeaderValue.TryParse(value, out AuthenticationHeaderValue? headerValue)
            || string.IsNullOrEmpty(headerValue.Parameter))
        {
            // Invalid Authorization header
            return null;
        }

        return headerValue;
    }

    /// <summary>
    /// Retrieve the customer id from "customerId" route value
    /// </summary>
    /// <returns>A parsed customer id, or null if not found or not numeric</returns>
    public static int? GetCustomerId(this HttpRequest request, ILogger logger)
    {
        const string customerIdRouteValue = "customerId";

        if (!request.RouteValues.TryGetValue(customerIdRouteValue, out var customerIdRouteVal)
            || customerIdRouteVal is null)
        {
            logger.LogDebug("Unable to identify customerId in auth request to {Path}", request.Path);
            return null;
        }

        if (!int.TryParse(customerIdRouteVal.ToString(), out var customerId))
        {
            logger.LogDebug("Specified customerId is not numeric {Path}", request.Path);
            return null;
        }

        return customerId;
    }
}
