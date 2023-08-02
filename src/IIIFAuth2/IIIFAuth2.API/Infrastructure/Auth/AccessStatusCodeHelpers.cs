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
}