using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Utils;

namespace IIIFAuth2.API.Infrastructure.Auth.Models;

/// <summary>
/// Represents the result of call to exchange cookie or access-token for a session user
/// </summary>
public record TryGetSessionResponse(GetSessionStatus Status, SessionUser? SessionUser = null);

public static class TryGetSessionResponseX
{
    /// <summary>
    /// Check if status is 'Success' and SessionUser is present
    /// </summary>
    public static bool IsSuccessWithSession(this TryGetSessionResponse response)
        => response.Status == GetSessionStatus.Success && response.SessionUser != null;

    /// <summary>
    /// Check if current Session has access to at least one of the specified roles
    /// </summary>
    public static bool CanUserAccessAtLeastOneRole(this TryGetSessionResponse response,
        IEnumerable<string> roles)
    {
        if (!response.IsSuccessWithSession()) return false;
        
        var sessionUserRoles = response.SessionUser?.Roles;
        return !sessionUserRoles.IsNullOrEmpty() && sessionUserRoles.Intersect(roles).Any();
    }
}

public enum GetSessionStatus
{
    /// <summary>
    /// Requested session not found
    /// </summary>
    MissingSession,
    
    /// <summary>
    /// Origin provided differs from that in DB
    /// </summary>
    DifferentOrigin,
    
    /// <summary>
    /// Cookie or AccessToken not found
    /// </summary>
    MissingCredentials,
    
    /// <summary>
    /// Cookie found but invalid
    /// </summary>
    InvalidCookie,
    
    /// <summary>
    /// User session found but it has expired
    /// </summary>
    ExpiredSession,
    
    /// <summary>
    /// Any other error
    /// </summary>
    UnknownError,
    
    /// <summary>
    /// Success
    /// </summary>
    Success
}