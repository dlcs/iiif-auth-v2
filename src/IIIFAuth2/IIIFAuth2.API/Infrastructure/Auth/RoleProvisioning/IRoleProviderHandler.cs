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
    Task<HandleRoleProvisionResponse> HandleRequest(int customerId, AccessService accessService,
        IProviderConfiguration providerConfiguration, bool hostIsOrigin, CancellationToken cancellationToken = default);
}