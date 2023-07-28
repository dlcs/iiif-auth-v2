using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
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
    public async Task<string> CreateRoleProvisionToken(int customerId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        var time = DateTime.UtcNow;
        var token = new RoleProvisionToken
        {
            Id = ExpiringToken.GenerateNewToken(time),
            Created = time,
            Customer = customerId,
            Roles = roles.ToList(),
            Used = false,
        };

        await dbContext.RoleProvisionTokens.AddAsync(token, cancellationToken);
        await SaveChangesWithRowCountCheck("Create role-provision", cancellationToken: cancellationToken);
       
        return token.Id;
    }

    /// <summary>
    /// Create a new session for customer + roles. Creates DB record and issues cookie to response.
    /// </summary>
    public Task<SessionUser> CreateSessionForRoles(int customerId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
        => CreateSessionAndIssueCookie(customerId, roles, "Create session-user", 1, cancellationToken);

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
            var sessionUser = await CreateSessionAndIssueCookie(token.Customer, token.Roles,
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
    
    private async Task<SessionUser> CreateAndAddSessionUser(int customerId, IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        var sessionUser = new SessionUser
        {
            Created = DateTime.UtcNow,
            LastChecked = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddSeconds(authSettings.SessionTtl),
            AccessToken = Guid.NewGuid().ToString("N"),
            Roles = roles.ToList(),
            Customer = customerId,
            CookieId = Guid.NewGuid().ToString()
        };
        await dbContext.SessionUsers.AddAsync(sessionUser, cancellationToken);
        return sessionUser;
    }

    private async Task<SessionUser> CreateSessionAndIssueCookie(int customerId, IReadOnlyCollection<string> roles, string operation,
        int expectedRowCount, CancellationToken cancellationToken)
    {
        // TODO - handle user already having a session 
        var sessionUser = await CreateAndAddSessionUser(customerId, roles, cancellationToken);
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
}