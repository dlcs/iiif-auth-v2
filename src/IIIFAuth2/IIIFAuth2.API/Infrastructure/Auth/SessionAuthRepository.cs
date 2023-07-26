using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Settings;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.API.Infrastructure.Auth;

/// <summary>
/// Repository for operations involving session management
/// </summary>
public class SessionAuthRepository
{
    private readonly AuthServicesContext dbContext;
    private readonly ILogger<SessionAuthRepository> logger;
    private readonly AuthSettings authSettings;

    public SessionAuthRepository(
        AuthServicesContext dbContext,
        IOptions<AuthSettings> authSettings,
        ILogger<SessionAuthRepository> logger)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.authSettings = authSettings.Value;
    }

    public async Task<string> CreateToken(int customerId, string[] roles,
        CancellationToken cancellationToken)
    {
        var time = DateTime.UtcNow;
        var token = new RoleProvisionToken
        {
            Id = ExpiringToken.GenerateNewToken(time),
            Created = time,
            Customer = customerId,
            Roles = roles,
            Used = false,
        };

        await Commit(
            async () => await dbContext.RoleProvisionTokens.AddAsync(token, cancellationToken),
            "Create role-provision",
            cancellationToken: cancellationToken);
       
        return token.Id;
    }
    
    public async Task<SessionUser> CreateSessionForRoles(int customerId, string[] roles, CancellationToken cancellationToken)
    {
        // TODO - handle user already having a session 
        var sessionUser = new SessionUser
        {
            Created = DateTime.UtcNow,
            LastChecked = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddSeconds(authSettings.SessionTtl),
            AccessToken = Guid.NewGuid().ToString().Replace("-", string.Empty),
            Roles = roles,
            Customer = customerId,
            CookieId = Guid.NewGuid().ToString()
        };
        
        await Commit(
            async () => await dbContext.SessionUsers.AddAsync(sessionUser, cancellationToken),
            "Create session-user",
            cancellationToken: cancellationToken);
        
        return sessionUser;
    }

    private async Task Commit(Func<Task> databaseWork, string operation, int expectedCount = 1,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await databaseWork();
            var rows = await dbContext.SaveChangesAsync(cancellationToken);
            if (rows != expectedCount)
            {
                logger.LogError(
                    "Unexpected database rows written committing '{Message}', expected {ExpectedCount} but got {ActualCount}",
                    operation, expectedCount, rows);
                throw new ApplicationException($"Error committing {operation}");
            }

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error committing '{Message}'", operation);
            throw new ApplicationException($"Error committing {operation}");
        }
    }
}