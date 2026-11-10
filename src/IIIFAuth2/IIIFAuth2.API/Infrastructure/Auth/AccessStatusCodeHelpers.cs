using System.Net;
using IIIFAuth2.API.Infrastructure.Auth.Models;

namespace IIIFAuth2.API.Infrastructure.Auth;

internal static class AccessStatusCodeHelpers
{
    /// <summary>
    /// Get HttpStatusCode representing the result of access attempt for session in <see cref="TryGetSessionResponse"/>
    /// to access specified roles
    /// </summary>
    public static HttpStatusCode GetStatusCode(TryGetSessionResponse getSessionResponse, IEnumerable<string> roles)
    {
        if (!getSessionResponse.IsSuccessWithSession()) return HttpStatusCode.Unauthorized;
        
        return getSessionResponse.CanUserAccessAtLeastOneRole(roles) ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
    }

    /// <summary>
    /// Get HttpStatusCode representing the result of access attempt based on client IP address, as determined by
    /// <see cref="IpRoleEvaluation"/>. Returns null if client IP alone doesn't determine the result.
    /// </summary>
    /// <returns>
    /// 200 if client IP is granted any role, 403 if all roles are IP roles but none are granted, else null
    /// </returns>
    public static HttpStatusCode? GetIpStatusCode(IpRoleEvaluation ipRoleEvaluation, IReadOnlyCollection<string> roles)
    {
        if (ipRoleEvaluation.HasGrantedRole) return HttpStatusCode.OK;
        if (ipRoleEvaluation.AreAllIpRoles(roles)) return HttpStatusCode.Forbidden;
        return null;
    }
}