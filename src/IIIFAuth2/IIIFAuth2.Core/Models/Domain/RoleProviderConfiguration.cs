using System.Diagnostics.CodeAnalysis;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace IIIFAuth2.Core.Models.Domain;

/// <summary>
/// A collection of <see cref="IProviderConfiguration"/> objects, keyed as dictionary.
/// A key of "default" will always be present. Further keys that match incoming host can be supplied, this allows for
/// different configurations for different hosts.
/// </summary>
public class RoleProviderConfiguration : Dictionary<string, IProviderConfiguration>
{
    public const string DefaultKey = "default";
    
    /// <summary>
    /// Get the most appropriate <see cref="IProviderConfiguration"/> object. This will be host-specific, if found,
    /// or fallback to Default config. Throws if neither found.
    /// </summary>
    public IProviderConfiguration GetConfiguration(string host)
        => GetConfigurationOrDefault(host) ??
           throw new KeyNotFoundException($"No configuration found for host '{host}' or '{DefaultKey}'");

    /// <summary>
    /// Get the most appropriate <see cref="IProviderConfiguration"/> object. This will be host-specific, if found,
    /// or fallback to Default config. Null if neither found.
    /// </summary>
    public IProviderConfiguration? GetConfigurationOrDefault(string host)
        => TryGetValue(host, out var configuration) || TryGetValue(DefaultKey, out configuration)
            ? configuration
            : null;
}

/// <summary>
/// Marker interface role-provider properties that are common to all types
/// </summary>
public interface IProviderConfiguration
{
    /// <summary>
    /// The type of configuration, this determines how it is handled
    /// </summary>
    public RoleProviderType Config { get; set; }
}

/// <summary>
/// Role-provider properties for types that grant roles via user interaction, which may require a significant gesture
/// to be captured before a cookie can be issued
/// </summary>
/// <remarks>
/// This would be 'active' access-services, 'kiosk' and 'external' services have no interaction. Avoided 'active' here
/// as that belongs to access-services whereas this is for role-providers.
/// </remarks>
public interface IInteractiveProviderConfiguration : IProviderConfiguration
{
    /// <summary>
    /// Optional title to be displayed on popup to capture significant gesture
    /// </summary>
    public string? GestureTitle { get; set; }

    /// <summary>
    /// Optional message to be displayed on popup to capture significant gesture
    /// </summary>
    public string? GestureMessage { get; set; }
}

/// <summary>
/// Configuration for clickthrough authentication, which the DLCS handles itself without any external system
/// </summary>
public class ClickthroughConfiguration : IInteractiveProviderConfiguration
{
    /// <summary>
    /// <inheritdoc />
    /// </summary>
    public string? GestureTitle { get; set; }
    
    /// <summary>
    /// <inheritdoc />
    /// </summary>
    public string? GestureMessage { get; set; }
    
    /// <summary>
    /// <inheritdoc />
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public RoleProviderType Config { get; set; }
}

/// <summary>
/// Configuration for using oidc to authenticate user and appropriate roles
/// </summary>
/// <remarks>
/// See https://github.com/dlcs/protagonist/blob/main/docs/rfcs/008-more-access-control-oidc-oauth.md#role-provider---oidc
/// </remarks>
public class OidcConfiguration : IInteractiveProviderConfiguration
{
    public string Provider { get; set; } = null!;
    public string Domain { get; set; } = null!;
    public string ClientId { get; set; } = null!;
    public string ClientSecret { get; set; } = null!;
    public string? Scopes { get; set; }
    public string ClaimType { get; set; } = null!;
    
    /// <summary>
    /// How to handle an unknown claim value 
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public UnknownMappingValueBehaviour UnknownValueBehaviour { get; set; }

    /// <summary>
    /// Role(s) to use if <see cref="UnknownMappingValueBehaviour"/> is 'Fallback'
    /// </summary>
    public string[]? FallbackMapping { get; set; }
    
    /// <summary>
    /// A collection of {claimValue}:{dlcs-role} mappings
    /// </summary>
    public Dictionary<string, string[]>? Mapping { get; set; }
    
    /// <inheritdoc />
    public string? GestureTitle { get; set; }
    
    /// <inheritdoc />
    public string? GestureMessage { get; set; }
    
    /// <inheritdoc />
    [JsonConverter(typeof(StringEnumConverter))]
    public RoleProviderType Config { get; set; }

    public static class SupportedProviders
    {
        public static readonly string[] AuthTypes = ["auth0", "entra"];
    }
}

/// <summary>
/// Configuration for granting roles based on client IP address. Roles granted are those whose access-service uses
/// this role-provider. Roles are never stored in a session, they are evaluated per request.
/// </summary>
public class IpRestrictionConfiguration : IProviderConfiguration
{
    private string[] ranges = [];
    private IPNetwork[]? parsedRanges;

    /// <summary>
    /// CIDR ranges (IPv4 or IPv6) that requests must originate from to be granted roles, e.g. "10.0.0.0/24"
    /// </summary>
    [AllowNull]
    public string[] Ranges
    {
        get => ranges;
        set
        {
            ranges = value ?? [];
            parsedRanges = null;
        }
    }

    /// <summary>
    /// Optional phrase describing where ranges represent, including preposition, used in user-facing wording, e.g.
    /// "in the Reading Room", "on Main Campus" or "via the company VPN". Output as "Only available {locationPhrase}"
    /// </summary>
    public string? LocationPhrase { get; set; }

    /// <summary>
    /// <inheritdoc />
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public RoleProviderType Config { get; set; }

    /// <summary>
    /// Check if provided <see cref="IPAddress"/> is within any of the configured <see cref="Ranges"/>. Invalid ranges
    /// are ignored. IPv4-mapped IPv6 addresses are treated as IPv4.
    /// </summary>
    public bool Contains(IPAddress? ipAddress)
    {
        if (ipAddress == null) return false;
        if (ipAddress.IsIPv4MappedToIPv6) ipAddress = ipAddress.MapToIPv4();

        parsedRanges ??= ParseRanges(ranges);
        return parsedRanges.Any(r => r.Contains(ipAddress));
    }

    private static IPNetwork[] ParseRanges(string[] toParse)
        => toParse
            .Select(r => IPNetwork.TryParse(r.Trim(), out var network) ? network : (IPNetwork?)null)
            .OfType<IPNetwork>()
            .ToArray();
}

/// <summary>
/// How to handle an unknown claim value for oidc role-provider
/// </summary>
public enum UnknownMappingValueBehaviour
{
    /// <summary>
    /// Default placeholder
    /// </summary>
    Unknown,
    
    /// <summary>
    /// Throw an exception, prevent user logging in
    /// </summary>
    Throw,
    
    /// <summary>
    /// Use the claim value as-is
    /// </summary>
    UseClaim,
    
    /// <summary>
    /// Use a default, fallback value as defined in <see cref="OidcConfiguration.FallbackMapping"/>
    /// </summary>
    Fallback,
}

/// <summary>
/// Details the type of role-provider, this will dictate how it is handled and what type it's mapped to
/// </summary>
public enum RoleProviderType
{
    Unknown,
    Clickthrough,
    Oidc,
    IpRestriction
}