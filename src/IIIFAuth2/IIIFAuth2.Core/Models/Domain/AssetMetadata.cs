namespace IIIFAuth2.Core.Models.Domain;

/// <summary>
/// Metadata of the asset being accessed, used to evaluate <see cref="AssetPatterns"/>. Customer and space are not
/// included as they come from the deliverable id.
/// </summary>
public record AssetMetadata
{
    public string[]? Scopes { get; init; }
    public string[]? Tags { get; init; }
    public string? String1 { get; init; }
    public string? String2 { get; init; }
    public string? String3 { get; init; }
    public int? Number1 { get; init; }
    public int? Number2 { get; init; }
    public int? Number3 { get; init; }
}
