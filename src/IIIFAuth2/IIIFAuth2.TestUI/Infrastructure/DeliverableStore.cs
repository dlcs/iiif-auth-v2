using System.Collections.Concurrent;

namespace IIIFAuth2.TestUI.Infrastructure;

/// <summary>
/// Asset metadata Orchestrator sends to iiif-auth-v2 for evaluating appointment asset patterns. Space comes from the
/// deliverable id. Scopes are Protagonist "manifests"
/// </summary>
public record DeliverableAsset(
    string[]? Scopes = null,
    string[]? Tags = null,
    string? String1 = null,
    string? String2 = null,
    string? String3 = null,
    int? Number1 = null,
    int? Number2 = null,
    int? Number3 = null)
{
    public static readonly DeliverableAsset Empty = new();

    public bool IsEmpty => Scopes is not { Length: > 0 } && Tags is not { Length: > 0 } && String1 == null &&
                           String2 == null && String3 == null && Number1 == null && Number2 == null &&
                           Number3 == null;

    public override string ToString()
    {
        var parts = new List<string>();
        if (Scopes is { Length: > 0 }) parts.Add($"scopes: {string.Join(", ", Scopes)}");
        if (Tags is { Length: > 0 }) parts.Add($"tags: {string.Join(", ", Tags)}");
        if (String1 != null) parts.Add($"string1: {String1}");
        if (String2 != null) parts.Add($"string2: {String2}");
        if (String3 != null) parts.Add($"string3: {String3}");
        if (Number1 != null) parts.Add($"number1: {Number1}");
        if (Number2 != null) parts.Add($"number2: {Number2}");
        if (Number3 != null) parts.Add($"number3: {Number3}");
        return parts.Count == 0 ? "none" : string.Join(" | ", parts);
    }
}

/// <summary>
/// A deliverable (Asset or Adjunct) and the roles it requires, as Orchestrator would have stored in DLCS db
/// </summary>
public record Deliverable(string Id, IReadOnlyList<string> Roles, DateTime Registered, DeliverableAsset Asset)
{
    public string RolesString => string.Join(",", Roles);

    /// <summary>
    /// Whether any role is an IP role, i.e. the final path segment starts with "ip:"
    /// </summary>
    /// <remarks>
    /// Mirrors RoleX.IsIpRole in IIIFAuth2.Core. Duplicated rather than referenced as Orchestrator, which this app
    /// stands in for, won't reference IIIFAuth2.Core
    /// </remarks>
    public bool HasIpRole => Roles.Any(r => HasRolePrefix(r, "ip:"));

    /// <summary>
    /// Whether any role is an appointment role, i.e. the final path segment starts with "appointment:". Asset
    /// metadata is only sent to iiif-auth-v2 if so
    /// </summary>
    /// <remarks>Mirrors RoleX.IsAppointmentRole in IIIFAuth2.Core, duplicated as for <see cref="HasIpRole"/></remarks>
    public bool HasAppointmentRole => Roles.Any(r => HasRolePrefix(r, "appointment:"));

    /// <summary>
    /// Customer id, from first segment of deliverable id. Null if not an int, as ids aren't validated
    /// </summary>
    public int? Customer => int.TryParse(Id.Split('/')[0], out var customer) ? customer : null;

    private static bool HasRolePrefix(string role, string prefix)
    {
        var trimmed = role.AsSpan().TrimEnd('/');
        return trimmed[(trimmed.LastIndexOf('/') + 1)..].StartsWith(prefix, StringComparison.Ordinal);
    }
}

/// <summary>
/// In-memory store of deliverables. Stands in for Orchestrator looking up Asset/Adjunct roles. Contents are lost on
/// restart.
/// </summary>
/// <remarks>
/// Deliverable ids are not validated - this allows invalid ids to be passed to iiif-auth-v2 to test handling.
/// </remarks>
public class DeliverableStore
{
    private readonly ConcurrentDictionary<string, Deliverable> deliverables = new(StringComparer.OrdinalIgnoreCase);

    public Deliverable Register(string deliverableId, IEnumerable<string> roles, DeliverableAsset? asset = null)
    {
        var deliverable = new Deliverable(Normalise(deliverableId), roles.ToList(), DateTime.UtcNow,
            asset ?? DeliverableAsset.Empty);
        deliverables[deliverable.Id] = deliverable;
        return deliverable;
    }

    public bool TryGet(string deliverableId, out Deliverable deliverable)
        => deliverables.TryGetValue(Normalise(deliverableId), out deliverable!);

    public bool Remove(string deliverableId) => deliverables.TryRemove(Normalise(deliverableId), out _);

    public IReadOnlyCollection<Deliverable> All() => deliverables.Values.OrderByDescending(d => d.Registered).ToList();

    private static string Normalise(string deliverableId) => deliverableId.Trim().Trim('/');
}
