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
    private readonly ILogger<ClickthroughRoleProviderHandler> logger;
    private readonly ApiSettings apiSettings;

    public ClickthroughRoleProviderHandler(
        AuthServicesContext dbContext,
        SessionManagementService sessionManagementService,
        IOptions<ApiSettings> apiOptions,
        ILogger<ClickthroughRoleProviderHandler> logger)
    {
        this.dbContext = dbContext;
        this.sessionManagementService = sessionManagementService;
        this.logger = logger;
        apiSettings = apiOptions.Value;
    }

    /// <inheritdoc />
    public async Task<HandleRoleProvisionResponse> HandleRequest(int customerId,
        string requestOrigin,
        AccessService accessService,
        IProviderConfiguration providerConfiguration,
        bool hostIsControlled,
        CancellationToken cancellationToken = default)
    {
        if (providerConfiguration is not ClickthroughConfiguration configuration)
        {
            logger.LogError(
                $"{nameof(ClickthroughRoleProviderHandler)} given non-clickthrough configuration {{@Configuration}}",
                providerConfiguration);
            throw new ArgumentException("Unable to handle provided configuration", nameof(providerConfiguration));
        }

        var roles = await GetRolesToBeGranted(customerId, accessService);

        if (hostIsControlled)
        {
            await sessionManagementService.CreateSessionForRoles(customerId, roles, requestOrigin, cancellationToken);
            return HandleRoleProvisionResponse.Handled();
        }

        // We need to capture a significant gesture on this domain before we can issue a cookie
        var gestureModel = await GetSignificantGestureModel(customerId, roles, requestOrigin, configuration, cancellationToken);
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
        string origin, ClickthroughConfiguration configuration, CancellationToken cancellationToken)
    {
        var expiringToken =
            await sessionManagementService.CreateRoleProvisionToken(customerId, roles, origin, cancellationToken);
        var gestureModel = new SignificantGestureModel(
            configuration.GestureTitle ?? apiSettings.DefaultSignificantGestureTitle,
            configuration.GestureMessage ?? apiSettings.DefaultSignificantGestureMessage,
            expiringToken);
        return gestureModel;
    }
}