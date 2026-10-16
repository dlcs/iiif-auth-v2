using System.Collections.Concurrent;

namespace IIIFAuth2.TestUI.Infrastructure;

/// <summary>
/// A deliverable (Asset or Adjunct) and the roles it requires, as Orchestrator would have stored in DLCS db
/// </summary>
public record Deliverable(string Id, IReadOnlyList<string> Roles, DateTime Registered)
{
    public string RolesString => string.Join(",", Roles);
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
