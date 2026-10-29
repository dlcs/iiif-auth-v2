namespace IIIFAuth2.API.Infrastructure.Auth.Models;

/// <summary>
/// The result of evaluating requested roles against the client IP address
/// </summary>
/// <param name="IpRoles">Requested roles that are granted by an IpRestriction role-provider</param>
/// <param name="GrantedRoles">Requested roles that are granted to the current client IP address</param>
/// <param name="LocationPhrases">Location phrases of the IpRestriction role-providers for <see cref="IpRoles"/></param>
public record IpRoleEvaluation(
    IReadOnlySet<string> IpRoles,
    IReadOnlySet<string> GrantedRoles,
    IReadOnlyCollection<string> LocationPhrases)
{
    public static readonly IpRoleEvaluation Empty = new(new HashSet<string>(), new HashSet<string>(), []);

    /// <summary>
    /// Whether the client IP address is granted at least one of the requested roles
    /// </summary>
    public bool HasGrantedRole => GrantedRoles.Count > 0;

    /// <summary>
    /// Whether every one of specified roles is granted by an IpRestriction role-provider
    /// </summary>
    public bool AreAllIpRoles(IReadOnlyCollection<string> roles)
        => roles.Count > 0 && roles.All(IpRoles.Contains);
}
