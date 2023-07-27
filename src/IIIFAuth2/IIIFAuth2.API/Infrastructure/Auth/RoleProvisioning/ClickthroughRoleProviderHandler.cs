using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Auth.Models;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Settings;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning;

/// <summary>
/// Implementation of <see cref="IRoleProviderHandler"/> for Clickthrough operations
/// </summary>
public class ClickthroughRoleProviderHandler : IRoleProviderHandler
{
    private readonly AuthServicesContext dbContext;
    private readonly SessionManagementService sessionManagementService;
    private readonly ApiSettings apiSettings;

    public ClickthroughRoleProviderHandler(
        AuthServicesContext dbContext,
        SessionManagementService sessionManagementService,
        IOptions<ApiSettings> apiOptions)
    {
        this.dbContext = dbContext;
        this.sessionManagementService = sessionManagementService;
        apiSettings = apiOptions.Value;
    }

    public async Task<HandleRoleProvisionResponse> HandleRequest(int customerId,
        AccessService accessService,
        IProviderConfiguration providerConfiguration,
        bool hostIsOrigin,
        CancellationToken cancellationToken = default)
    {
        // TODO - can this be avoided with generics
        if (providerConfiguration is not ClickthroughConfiguration configuration)
        {
            throw new ArgumentException("TODO - suitable error that could go on base class");
        }

        var roles = await GetRolesToBeGranted(customerId, accessService);

        if (hostIsOrigin)
        {
            await sessionManagementService.CreateSessionForRoles(customerId, roles, cancellationToken);
            return HandleRoleProvisionResponse.Handled();
        }

        // We need to capture a significant gesture on this domain before we can issue a cookie
        var expiringToken = await sessionManagementService.CreateRoleProvisionToken(customerId, roles, cancellationToken); 
        var gestureModel = new SignificantGestureModel(
            configuration.GestureTitle ?? apiSettings.DefaultSignificantGestureTitle,
            configuration.GestureMessage ?? apiSettings.DefaultSignificantGestureMessage,
            expiringToken);

        return HandleRoleProvisionResponse.SignificantGesture(gestureModel);
    }

    private async Task<string[]> GetRolesToBeGranted(int customerId, AccessService accessService)
    {
        var customerRoles = await dbContext.Roles.GetCachedCustomerRecords(customerId, CacheKeys.Roles);
        var roles = customerRoles
            .Where(r => r.AccessServiceId == accessService.Id)
            .Select(r => r.Id)
            .ToArray();
        return roles;
    }
}