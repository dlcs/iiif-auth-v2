using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Oidc;
using IIIFAuth2.API.Models.Domain;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning;

/// <summary>
/// Service for managing requests to provision roles and create user sessions
/// </summary>
public class RoleProviderService
{
    private readonly AuthServicesContext dbContext;
    private readonly RoleProvisioner roleProvisioner;
    private readonly OidcRoleProviderHandler oidcRoleProviderHandler;
    private readonly ILogger<RoleProviderService> logger;

    public RoleProviderService(
        AuthServicesContext dbContext,
        RoleProvisioner roleProvisioner,
        OidcRoleProviderHandler oidcRoleProviderHandler,
        ILogger<RoleProviderService> logger)
    {
        this.dbContext = dbContext;
        this.roleProvisioner = roleProvisioner;
        this.oidcRoleProviderHandler = oidcRoleProviderHandler;
        this.logger = logger;
    }

    /// <summary>
    /// Handle 'initial' role-provision request, which is when a user requests the access-service for the first time.
    /// </summary>
    public async Task<HandleRoleProvisionResponse?> HandleInitialRequest(int customerId, string accessServiceName,
        Uri requestOrigin, CancellationToken cancellationToken = default)
    {
        var accessService = await GetAccessServices(customerId, accessServiceName);
        if (accessService == null) return null;
        
        var roleProvider = accessService.RoleProvider;
        if (roleProvider == null)
        {
            logger.LogWarning(
                "AccessService '{AccessServiceId}' ({CustomerId}:{AccessServiceName}) has no RoleProvider",
                accessService.Id, customerId, accessService.Name);
            return null;
        }
        
        // TODO - does this need to be smarter? How does it look up the key? Hostname?
        var providerConfiguration = roleProvider.Configuration.GetDefaultConfiguration();
        switch (providerConfiguration.Config)
        {
            case RoleProviderType.Clickthrough:
            {
                // By the time access-service is accessed the user has agreed to terms so we can complete request
                var clickthroughResult = await roleProvisioner.CompleteRequest(customerId, requestOrigin,
                    accessService, providerConfiguration, cancellationToken);
                return clickthroughResult;
            }
            case RoleProviderType.Oidc:
            {
                // For OIDC the initial request needs to head off to identity provider first
                var oidcResult = await oidcRoleProviderHandler.InitiateLoginRequest(customerId, requestOrigin,
                    accessService, providerConfiguration, cancellationToken);
                return oidcResult;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(providerConfiguration.Config),
                    $"Role provider configuration type {providerConfiguration.Config} unknown");
        }
    }

    private async Task<AccessService?> GetAccessServices(int customerId, string accessServiceName)
    {
        var customerServices = await dbContext.AccessServices.GetCachedCustomerRecords(customerId, CacheKeys.AccessService);

        var accessService = customerServices
            .SingleOrDefault(s => s.Name.Equals(accessServiceName, StringComparison.OrdinalIgnoreCase));
        return accessService;
    }
}