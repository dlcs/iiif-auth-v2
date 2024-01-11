using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Models.Domain;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Oidc;

/// <summary>
/// Implementation of <see cref="IRoleProviderHandler"/> for Auth0/OIDC
/// </summary>
public class OidcRoleProviderHandler : IRoleProviderHandler
{
    private readonly Auth0Client auth0Client;

    public OidcRoleProviderHandler(Auth0Client auth0Client)
    {
        this.auth0Client = auth0Client;
    }
    
    public Task<HandleRoleProvisionResponse> HandleRequest(int customerId, string requestOrigin, AccessService accessService,
        IProviderConfiguration providerConfiguration, bool hostIsControlled, CancellationToken cancellationToken = default)
    {
        var configuration = providerConfiguration.SafelyGetTypedConfig<OidcConfiguration>();

        ValidateProviderIsSupported(configuration);

        var loginUrl = auth0Client.GetAuthLoginUrl(configuration, accessService);
        return Task.FromResult(HandleRoleProvisionResponse.Redirect(loginUrl));
    }

    private static void ValidateProviderIsSupported(OidcConfiguration configuration)
    {
        if (!configuration.Provider.Equals(OidcConfiguration.SupportedProviders.Auth0, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only Auth0 is supported as oidc provider");
        }
    }
}