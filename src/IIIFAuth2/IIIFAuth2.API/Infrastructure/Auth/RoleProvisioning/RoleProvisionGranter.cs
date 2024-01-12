using IIIFAuth2.API.Infrastructure.Auth.Models;
using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Settings;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning;

/// <summary>
/// Class used to handle provisioning a token OR initiating a request for a significant gesture. This happens when the
/// user has provided enough information that we are satisfied that have relevant access required for the provided
/// accessService.
/// </summary>
public class RoleProvisionGranter
{
    private readonly SessionManagementService sessionManagementService;
    private readonly ApiSettings apiSettings;
    private readonly IUrlPathProvider urlPathProvider;
    private readonly ICustomerDomainChecker customerDomainChecker;

    public RoleProvisionGranter(
        SessionManagementService sessionManagementService,
        IUrlPathProvider urlPathProvider,
        ICustomerDomainChecker customerDomainChecker,
        IOptions<ApiSettings> apiOptions)
    {
        this.sessionManagementService = sessionManagementService;
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
        IProviderConfiguration providerConfiguration,
        Func<Task<IReadOnlyCollection<string>>> getRolesToBeGranted,
        CancellationToken cancellationToken = default)
    {
        var hostIsControlled = await customerDomainChecker.OriginForControlledDomain(customerId, requestOrigin);
        var roles = await getRolesToBeGranted();

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