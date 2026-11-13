namespace IIIFAuth2.Core.Models.Domain;

public static class RoleX
{
    /// <summary>
    /// Prefix of final path segment of role URI that indicates role is granted by client IP address 
    /// </summary>
    public const string IpRolePrefix = "ip:";

    /// <summary>
    /// Prefix of final path segment of role URI that indicates role is granted by an appointment
    /// </summary>
    public const string AppointmentRolePrefix = "appointment:";

    /// <summary>
    /// Check if role URI is an IP role, i.e. the final path segment starts with "ip:"
    /// (e.g. https://api.dlcs.io/customers/2/roles/ip:reading-room)
    /// </summary>
    public static bool IsIpRole(this string? roleUri) => HasRolePrefix(roleUri, IpRolePrefix);

    /// <summary>
    /// Check if role URI is an appointment role, i.e. the final path segment starts with "appointment:"
    /// (e.g. https://api.dlcs.io/customers/2/roles/appointment:token-based)
    /// </summary>
    public static bool IsAppointmentRole(this string? roleUri) => HasRolePrefix(roleUri, AppointmentRolePrefix);

    private static bool HasRolePrefix(string? roleUri, string prefix)
    {
        if (string.IsNullOrWhiteSpace(roleUri)) return false;

        var trimmed = roleUri.AsSpan().TrimEnd('/');
        var lastSegment = trimmed[(trimmed.LastIndexOf('/') + 1)..];
        return lastSegment.StartsWith(prefix, StringComparison.Ordinal);
    }
}
