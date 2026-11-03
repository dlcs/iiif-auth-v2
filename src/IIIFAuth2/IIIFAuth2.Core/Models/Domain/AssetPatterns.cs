namespace IIIFAuth2.Core.Models.Domain;

/// <summary>
/// Set of patterns that an asset must match. All non-empty fields must match (AND), a field matches if any of its
/// values match (OR). A null or empty field is not a constraint.
/// Scopes and tags match exactly. String1-3 match exactly, unless they end with "*", in which case they match anything
/// starting with the value before the "*" (e.g. "LIDDLE/*" matches "LIDDLE/WW2" but not "LIDDLE"). "*" anywhere else
/// is a literal character.
/// </summary>
public class AssetPatterns
{
    public int[]? Customer { get; set; }
    public int[]? Space { get; set; }
    public string[]? Scopes { get; set; }
    public string[]? Tags { get; set; }

    public string[]? String1 { get; set; }
    public string[]? String2 { get; set; }
    public string[]? String3 { get; set; }

    public int[]? Number1 { get; set; }
    public int[]? Number2 { get; set; }
    public int[]? Number3 { get; set; }

    /// <summary>
    /// Check if asset matches all patterns. <paramref name="customer"/> and <paramref name="space"/> come from the
    /// deliverable id. A constrained field with no corresponding asset value (including a null
    /// <paramref name="asset"/>) does not match.
    /// </summary>
    public bool Matches(int customer, int space, AssetMetadata? asset)
        => MatchesValue(Customer, customer)
           && MatchesValue(Space, space)
           && MatchesAny(Scopes, asset?.Scopes)
           && MatchesAny(Tags, asset?.Tags)
           && MatchesString(String1, asset?.String1)
           && MatchesString(String2, asset?.String2)
           && MatchesString(String3, asset?.String3)
           && MatchesValue(Number1, asset?.Number1)
           && MatchesValue(Number2, asset?.Number2)
           && MatchesValue(Number3, asset?.Number3);

    private static bool MatchesValue(int[]? pattern, int? value)
        => IsUnconstrained(pattern) || (value.HasValue && pattern!.Contains(value.Value));

    private static bool MatchesAny(string[]? pattern, string[]? values)
        => IsUnconstrained(pattern) ||
           (values != null && values.Any(v => pattern!.Contains(v, StringComparer.Ordinal)));

    private static bool MatchesString(string[]? pattern, string? value)
        => IsUnconstrained(pattern) || (value != null && pattern!.Any(p => IsStringMatch(p, value)));

    private static bool IsStringMatch(string pattern, string value)
        => pattern.EndsWith('*')
            ? value.StartsWith(pattern[..^1], StringComparison.Ordinal)
            : value.Equals(pattern, StringComparison.Ordinal);

    private static bool IsUnconstrained<T>(T[]? pattern) => pattern is not { Length: > 0 };
}
