using System.Linq.Expressions;
using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Auth.Models;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Models.Result;
using IIIFAuth2.API.Settings;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.API.Infrastructure.Auth;

/// <summary>
/// Service for operations involving session management (creation and validation)
/// </summary>
public class SessionManagementService : SessionManagerBase
{
    private readonly IAppCache appCache;
    private readonly AuthSettings authSettings;

    public SessionManagementService(
        AuthServicesContext dbContext,
        AuthAspectManager authAspectManager,
        IAppCache appCache,
        IOptions<AuthSettings> authSettings,
        ILogger<SessionManagementService> logger): base(dbContext, authAspectManager, logger)
    {
        this.appCache = appCache;
        this.authSettings = authSettings.Value;
    }

    /// <summary>
    /// Create a new RoleProvisionToken for specified roles + customer
    /// </summary>
    public async Task<string> CreateRoleProvisionToken(int customerId, IReadOnlyCollection<string> roles, string origin,
        CancellationToken cancellationToken)
    {
        var time = DateTime.UtcNow;
        var token = new RoleProvisionToken
        {
            Id = ExpiringToken.GenerateNewToken(time),
            Created = time,
            Customer = customerId,
            Roles = roles.ToList(),
            Used = false,
            Origin = origin
        };

        await DbContext.RoleProvisionTokens.AddAsync(token, cancellationToken);
        await SaveChangesWithRowCountCheck("Create role-provision", cancellationToken: cancellationToken);
       
        return token.Id;
    }

    /// <summary>
    /// Create a new session for customer + roles. Creates DB record and issues cookie to response.
    /// </summary>
    public Task<SessionUser> CreateSessionForRoles(int customerId, IReadOnlyCollection<string> roles, string origin, CancellationToken cancellationToken)
        => CreateSessionAndIssueCookie(customerId, roles, origin, "Create session-user", 1, cancellationToken);

    /// <summary>
    /// Validate the provided roleProvisionToken - this will validate whether it has timed out or been used.
    /// </summary>
    public async Task<ResultStatus<RoleProvisionToken>> TryGetValidToken(string roleProvisionToken,
        CancellationToken cancellationToken)
    {
        try
        {
            const int tokenValidForSecs = 900;
            var token =
                await TryGetValidUnusedRoleProvisionToken(roleProvisionToken, tokenValidForSecs, cancellationToken);
            if (token == null) return ResultStatus<RoleProvisionToken>.Unsuccessful();

            await SaveChangesWithRowCountCheck("Mark token as used", cancellationToken: cancellationToken);
            Logger.LogDebug("Successfully marked token as used: {Token}", roleProvisionToken);
            return ResultStatus<RoleProvisionToken>.Successful(token);
        }
        catch (DbUpdateConcurrencyException dbEx)
        {
            Logger.LogError(dbEx, "Concurrency failure marking token {Token} as used", roleProvisionToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error creating session user from token {Token}", roleProvisionToken);
        }

        return ResultStatus<RoleProvisionToken>.Unsuccessful();
    }

    /// <summary>
    /// Attempt to create a new session using provided token. This can fail if token has been used, is expired or not
    /// known
    /// </summary>
    public async Task<ResultStatus<SessionUser>> TryCreateSessionFromToken(string roleProvisionToken,
        CancellationToken cancellationToken)
    {
        try
        {
            var token =
                await TryGetValidUnusedRoleProvisionToken(roleProvisionToken, cancellationToken: cancellationToken);
            if (token == null) return ResultStatus<SessionUser>.Unsuccessful();

            const int expectedRowCount = 2;
            var sessionUser = await CreateSessionAndIssueCookie(token.Customer, token.Roles, token.Origin,
                "Create session from token", expectedRowCount, cancellationToken);
            Logger.LogDebug("Successfully created Session for token: {Token}", roleProvisionToken);
            return ResultStatus<SessionUser>.Successful(sessionUser);
        }
        catch (DbUpdateConcurrencyException dbEx)
        {
            Logger.LogError(dbEx, "Concurrency failure marking token {Token} as used", roleProvisionToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error creating session user from token {Token}", roleProvisionToken);
        }
        
        return ResultStatus<SessionUser>.Unsuccessful();
    }

    /// <summary>
    /// Attempt to load <see cref="SessionUser"/> for provided CookieId. If found it may have expiry extended in
    /// database and will issue cookie
    /// </summary>
    public async Task<TryGetSessionResponse> TryGetSessionUserForCookie(int customerId, string? origin, CancellationToken cancellationToken)
    {
        if (!TryGetCookieId(customerId, out var cookieId, out var status))
            return new TryGetSessionResponse(status.Value);

        var findSessionResponse = await GetRefreshedSession(
            su => su.CookieId == cookieId && su.Customer == customerId,
            customerId,
            cookieId,
            origin,
            cancellationToken);

        return findSessionResponse;
    }

    /// <summary>
    /// Attempt to load <see cref="SessionUser"/> for provided access-token. If found it may have expiry extended in
    /// database and will issue cookie
    /// </summary>
    public async Task<TryGetSessionResponse> TryGetSessionUserForAccessToken(int customerId,
        CancellationToken cancellationToken)
    {
        var accessToken = AuthAspectManager.GetAccessToken();
        if (string.IsNullOrEmpty(accessToken))
        {
            Logger.LogDebug("Attempt to get session for customer {CustomerId} access-token but none found", customerId);
            return new TryGetSessionResponse(GetSessionStatus.MissingCredentials);
        }

        var findSessionResponse = await GetRefreshedSession(
            su => su.AccessToken == accessToken && su.Customer == customerId,
            customerId,
            accessToken,
            cancellationToken: cancellationToken);

        return findSessionResponse;
    }
    
    /// <summary>
    /// Try and get <see cref="RoleProvisionToken"/> with specified Id. Value only returned if token hasn't expired and
    /// is unused.
    /// Returned token will have been marked as used but not saved to backing store.
    /// </summary>
    private async Task<RoleProvisionToken?> TryGetValidUnusedRoleProvisionToken(string roleProvisionToken,
        int validForSecs = 300, CancellationToken cancellationToken = default)
    {
        if (ExpiringToken.HasExpired(roleProvisionToken, validForSecs))
        {
            Logger.LogInformation("Received an invalid or expired token: {Token}", roleProvisionToken);
            return null;
        }

        var token = await DbContext.RoleProvisionTokens.SingleOrDefaultAsync(r => r.Id == roleProvisionToken,
            cancellationToken);
        if (token == null)
        {
            Logger.LogWarning("Received a valid, unexpired token that could not be found in db: {Token}",
                roleProvisionToken);
            return null;
        }

        if (token.Used)
        {
            Logger.LogDebug("Received a used token: {Token}", roleProvisionToken);
            return null;
        }

        token.Used = true;
        return token;
    }

    private async Task<SessionUser> CreateAndAddSessionUser(int customerId, IReadOnlyCollection<string> roles, 
        string origin, CancellationToken cancellationToken)
    {
        var sessionUser = new SessionUser
        {
            Created = DateTime.UtcNow,
            LastChecked = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddSeconds(authSettings.SessionTtl),
            AccessToken = Guid.NewGuid().ToString("N"),
            Roles = roles.ToList(),
            Customer = customerId,
            CookieId = Guid.NewGuid().ToString(),
            Origin = origin
        };
        await DbContext.SessionUsers.AddAsync(sessionUser, cancellationToken);
        return sessionUser;
    }

    private async Task<SessionUser> CreateSessionAndIssueCookie(int customerId, IReadOnlyCollection<string> roles,
        string origin, string operation, int expectedRowCount, CancellationToken cancellationToken)
    {
        // TODO - handle user already having a session 
        var sessionUser = await CreateAndAddSessionUser(customerId, roles, origin, cancellationToken);
        await SaveChangesWithRowCountCheck(operation, expectedRowCount, cancellationToken: cancellationToken);

        await AuthAspectManager.IssueCookie(sessionUser);
        return sessionUser;
    }

    private async Task SaveChangesWithRowCountCheck(string operation, int expectedCount = 1,
        CancellationToken cancellationToken = default)
    {
        var rows = await DbContext.SaveChangesAsync(cancellationToken);
        if (rows != expectedCount)
        {
            Logger.LogError(
                "Unexpected database rows written committing '{Message}', expected {ExpectedCount} but got {ActualCount}",
                operation, expectedCount, rows);
            throw new ApplicationException($"Error committing {operation}");
        }
    }
    
    private async Task<TryGetSessionResponse> GetRefreshedSession(
        Expression<Func<SessionUser, bool>> predicate,
        int customerId,
        string aspectValue,
        string? origin = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.AuthAspect(aspectValue, origin);
        return await appCache.GetOrAddAsync(cacheKey, async entry =>
        {
            Logger.LogTrace("Refreshing cached session for {AuthAspect} for {CustomerId}", aspectValue, customerId);
            var tryGetSessionResponse =
                await GetRefreshedSessionInternal(predicate, customerId, aspectValue, origin, cancellationToken);

            if (tryGetSessionResponse.IsSuccessWithSession())
            {
                // Cache successful checks until just before next check time - overwriting default
                var lastChecked = tryGetSessionResponse.SessionUser!.LastChecked ?? DateTime.UtcNow;
                var entryAbsoluteExpiration = lastChecked.AddSeconds(authSettings.RefreshThreshold * 0.9);
                Logger.LogTrace("{AuthAspect} for {CustomerId} successfully fetched, caching until {CacheExpiry}",
                    aspectValue, customerId, entryAbsoluteExpiration);
                entry.AbsoluteExpiration = entryAbsoluteExpiration;
            }

            return tryGetSessionResponse;

        }, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(2) });
    }

    private async Task<TryGetSessionResponse> GetRefreshedSessionInternal(
        Expression<Func<SessionUser, bool>> predicate,
        int customerId,
        string aspectValue,
        string? origin = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tryGetSessionResponse = await GetSessionUser(predicate, customerId, aspectValue, origin, cancellationToken);

            if (!tryGetSessionResponse.IsSuccessWithSession()) return tryGetSessionResponse;

            var session = tryGetSessionResponse.SessionUser!;
            
            // Token has never been checked, or was last checked in the past and threshold has passed
            if (!session.LastChecked.HasValue ||
                session.LastChecked.Value.AddSeconds(authSettings.RefreshThreshold) < DateTime.UtcNow)
            {
                Logger.LogDebug("Extending session {SessionId}", session.Id);
                session.LastChecked = DateTime.UtcNow;
                session.Expires = DateTime.UtcNow.AddSeconds(authSettings.SessionTtl);
                await SaveChangesWithRowCountCheck("Extend user session", cancellationToken: cancellationToken);
            }

            // Re-issue the cookie to extend ttl
            await AuthAspectManager.IssueCookie(session);
            return new TryGetSessionResponse(GetSessionStatus.Success, session);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error getting refresh token");
            return new TryGetSessionResponse(GetSessionStatus.UnknownError);
        }
    }
}