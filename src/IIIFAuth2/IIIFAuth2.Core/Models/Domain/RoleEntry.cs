namespace IIIFAuth2.Core.Models.Domain;

/// <summary>
/// An entry in a deliverable's roles. Access requires every role in the entry (AND); a deliverable's entries are
/// alternatives, any one of which grants access (OR)
/// </summary>
/// <remarks>
/// Text form is roles joined with "+", e.g. "ip:reading-room+staff", and entries joined with ",". Whitespace is also
/// treated as "+" as an unencoded "+" in a query string decodes to a space. Role URIs never contain whitespace
/// </remarks>
public record RoleEntry(IReadOnlyList<string> Roles)
{
    private static readonly char[] RoleSeparators = ['+', ' ', '\t', '\r', '\n'];

    /// <summary>
    /// Parse a single entry, e.g. "a+b"
    /// </summary>
    public static RoleEntry Parse(string entry)
        => new(entry.Split(RoleSeparators, StringSplitOptions.RemoveEmptyEntries).Distinct().ToList());

    /// <summary>
    /// Parse comma-delimited entries, e.g. "a,b+c". Empty entries are dropped
    /// </summary>
    public static IReadOnlyList<RoleEntry> ParseList(string? entries)
        => (entries ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(Parse)
            .Where(e => e.Roles.Count > 0)
            .ToList();

    public override string ToString() => string.Join('+', Roles);

    public virtual bool Equals(RoleEntry? other) => other != null && Roles.SequenceEqual(other.Roles);

    public override int GetHashCode() => Roles.Aggregate(0, (hash, role) => HashCode.Combine(hash, role));
}

public static class RoleEntryX
{
    /// <summary>
    /// Distinct roles across all entries
    /// </summary>
    public static IReadOnlyCollection<string> AllRoles(this IEnumerable<RoleEntry> entries)
        => entries.SelectMany(e => e.Roles).Distinct().ToList();
}
