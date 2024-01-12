using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Auth.Models;
using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Settings;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning;

/// <summary>
/// Class used to handle provisioning a token OR initiating a request for a significant gesture. This happens when the
/// user has provided enough information that we are satisfied that have relevant access required for the provided
/// accessService 
/// </summary>
public class RoleProvisioner 
{
    private readonly AuthServicesContext dbContext;
    private readonly SessionManagementService sessionManagementService;
    private readonly ILogger<RoleProvisioner> logger;
    private readonly ApiSettings apiSettings;
    private readonly IUrlPathProvider urlPathProvider;
    private readonly ICustomerDomainChecker customerDomainChecker;

    public RoleProvisioner(
        AuthServicesContext dbContext,
        SessionManagementService sessionManagementService,
        IUrlPathProvider urlPathProvider,
        ICustomerDomainChecker customerDomainChecker,
        IOptions<ApiSettings> apiOptions,
        ILogger<RoleProvisioner> logger)
    {
        this.dbContext = dbContext;
        this.sessionManagementService = sessionManagementService;
        this.logger = logger;
        this.urlPathProvider = urlPathProvider;
        this.customerDomainChecker = customerDomainChecker;
        apiSettings = apiOptions.Value;
    }

    /// <summary>
    /// Handle the role provisioning request - this will either request a significant gesture from the user or issue
    /// a cookie.
    /// </summary>
    public async Task<HandleRoleProvisionResponse> CompleteRequest(
        int customerId,
        Uri requestOrigin,
        AccessService accessService,
        IProviderConfiguration providerConfiguration,
        CancellationToken cancellationToken = default)
    {
        var hostIsControlled = await customerDomainChecker.OriginForControlledDomain(customerId, requestOrigin);
        var roles = await GetRolesToBeGranted(customerId, accessService);

        if (hostIsControlled)
        {
            await sessionManagementService.CreateSessionForRoles(customerId, roles, requestOrigin.ToString(),
                cancellationToken);
            return HandleRoleProvisionResponse.Handled();
        }

        // We need to capture a significant gesture on this domain before we can issue a cookie
        var gestureModel = await GetSignificantGestureModel(customerId, roles, requestOrigin.ToString(),
            providerConfiguration, cancellationToken);
        return HandleRoleProvisionResponse.SignificantGesture(gestureModel);
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

    private async Task<SignificantGestureModel> GetSignificantGestureModel(int customerId, IReadOnlyCollection<string> roles,
        string origin, IProviderConfiguration configuration, CancellationToken cancellationToken)
    {
        var expiringToken =
            await sessionManagementService.CreateRoleProvisionToken(customerId, roles, origin, cancellationToken);
        var relativePath = urlPathProvider.GetGesturePostbackRelativePath(customerId);
        var gestureModel = new SignificantGestureModel(
            relativePath,
            configuration.GestureTitle ?? apiSettings.DefaultSignificantGestureTitle,
            configuration.GestureMessage ?? apiSettings.DefaultSignificantGestureMessage,
            expiringToken);
        return gestureModel;
    }
}