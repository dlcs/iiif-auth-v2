namespace IIIFAuth2.Core.Models.Domain;

public static class RoleX
{
    /// <summary>
    /// Prefix of final path segment of role URI that indicates role is granted by client IP address 
    /// </summary>
    public const string IpRolePrefix = "ip:";

    /// <summary>
    /// Check if role URI is an IP role, i.e. the final path segment starts with "ip:"
    /// (e.g. https://api.dlcs.io/customers/2/roles/ip:reading-room)
    /// </summary>
    public static bool IsIpRole(this string? roleUri)
    {
        if (string.IsNullOrWhiteSpace(roleUri)) return false;

        var trimmed = roleUri.TrimEnd('/');
        var lastSegment = trimmed[(trimmed.LastIndexOf('/') + 1)..];
        return lastSegment.StartsWith(IpRolePrefix, StringComparison.Ordinal);
    }
}
