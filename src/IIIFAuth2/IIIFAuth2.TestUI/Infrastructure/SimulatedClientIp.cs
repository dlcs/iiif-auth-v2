namespace IIIFAuth2.TestUI.Infrastructure;

/// <summary>
/// Client IP address sent to iiif-auth-v2 as x-forwarded-for, allowing IpRestriction roles to be tested from a single
/// machine. If not set the IP address of the incoming request is used. Held in memory, so lost on restart.
/// </summary>
/// <remarks>
/// This is global rather than per-request as browser requests for content resources (e.g. img src) can't set headers
/// </remarks>
public class SimulatedClientIp
{
    private volatile string? clientIp;

    public string? Value
    {
        get => clientIp;
        set => clientIp = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Get the client IP to send as x-forwarded-for, the simulated value if set, else the request's remote IP
    /// </summary>
    public string? GetClientIp(HttpRequest request)
        => Value ?? request.HttpContext.Connection.RemoteIpAddress?.ToString();
}
