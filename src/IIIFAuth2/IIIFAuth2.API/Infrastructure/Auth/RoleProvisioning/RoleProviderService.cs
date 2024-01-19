using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Oidc;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Utils;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning;

/// <summary>
/// Service for managing requests to provision roles and create user sessions
/// </summary>
public class RoleProviderService
{
    private readonly AuthServicesContext dbContext;
    private readonly ClickThroughProviderHandler clickthroughRoleHandler;
    private readonly OidcRoleProviderHandler oidcRoleProviderHandler;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ILogger<RoleProviderService> logger;

    public RoleProviderService(
        AuthServicesContext dbContext,
        ClickThroughProviderHandler clickthroughRoleHandler,
        OidcRoleProviderHandler oidcRoleProviderHandler,
        IHttpContextAccessor httpContextAccessor,
        ILogger<RoleProviderService> logger)
    {
        this.dbContext = dbContext;
        this.clickthroughRoleHandler = clickthroughRoleHandler;
        this.oidcRoleProviderHandler = oidcRoleProviderHandler;
        this.httpContextAccessor = httpContextAccessor;
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
        
        var providerConfiguration = GetProviderConfigurationForHost(accessService);
        if (providerConfiguration == null) return null;
        
        switch (providerConfiguration.Config)
        {
            case RoleProviderType.Clickthrough:
            {
                // By the time access-service is accessed the user has agreed to terms so we can complete request
                var clickthroughResult = await clickthroughRoleHandler.CompleteRequest(customerId, requestOrigin,
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

    public async Task<HandleRoleProvisionResponse?> HandleOidcCallback(int customerId, string accessServiceName,
        string roleProvisionToken, string authCode, CancellationToken cancellationToken = default)
    {
        var accessService = await GetAccessServices(customerId, accessServiceName);
        if (accessService == null) return null;

        var providerConfiguration = GetProviderConfigurationForHost(accessService);
        if (providerConfiguration == null) return null;
        
        var result = await oidcRoleProviderHandler.HandleLoginCallback(customerId, roleProvisionToken, authCode,
            accessService, providerConfiguration, cancellationToken);
        return result;
    }

    private async Task<AccessService?> GetAccessServices(int customerId, string accessServiceName)
    {
        var customerServices = await dbContext.AccessServices.GetCachedCustomerRecords(customerId, CacheKeys.AccessService);

        var accessService = customerServices
            .SingleOrDefault(s => s.Name.Equals(accessServiceName, StringComparison.OrdinalIgnoreCase));
        return accessService;
    }

    private IProviderConfiguration? GetProviderConfigurationForHost(AccessService accessService)
    {
        var roleProvider = accessService.RoleProvider;
        if (roleProvider == null)
        {
            logger.LogWarning(
                "AccessService '{AccessServiceId}' ({CustomerId}:{AccessServiceName}) has no RoleProvider",
                accessService.Id, accessService.Customer, accessService.Name);
            return null;
        }
        
        var currentRequest = httpContextAccessor.SafeHttpContext().Request;
        var host = currentRequest.Host.Value;
        logger.LogTrace("Getting provider configuration for host {Host}", host);
        
        var providerConfiguration = roleProvider.Configuration.GetConfiguration(host);
        return providerConfiguration;
    }
}