using System.Text;

namespace IIIFAuth2.API.Utils;

public static class UriX
{
    private const string SchemeDelimiter = "://";
    
    /// <summary>
    /// Get web-content origin, the scheme, host and port (if non-default)
    /// </summary>
    /// <remarks>See https://developer.mozilla.org/en-US/docs/Glossary/Origin</remarks>
    public static string GetOrigin(this Uri uri)
    {
        var host = uri.Host;
        var scheme = uri.Scheme;
        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";

        // PERF: Calculate string length to allocate correct buffer size for StringBuilder.
        var length = scheme.Length + SchemeDelimiter.Length + host.Length + port.Length;

        return new StringBuilder(length)
            .Append(scheme)
            .Append(SchemeDelimiter)
            .Append(host)
            .Append(port)
            .ToString();
    }
}