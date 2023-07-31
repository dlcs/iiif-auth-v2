using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Auth.Models;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Models.Result;
using IIIFAuth2.API.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.API.Infrastructure.Auth;

/// <summary>
/// Repository for operations involving session management
/// </summary>
public class SessionManagementService
{
    private readonly AuthServicesContext dbContext;
    private readonly AuthCookieManager authCookieManager;
    private readonly ILogger<SessionManagementService> logger;
    private readonly AuthSettings authSettings;

    public SessionManagementService(
        AuthServicesContext dbContext,
        AuthCookieManager authCookieManager,
        IOptions<AuthSettings> authSettings,
        ILogger<SessionManagementService> logger)
    {
        this.dbContext = dbContext;
        this.authCookieManager = authCookieManager;
        this.logger = logger;
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

        await dbContext.RoleProvisionTokens.AddAsync(token, cancellationToken);
        await SaveChangesWithRowCountCheck("Create role-provision", cancellationToken: cancellationToken);
       
        return token.Id;
    }

    /// <summary>
    /// Create a new session for customer + roles. Creates DB record and issues cookie to response.
    /// </summary>
    public Task<SessionUser> CreateSessionForRoles(int customerId, IReadOnlyCollection<string> roles, string origin, CancellationToken cancellationToken)
        => CreateSessionAndIssueCookie(customerId, roles, origin, "Create session-user", 1, cancellationToken);

    /// <summary>
    /// Attempt to create a new session using provided token. This can fail if token has been used, is expired or not
    /// known
    /// </summary>
    public async Task<ResultStatus<SessionUser>> TryCreateSessionFromToken(string roleProvisionToken,
        CancellationToken cancellationToken)
    {
        try
        {
            if (ExpiringToken.HasExpired(roleProvisionToken))
            {
                logger.LogInformation("Received an invalid or expired token: {Token}", roleProvisionToken);
                return ResultStatus<SessionUser>.Unsuccessful();
            }

            var token = await dbContext.RoleProvisionTokens.SingleOrDefaultAsync(r => r.Id == roleProvisionToken,
                cancellationToken);
            if (token == null)
            {
                logger.LogWarning("Received a valid, unexpired token that could not be found in db: {Token}",
                    roleProvisionToken);
                return ResultStatus<SessionUser>.Unsuccessful();
            }

            if (token.Used)
            {
                logger.LogDebug("Received a used token: {Token}", roleProvisionToken);
                return ResultStatus<SessionUser>.Unsuccessful();
            }

            token.Used = true;
            const int expectedRowCount = 2;
            var sessionUser = await CreateSessionAndIssueCookie(token.Customer, token.Roles, token.Origin,
                "Create session from token", expectedRowCount, cancellationToken);
            return ResultStatus<SessionUser>.Successful(sessionUser);
        }
        catch (DbUpdateConcurrencyException dbEx)
        {
            logger.LogError(dbEx, "Concurrency failure marking token {Token} as used", roleProvisionToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error creating session user from token {Token}", roleProvisionToken);
        }
        
        return ResultStatus<SessionUser>.Unsuccessful();
    }

    /// <summary>
    /// Attempt to load <see cref="SessionUser"/> for provided CookieId. If found it may have expiry extended in
    /// database and will issue cookie
    /// </summary>
    public async Task<TryGetSessionResponse> TryGetSessionUserForCookie(int customerId, string origin, CancellationToken cancellationToken)
    {
        var cookieValue = authCookieManager.GetCookieValueForCustomer(customerId);
        if (string.IsNullOrEmpty(cookieValue))
        {
            logger.LogDebug("Attempt to get cookie value for customer {CustomerId} but cookie not found", customerId);
            return new TryGetSessionResponse(GetSessionStatus.MissingCookie);
        }

        var cookieId = authCookieManager.GetCookieIdFromValue(cookieValue);
        if (string.IsNullOrEmpty(cookieId))
        {
            logger.LogDebug("Id not found in cookie '{CookieValue}' for customer {CustomerId}",
                cookieValue, customerId);
            return new TryGetSessionResponse(GetSessionStatus.InvalidCookie);
        }

        var findSessionResponse = await GetRefreshedSession(customerId, cookieId, origin, cancellationToken);
        if (findSessionResponse.Status != GetSessionStatus.Success) return findSessionResponse;
        
        // Re-issue the cookie to extend ttl
        authCookieManager.IssueCookie(findSessionResponse.SessionUser!);
        return findSessionResponse;
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
        await dbContext.SessionUsers.AddAsync(sessionUser, cancellationToken);
        return sessionUser;
    }

    private async Task<SessionUser> CreateSessionAndIssueCookie(int customerId, IReadOnlyCollection<string> roles,
        string origin, string operation, int expectedRowCount, CancellationToken cancellationToken)
    {
        // TODO - handle user already having a session 
        var sessionUser = await CreateAndAddSessionUser(customerId, roles, origin, cancellationToken);
        await SaveChangesWithRowCountCheck(operation, expectedRowCount, cancellationToken: cancellationToken);

        authCookieManager.IssueCookie(sessionUser);
        return sessionUser;
    }

    private async Task SaveChangesWithRowCountCheck(string operation, int expectedCount = 1,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.SaveChangesAsync(cancellationToken);
        if (rows != expectedCount)
        {
            logger.LogError(
                "Unexpected database rows written committing '{Message}', expected {ExpectedCount} but got {ActualCount}",
                operation, expectedCount, rows);
            throw new ApplicationException($"Error committing {operation}");
        }
    }

    private async Task<TryGetSessionResponse> GetRefreshedSession(int customerId, string cookieId, string origin,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await dbContext.SessionUsers
                .SingleOrDefaultAsync(c => c.CookieId == cookieId && c.Customer == customerId, cancellationToken);

            if (session == null)
            {
                logger.LogInformation("UserSession for CookieId '{CookieId}' not found for customer {CustomerId}",
                    cookieId, customerId);
                return new TryGetSessionResponse(GetSessionStatus.MissingSession);
            }

            if (session.Expires <= DateTime.UtcNow)
            {
                logger.LogTrace("UserSession for CookieId '{CookieId}' for customer {Customer} expired", cookieId,
                    customerId);
                return new TryGetSessionResponse(GetSessionStatus.ExpiredSession);
            }

            if (session.Origin != origin)
            {
                logger.LogDebug(
                    "UserSession for CookieId '{CookieId}' for customer {Customer} was for origin '{OriginalOrigin} but requested for '{NewOrigin}'",
                    cookieId, customerId, session.Origin, origin);
                return new TryGetSessionResponse(GetSessionStatus.DifferentOrigin);
            }

            // Token has never been checked, or was last checked in the past and threshold has passed
            if (!session.LastChecked.HasValue ||
                session.LastChecked.Value.AddSeconds(authSettings.RefreshThreshold) < DateTime.UtcNow)
            {
                logger.LogDebug("Extending session {SessionId}", session.Id);
                session.LastChecked = DateTime.UtcNow;
                session.Expires = DateTime.UtcNow.AddSeconds(authSettings.SessionTtl);
                await SaveChangesWithRowCountCheck("Extend user session", cancellationToken: cancellationToken);
            }

            return new TryGetSessionResponse(GetSessionStatus.Success, session);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error getting refresh token");
            return new TryGetSessionResponse(GetSessionStatus.UnknownError);
        }
    } 
}