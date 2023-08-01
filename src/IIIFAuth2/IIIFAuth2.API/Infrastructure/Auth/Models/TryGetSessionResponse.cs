using IIIFAuth2.API.Data.Entities;

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
    /// Cookie not found
    /// </summary>
    MissingCookie,
    
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