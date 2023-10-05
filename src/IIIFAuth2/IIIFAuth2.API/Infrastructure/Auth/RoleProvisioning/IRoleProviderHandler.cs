using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Models.Domain;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning;

/// <summary>
/// A delegate for finding appropriate <see cref="IRoleProviderHandler"/> 
/// </summary>
public delegate IRoleProviderHandler RoleProviderHandlerResolver(RoleProviderType roleProviderType);

/// <summary>
/// Base interface for RoleProvider implementations
/// </summary>
public interface IRoleProviderHandler
{
    /// <summary>
    /// Handle the request for role-provisioning. Depending in the configuration of the role-provider, this can either
    /// create a session + issue a cookie (if authorizing aspect present) or request the user carry out a significant
    /// gesture in order that a cookie may be issues for that domain
    /// </summary>
    /// <param name="customerId">Id of current customer</param>
    /// <param name="requestOrigin">"Origin" value for current request</param>
    /// <param name="accessService">Current AccessServiceDescription object</param>
    /// <param name="providerConfiguration">
    /// Current <see cref="IProviderConfiguration"/> object with details on how to provision role
    /// </param>
    /// <param name="hostIsControlled">
    /// If true, Origin is either for current host or a known domain that we will issue a cookie for. Assumption is that
    /// if we are issuing a cookie for domain we don't need to capture a significant gesture
    /// </param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns><see cref="HandleRoleProvisionResponse"/> representing how this request has been handled</returns>
    Task<HandleRoleProvisionResponse> HandleRequest(
        int customerId,
        string requestOrigin,
        AccessService accessService,
        IProviderConfiguration providerConfiguration, 
        bool hostIsControlled,
        CancellationToken cancellationToken = default);
}