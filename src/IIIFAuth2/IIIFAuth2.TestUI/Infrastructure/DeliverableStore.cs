using System.Collections.Concurrent;

namespace IIIFAuth2.TestUI.Infrastructure;

/// <summary>
/// A deliverable (Asset or Adjunct) and the roles it requires, as Orchestrator would have stored in DLCS db
/// </summary>
public record Deliverable(string Id, IReadOnlyList<string> Roles, DateTime Registered)
{
    public string RolesString => string.Join(",", Roles);

    /// <summary>
    /// Whether any role is an IP role, i.e. the final path segment starts with "ip:"
    /// </summary>
    /// <remarks>
    /// Mirrors RoleX.IsIpRole in IIIFAuth2.Core. Duplicated rather than referenced as Orchestrator, which this app
    /// stands in for, won't reference IIIFAuth2.Core
    /// </remarks>
    public bool HasIpRole => Roles.Any(IsIpRole);

    /// <summary>
    /// Customer id, from first segment of deliverable id. Null if not an int, as ids aren't validated
    /// </summary>
    public int? Customer => int.TryParse(Id.Split('/')[0], out var customer) ? customer : null;

    private static bool IsIpRole(string role)
    {
        var trimmed = role.AsSpan().TrimEnd('/');
        return trimmed[(trimmed.LastIndexOf('/') + 1)..].StartsWith("ip:", StringComparison.Ordinal);
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

    public Deliverable Register(string deliverableId, IEnumerable<string> roles)
    {
        var deliverable = new Deliverable(Normalise(deliverableId), roles.ToList(), DateTime.UtcNow);
        deliverables[deliverable.Id] = deliverable;
        return deliverable;
    }

    public bool TryGet(string deliverableId, out Deliverable deliverable)
        => deliverables.TryGetValue(Normalise(deliverableId), out deliverable!);

    public bool Remove(string deliverableId) => deliverables.TryRemove(Normalise(deliverableId), out _);

    public IReadOnlyCollection<Deliverable> All() => deliverables.Values.OrderByDescending(d => d.Registered).ToList();

    private static string Normalise(string deliverableId) => deliverableId.Trim().Trim('/');
}
