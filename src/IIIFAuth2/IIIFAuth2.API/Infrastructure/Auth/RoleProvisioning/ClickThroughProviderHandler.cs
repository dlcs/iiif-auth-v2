using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Models.Domain;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning;

/// <summary>
/// Handles RoleProvisioning for clickthrough configurations 
/// </summary>
public class ClickThroughProviderHandler 
{
    private readonly AuthServicesContext dbContext;
    private readonly ILogger<ClickThroughProviderHandler> logger;
    private readonly RoleProvisionGranter roleProvisionGranter;

    public ClickThroughProviderHandler(
        AuthServicesContext dbContext,
        RoleProvisionGranter roleProvisionGranter,
        ILogger<ClickThroughProviderHandler> logger)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.roleProvisionGranter = roleProvisionGranter;
    }

    /// <summary>
    /// Handle the role provisioning request - this will either request a significant gesture from the user or issue
    /// a cookie.
    /// </summary>
    public Task<HandleRoleProvisionResponse> CompleteRequest(
        int customerId,
        Uri requestOrigin,
        AccessService accessService,
        IProviderConfiguration providerConfiguration,
        CancellationToken cancellationToken = default)
    {
        return roleProvisionGranter.CompleteRequest(customerId, requestOrigin, providerConfiguration,
            () => GetRolesToBeGranted(customerId, accessService), cancellationToken);
    }

    private async Task<IReadOnlyCollection<string>> GetRolesToBeGranted(int customerId, AccessService accessService)
    {
        var customerRoles = await dbContext.Roles.GetCachedCustomerRecords(customerId, CacheKeys.Roles);
        var roles = customerRoles
            .Where(r => r.AccessServiceId == accessService.Id)
            .Select(r => r.Id)
            .ToList();

        if (roles.Count == 0)
        {
            logger.LogWarning("AccessService {CustomerId}:{AccessServiceName} grants no roles", customerId,
                accessService.Name);
        }
        return roles;
    }
}