using IIIFAuth2.API.Models.Domain;
using Auth0.AuthenticationApi.Builders;
using Auth0.AuthenticationApi.Models;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Web;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Oidc;

public class Auth0Client
{
    private readonly IUrlPathProvider urlPathProvider;

    public Auth0Client(IUrlPathProvider urlPathProvider)
    {
        this.urlPathProvider = urlPathProvider;
    }
    
    /// <summary>
    /// Get URI to redirect user for authorizing with auth0
    /// </summary>
    /// <remarks>See https://auth0.com/docs/api/authentication#-get-authorize- </remarks>
    public Uri GetAuthLoginUrl(OidcConfiguration oidcConfiguration, AccessService accessService)
    {
        var callbackUrl = urlPathProvider.GetAccessServiceOAuthCallbackPath(accessService);
        
        // TODO create and add a State value with meaning - use RoleProvisionToken table, maybe with a different flag?
        var authBuilder = new AuthorizationUrlBuilder(oidcConfiguration.Domain)
            .WithClient(oidcConfiguration.ClientId)
            .WithRedirectUrl(callbackUrl)
            .WithResponseType(AuthorizationResponseType.Code)
            .WithState(Guid.NewGuid().ToString());

        var url = authBuilder.Build();
        return url;
    }
}