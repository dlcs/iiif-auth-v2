using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Auth.Models;
using Microsoft.EntityFrameworkCore;

namespace IIIFAuth2.API.Infrastructure.Auth;

/// <summary>
/// Base class for services that interact with UserSessions 
/// </summary>
public abstract class SessionManagerBase
{
    protected readonly AuthServicesContext DbContext;
    protected readonly AuthAspectManager AuthAspectManager;
    protected readonly ILogger Logger;

    protected SessionManagerBase(
        AuthServicesContext dbContext,
        AuthAspectManager authAspectManager,
        ILogger logger)
    {
        DbContext = dbContext;
        AuthAspectManager = authAspectManager;
        Logger = logger;
    }
    
    protected bool TryGetCookieId(int customerId, [NotNullWhen(true)] out string? cookieId,
        [NotNullWhen(false)] out GetSessionStatus? status)
    {
        var cookieValue = AuthAspectManager.GetCookieValueForCustomer(customerId);
        cookieId = null;
        status = null;
        if (string.IsNullOrEmpty(cookieValue))
        {
            Logger.LogDebug("Attempt to get cookie value for customer {CustomerId} but cookie not found", customerId);
            status = GetSessionStatus.MissingCredentials;
            return false;
        }

        cookieId = AuthAspectManager.GetCookieIdFromValue(cookieValue);
        if (string.IsNullOrEmpty(cookieId))
        {
            Logger.LogDebug("Id not found in cookie '{CookieValue}' for customer {CustomerId}",
                cookieValue, customerId);
            status = GetSessionStatus.InvalidCookie;
            return false;
        }
        
        return true;
    }
    
    protected async Task<TryGetSessionResponse> GetSessionUser(
        Expression<Func<SessionUser, bool>> predicate,
        int customerId,
        string aspectValue,
        string? origin = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await DbContext.SessionUsers.SingleOrDefaultAsync(predicate, cancellationToken);

            if (session == null)
            {
                Logger.LogInformation("UserSession for aspect '{AuthAspect}' not found for customer {CustomerId}",
                    aspectValue, customerId);
                return new TryGetSessionResponse(GetSessionStatus.MissingSession);
            }

            if (session.Expires <= DateTime.UtcNow)
            {
                Logger.LogTrace("UserSession for aspect '{AuthAspect}' for customer {Customer} expired", aspectValue,
                    customerId);
                return new TryGetSessionResponse(GetSessionStatus.ExpiredSession);
            }

            if (!string.IsNullOrEmpty(origin) && session.Origin != origin)
            {
                Logger.LogDebug(
                    "UserSession for aspect '{AuthAspect}' for customer {Customer} was for origin '{OriginalOrigin} but requested for '{NewOrigin}'",
                    aspectValue, customerId, session.Origin, origin);
                return new TryGetSessionResponse(GetSessionStatus.DifferentOrigin);
            }

            return new TryGetSessionResponse(GetSessionStatus.Success, session);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error getting refresh token");
            return new TryGetSessionResponse(GetSessionStatus.UnknownError);
        }
    }
}