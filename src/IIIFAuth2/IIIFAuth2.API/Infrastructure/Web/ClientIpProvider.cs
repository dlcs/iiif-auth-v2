using System.Net;

namespace IIIFAuth2.API.Infrastructure.Web;

/// <summary>
/// Provides the IP address of the client making the current request
/// </summary>
public interface IClientIpProvider
{
    /// <summary>
    /// Get the client IP address for current request, or null if not known.
    /// IPv4-mapped IPv6 addresses are returned as IPv4.
    /// </summary>
    IPAddress? GetClientIp();
}

/// <summary>
/// <see cref="IClientIpProvider"/> using <see cref="ConnectionInfo.RemoteIpAddress"/>. This will have been set from
/// x-forwarded-for header by forwarded headers middleware, if request came via a known proxy.
/// </summary>
public class ClientIpProvider(IHttpContextAccessor httpContextAccessor) : IClientIpProvider
{
    public IPAddress? GetClientIp()
    {
        var remoteIpAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
        if (remoteIpAddress == null) return null;

        return remoteIpAddress.IsIPv4MappedToIPv6 ? remoteIpAddress.MapToIPv4() : remoteIpAddress;
    }
}
