using System.Reflection.Metadata.Ecma335;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Utils;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Oidc;

/// <summary>
/// Handles RoleProvisioning for OAuth2/OIDC configurations
/// </summary>
public class OidcRoleProviderHandler
{
    private readonly Auth0Client auth0Client;
    private readonly SessionManagementService sessionManagementService;
    private readonly RoleProvisionGranter roleProvisionGranter;
    private readonly ILogger<OidcRoleProviderHandler> logger;

    public OidcRoleProviderHandler(
        Auth0Client auth0Client, 
        SessionManagementService sessionManagementService,
        RoleProvisionGranter roleProvisionGranter,
        ILogger<OidcRoleProviderHandler> logger)
    {
        this.auth0Client = auth0Client;
        this.sessionManagementService = sessionManagementService;
        this.roleProvisionGranter = roleProvisionGranter;
        this.logger = logger;
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

    public async Task<HandleRoleProvisionResponse> HandleLoginCallback(int customerId, string roleProvisionToken, string authCode,
        AccessService accessService, IProviderConfiguration providerConfiguration,
        CancellationToken cancellationToken = default)
    {
        var configuration = providerConfiguration.SafelyGetTypedConfig<OidcConfiguration>();
        ValidateProviderIsSupported(configuration);

        // Validate the roleProviderToken is valid and hasn't been used
        var validateTokenResult =
            await sessionManagementService.TryGetValidToken(roleProvisionToken, cancellationToken);

        if (!validateTokenResult.Success)
        {
            logger.LogInformation("Received nonce token {Token} in oidc response that is invalid",
                roleProvisionToken);
            return HandleRoleProvisionResponse.Error("Auth code invalid");
        }

        var requestUri = new Uri(validateTokenResult.Value!.Origin);
        
        // Get DLCS roles from authcode
        var roles = await auth0Client.GetDlcsRolesForCode(configuration, accessService, authCode, cancellationToken);
        
        if (roles.IsNullOrEmpty()) return HandleRoleProvisionResponse.Error("Unable to get DLCS roles for user");
        
        return await roleProvisionGranter.CompleteRequest(customerId, requestUri, providerConfiguration,
            () => Task.FromResult(roles), cancellationToken);
    }

    private static void ValidateProviderIsSupported(OidcConfiguration configuration)
    {
        if (!configuration.Provider.Equals(OidcConfiguration.SupportedProviders.Auth0, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only Auth0 is supported as oidc provider");
        }
    }
}