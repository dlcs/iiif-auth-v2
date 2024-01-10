using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Models.Domain;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Oidc;

public class OidcRoleProviderHandler : IRoleProviderHandler
{
    public Task<HandleRoleProvisionResponse> HandleRequest(int customerId, string requestOrigin, AccessService accessService,
        IProviderConfiguration providerConfiguration, bool hostIsControlled, CancellationToken cancellationToken = default)
    {
        var configuration = providerConfiguration.SafelyGetTypedConfig<OidcConfiguration>();

        ValidateProviderIsSupported(configuration);
        
        // Make a request to /authorize endpoint using client secret etc in the 
        throw new NotImplementedException();
    }

    private static void ValidateProviderIsSupported(OidcConfiguration configuration)
    {
        if (!configuration.Provider.Equals(OidcConfiguration.SupportedProviders.Auth0))
        {
            throw new InvalidOperationException("Only Auth0 is supported as oidc provider");
        }
    }
}

