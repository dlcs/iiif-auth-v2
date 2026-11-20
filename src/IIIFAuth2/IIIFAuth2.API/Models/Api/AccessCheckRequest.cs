using System.Text.Json.Serialization;
using IIIFAuth2.Core.Models.Domain;

namespace IIIFAuth2.API.Models.Api;

/// <summary>
/// Body of POST requests to probe_internal and verifyaccess
/// </summary>
public class AccessCheckRequest
{
    /// <summary>
    /// Role entries that the asset has, any of which grants access if all of its roles are met. Each element is a
    /// string array (one entry) or a string (one entry, "+" or whitespace separating roles)
    /// </summary>
    [JsonConverter(typeof(RoleEntriesJsonConverter))]
    public RoleEntry[]? Roles { get; set; }

    /// <summary>
    /// Optional asset metadata, used to evaluate appointment asset patterns. Only expected if any role is an
    /// appointment role
    /// </summary>
    public AssetMetadataBody? Asset { get; set; }
}

/// <summary>
/// Asset metadata as sent by Orchestrator. Customer and space come from the deliverable id
/// </summary>
public class AssetMetadataBody
{
    public string[]? Scopes { get; set; }

    /// <summary>
    /// Current Protagonist name for <see cref="Scopes"/>, used if <see cref="Scopes"/> not provided. Both are
    /// accepted so Protagonist can rename without a coordinated deployment
    /// </summary>
    public string[]? Manifests { get; set; }
    public string[]? Tags { get; set; }
    public string? String1 { get; set; }
    public string? String2 { get; set; }
    public string? String3 { get; set; }
    public int? Number1 { get; set; }
    public int? Number2 { get; set; }
    public int? Number3 { get; set; }

    public AssetMetadata ToAssetMetadata()
        => new()
        {
            Scopes = Scopes ?? Manifests,
            Tags = Tags,
            String1 = String1,
            String2 = String2,
            String3 = String3,
            Number1 = Number1,
            Number2 = Number2,
            Number3 = Number3,
        };
}
