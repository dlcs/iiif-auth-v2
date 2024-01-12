using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Models.Domain;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Oidc;

/// <summary>
/// Handles RoleProvisioning for OAuth2/OIDC configurations
/// </summary>
public class OidcRoleProviderHandler
{
    private readonly Auth0Client auth0Client;
    private readonly SessionManagementService sessionManagementService;

    public OidcRoleProviderHandler(Auth0Client auth0Client, SessionManagementService sessionManagementService)
    {
        this.auth0Client = auth0Client;
        this.sessionManagementService = sessionManagementService;
    }

    public async Task<HandleRoleProvisionResponse> InitiateLoginRequest(int customerId, Uri requestOrigin,
        AccessService accessService, IProviderConfiguration providerConfiguration,
        CancellationToken cancellationToken = default)
    {
        var configuration = providerConfiguration.SafelyGetTypedConfig<OidcConfiguration>();
        ValidateProviderIsSupported(configuration);

        var roleProvisionTokenId = await sessionManagementService.CreateRoleProvisionToken(customerId,
            Array.Empty<string>(), requestOrigin.ToString(), cancellationToken);
        
        var loginUrl = auth0Client.GetAuthLoginUrl(configuration, accessService, roleProvisionTokenId);
        return HandleRoleProvisionResponse.Redirect(loginUrl);
    }

    private static void ValidateProviderIsSupported(OidcConfiguration configuration)
    {
        if (!configuration.Provider.Equals(OidcConfiguration.SupportedProviders.Auth0, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only Auth0 is supported as oidc provider");
        }
    }
}