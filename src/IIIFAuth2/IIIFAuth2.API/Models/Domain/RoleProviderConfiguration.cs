using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace IIIFAuth2.API.Models.Domain;

/// <summary>
/// A collection of <see cref="IProviderConfiguration"/> objects, keyed as dictionary.
/// </summary>
/// <remarks>
/// The key will always be "default" but is a placeholder to allow more flexible configuration in the future. For auth1
/// it stored hostname as different RoleProviders could be used per host
/// </remarks>
public class RoleProviderConfiguration : Dictionary<string, IProviderConfiguration>
{
    public const string DefaultKey = "default";

    public IProviderConfiguration GetDefaultConfiguration()
        => this[DefaultKey];
}

/// <summary>
/// Marker interface role-provider properties that are common to all types
/// </summary>
public interface IProviderConfiguration
{
    /// <summary>
    /// Optional title to be displayed on popup to capture significant gesture
    /// </summary>
    public string? GestureTitle { get; set; }
    
    /// <summary>
    /// Optional title to be displayed on popup to capture significant gesture
    /// </summary>
    public string? GestureMessage { get; set; }

    /// <summary>
    /// The type of configuration, this determines how it is handled
    /// </summary>
    public RoleProviderType Config { get; set; }
}

/// <summary>
/// Configuration for clickthrough authentication, which the DLCS handles itself without any external system 
/// </summary>
public class ClickthroughConfiguration : IProviderConfiguration
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
public class OidcConfiguration : IProviderConfiguration
{
    public string Provider { get; set; } = null!;
    public string Domain { get; set; } = null!;
    public string Scopes { get; set; } = null!;
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
    public Dictionary<string, string[]> Mapping { get; set; } = new();
    
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
    Oidc
}