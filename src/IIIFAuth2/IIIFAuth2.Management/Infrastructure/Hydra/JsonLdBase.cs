using System.Text.Json.Serialization;

namespace IIIFAuth2.Management.Infrastructure.Hydra;

/// <summary>
/// Base for JSON-LD resources. Minimal System.Text.Json port of Protagonist's Hydra JsonLdBase
/// </summary>
/// <remarks>
/// Type is set via constructor rather than overridden as STJ doesn't apply base attributes to overridden properties
/// </remarks>
public abstract class JsonLdBase(string type)
{
    public const string HydraContext = "http://www.w3.org/ns/hydra/context.jsonld";

    /// <summary>
    /// Set on top-level resources only, see <see cref="HydraController"/>
    /// </summary>
    [JsonPropertyName("@context")]
    [JsonPropertyOrder(-3)]
    public string? Context { get; set; }

    [JsonPropertyName("@id")]
    [JsonPropertyOrder(-2)]
    public string? Id { get; set; }

    [JsonPropertyName("@type")]
    [JsonPropertyOrder(-1)]
    public string Type { get; } = type;
}
