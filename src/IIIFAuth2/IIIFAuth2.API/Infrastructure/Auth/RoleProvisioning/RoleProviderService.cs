using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning;

/// <summary>
/// Service for managing requests to provision roles and create user sessions
/// </summary>
public class RoleProviderService
{
    private readonly AuthServicesContext dbContext;
    private readonly RoleProviderHandlerResolver handlerResolver;
    private readonly ILogger<AuthServicesContext> logger;

    public RoleProviderService(
        AuthServicesContext dbContext,
        RoleProviderHandlerResolver handlerResolver,
        ILogger<AuthServicesContext> logger)
    {
        this.dbContext = dbContext;
        this.handlerResolver = handlerResolver;
        this.logger = logger;
    }

    public async Task<HandleRoleProvisionResponse?> HandleRequest(int customerId, string accessServiceName,
        bool hostIsOrigin, Uri requestOrigin, CancellationToken cancellationToken = default)
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
        var handler = handlerResolver(providerConfiguration.Config);

        var result = await handler.HandleRequest(customerId, requestOrigin.ToString(), accessService,
            providerConfiguration, hostIsOrigin, cancellationToken);
        return result;
    }

    private async Task<AccessService?> GetAccessServices(int customerId, string accessServiceName)
    {
        var customerServices = await dbContext.AccessServices.GetCachedCustomerRecords(customerId, CacheKeys.AccessService);

        var accessService = customerServices
            .SingleOrDefault(s => s.Name.Equals(accessServiceName, StringComparison.OrdinalIgnoreCase));
        return accessService;
    }
}