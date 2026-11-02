using System.Net;
using System.Net.Sockets;
using IIIFAuth2.API.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

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
/// <see cref="IClientIpProvider"/> using <see cref="ConnectionInfo.RemoteIpAddress"/>, only if it has been set from
/// x-forwarded-for header by forwarded headers middleware.
/// </summary>
/// <remarks>
/// This service is called by Orchestrator, rather than clients, so the direct caller's IP address is never the client
/// IP. If x-forwarded-for is missing, or not trusted (e.g. not from a KnownNetwork), the client IP is unknown. This
/// prevents the IP of the caller (e.g. Orchestrator) being evaluated against IpRestriction ranges.
/// If the resolved IP is itself a known proxy (KnownNetworks or KnownProxies) the client IP is also unknown. This
/// happens when a proxy forwards a request without x-forwarded-for, e.g. Orchestrator via an ALB, where the ALB adds
/// Orchestrator's IP.
/// Requires <see cref="RecordReceivedForwardedFor"/> to be called prior to forwarded headers middleware.
/// </remarks>
public class ClientIpProvider(
    IHttpContextAccessor httpContextAccessor,
    IOptions<ForwardedHeadersOptions> forwardedHeadersOptions,
    ILogger<ClientIpProvider> logger) : IClientIpProvider
{
    private const string ReceivedForwardedForKey = "__ReceivedXForwardedFor";

    public IPAddress? GetClientIp()
    {
        var context = httpContextAccessor.SafeHttpContext();
        if (!WasForwardedForApplied(context)) return null;

        var remoteIpAddress = context.Connection.RemoteIpAddress;
        if (remoteIpAddress == null) return null;

        var clientIp = remoteIpAddress.IsIPv4MappedToIPv6 ? remoteIpAddress.MapToIPv4() : remoteIpAddress;
        if (IsKnownProxy(clientIp))
        {
            logger.LogWarning("Client IP {ClientIp} is a known proxy, client IP is unknown", clientIp);
            return null;
        }

        return clientIp;
    }

    private bool IsKnownProxy(IPAddress clientIp)
    {
        // Known proxies/networks may be configured as IPv4 or IPv4-mapped IPv6, so check both forms
        var options = forwardedHeadersOptions.Value;
        var mappedIp = clientIp.AddressFamily == AddressFamily.InterNetwork ? clientIp.MapToIPv6() : clientIp;
        return options.KnownProxies.Any(p => p.Equals(clientIp) || p.Equals(mappedIp)) ||
               options.KnownIPNetworks.Any(n => n.Contains(clientIp) || n.Contains(mappedIp));
    }

    /// <summary>
    /// Record the x-forwarded-for header value as received, before forwarded headers middleware has processed it
    /// </summary>
    public static void RecordReceivedForwardedFor(HttpContext context)
        => context.Items[ReceivedForwardedForKey] = GetForwardedFor(context);

    private static bool WasForwardedForApplied(HttpContext context)
    {
        if (context.Items[ReceivedForwardedForKey] is not string received || string.IsNullOrWhiteSpace(received))
            return false;

        // Forwarded headers middleware removes x-forwarded-for values that it applies to RemoteIpAddress, so if the
        // header is unchanged then it wasn't trusted
        return received != GetForwardedFor(context);
    }

    private static string GetForwardedFor(HttpContext context)
        => context.Request.Headers[ForwardedHeadersDefaults.XForwardedForHeaderName].ToString();
}
