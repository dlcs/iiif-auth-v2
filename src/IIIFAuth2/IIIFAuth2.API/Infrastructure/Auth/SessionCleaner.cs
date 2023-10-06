using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Auth.Models;
using LazyCache;

namespace IIIFAuth2.API.Infrastructure.Auth;

/// <summary>
/// Service for handling logging user out and cleaning up session
/// </summary>
public class SessionCleaner : SessionManagerBase
{
    private readonly IAppCache appCache;

    public SessionCleaner(
        AuthServicesContext dbContext,
        AuthAspectManager authAspectManager,
        IAppCache appCache,
        ILogger<SessionManagementService> logger) : base(dbContext, authAspectManager, logger)
    {
        this.appCache = appCache;
    }
    
    public async Task<bool> LogoutUser(int customerId, CancellationToken cancellationToken)
    {
        var sessionUser = await GetExistingSession(customerId, cancellationToken);
        if (sessionUser == null) return false;
        
        Logger.LogDebug("Logging out session {SessionUserId} for {CustomerId}", sessionUser.Id, customerId);

        var saveSuccess = await ExpireSessionInDatabase(cancellationToken, sessionUser);
        
        InvalidateCache(sessionUser);

        await AuthAspectManager.RemoveCookieFromResponse(sessionUser.Customer);
        return saveSuccess;
    }

    private async Task<SessionUser?> GetExistingSession(int customerId, CancellationToken cancellationToken)
    {
        if (!TryGetCookieId(customerId, out var cookieId, out _)) return null;
        
        var findSessionResponse = await GetSessionUser(
            su => su.CookieId == cookieId && su.Customer == customerId,
            customerId,
            cookieId,
            null,
            cancellationToken);

        return findSessionResponse.IsSuccessWithSession() ? findSessionResponse.SessionUser : null;
    } 
    
    private async Task<bool> ExpireSessionInDatabase(CancellationToken cancellationToken, SessionUser sessionUser)
    {
        try
        {
            sessionUser.LastChecked = DateTime.UtcNow;
            sessionUser.Expires = DateTime.UtcNow.AddSeconds(-10);
            await DbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving expired SessionUser object {SessionUserId} for {CustomerId}",
                sessionUser.Id, sessionUser.Customer);
            return false;
        }
    }
    
    private void InvalidateCache(SessionUser sessionUser)
    {
        try
        {
            appCache.Remove(CacheKeys.AuthAspect(sessionUser.CookieId, sessionUser.Origin));
            appCache.Remove(CacheKeys.AuthAspect(sessionUser.CookieId));
            appCache.Remove(CacheKeys.AuthAspect(sessionUser.AccessToken, sessionUser.Origin));
            appCache.Remove(CacheKeys.AuthAspect(sessionUser.AccessToken));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Error invalidating caches for expired SessionUser object {SessionUserId} for {CustomerId}",
                sessionUser.Id, sessionUser.Customer);
        }
    }
}