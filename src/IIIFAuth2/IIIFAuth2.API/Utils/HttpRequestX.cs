using System.Text;

namespace IIIFAuth2.API.Utils;

public static class HttpRequestX
{
    private const string SchemeDelimiter = "://";
    
    /// <summary>
    /// Generate a full display URL, deriving values from specified HttpRequest
    /// </summary>
    /// <param name="request">HttpRequest to generate display URL for</param>
    /// <param name="path">Path to append to URL</param>
    /// <param name="includeQueryParams">If true, query params are included in path. Else they are omitted</param>
    /// <returns>Full URL, including scheme, host, pathBase, path and queryString</returns>
    /// <remarks>
    /// based on Microsoft.AspNetCore.Http.Extensions.UriHelper.GetDisplayUrl(this HttpRequest request)
    /// </remarks>
    public static string GetDisplayUrl(this HttpRequest request, string? path = null, bool includeQueryParams = true)
    {
        var host = request.Host.Value ?? string.Empty;
        var scheme = request.Scheme ?? string.Empty;
        var pathBase = request.PathBase.Value ?? string.Empty;
        var queryString = includeQueryParams
            ? request.QueryString.Value ?? string.Empty
            : string.Empty;
        var pathElement = path ?? string.Empty;

        // PERF: Calculate string length to allocate correct buffer size for StringBuilder.
        var length = scheme.Length + SchemeDelimiter.Length + host.Length
                     + pathBase.Length + pathElement.Length + queryString.Length;

        return new StringBuilder(length)
            .Append(scheme)
            .Append(SchemeDelimiter)
            .Append(host)
            .Append(pathBase)
            .Append(path)
            .Append(queryString)
            .ToString();
    }

    /// <summary>
    /// Compare provided origin with current HttpRequest origin.
    /// Matches scheme, hostname, and port
    /// </summary>
    /// <param name="request">Current HttpRequest</param>
    /// <param name="origin">Origin to compare, containing scheme</param>
    /// <returns>true if provided origin is same as current host, else false</returns>
    /// <remarks>
    /// See https://developer.mozilla.org/en-US/docs/Web/API/Window/postMessage
    /// </remarks>
    public static bool IsSameOrigin(this HttpRequest request, Uri origin)
    {
        // Note: Uri sets Port to default (80 or 443, depending on scheme) if not set.
        // HttpRequest.Host does not default
        var hostPort = request.Host.Port ?? (request.Scheme == "http" ? 80 : 443);
        
        return string.Equals(request.Host.Host, origin.Host, StringComparison.OrdinalIgnoreCase)
               && string.Equals(request.Scheme, origin.Scheme, StringComparison.OrdinalIgnoreCase)
               && hostPort == origin.Port;
    }
}