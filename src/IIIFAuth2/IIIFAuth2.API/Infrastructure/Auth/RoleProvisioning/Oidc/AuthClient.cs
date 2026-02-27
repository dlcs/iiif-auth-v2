using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Utils;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Oidc;

public interface IAuthClient
{
    /// <summary>
    /// Get URI to redirect user for authorizing with auth0 (and other ODIC compliant providers)
    /// </summary>
    /// <remarks>See https://auth0.com/docs/api/authentication#-get-authorize- </remarks>
    Uri GetAuthLoginUrl(OidcConfiguration oidcConfiguration, AccessService accessService, string state);

    /// <summary>
    /// Exchange authentication code for access tokens for logged-in user
    /// </summary>
    Task<IReadOnlyCollection<string>> GetDlcsRolesForCode(OidcConfiguration oidcConfiguration,
        AccessService accessService, string code, CancellationToken cancellationToken);
}

public class AuthClient(
    IUrlPathProvider urlPathProvider,
    HttpClient httpClient,
    IJwtTokenHandler jwtTokenHandler,
    ClaimsConverter claimsConverter,
    ILogger<AuthClient> logger) : IAuthClient
{
    /// <summary>
    /// Get URI to redirect user for authorizing with auth0
    /// </summary>
    /// <remarks>See https://auth0.com/docs/api/authentication#-get-authorize- </remarks>
    public Uri GetAuthLoginUrl(OidcConfiguration oidcConfiguration, AccessService accessService, string state)
    {
        var provider = oidcConfiguration.Provider.ToLowerInvariant();
        
        var callbackUrl = urlPathProvider.GetAccessServiceOAuthCallbackPath(accessService);

        var additionalScopes = oidcConfiguration.Scopes?.Split(",", StringSplitOptions.RemoveEmptyEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList() ?? new List<string>();

        additionalScopes.Add("openid");

        string authorizationEndpoint;

        switch (provider)
        {
            case "entra":
                additionalScopes.Add("profile");
                additionalScopes.Add("offline_access");
                authorizationEndpoint = oidcConfiguration.Domain.EnsureEndsWith("/") + "oauth2/v2.0/authorize";
                break;
            case "auth0":
                authorizationEndpoint = new UriBuilder(oidcConfiguration.Domain)
                {
                    Path = "/authorize"
                }.ToString();
                break;
            default:
                throw new NotSupportedException($"Provider is not supported {provider}");
        }


        var queryParams = new Dictionary<string, string?>
        {
            { "client_id", oidcConfiguration.ClientId },
            { "redirect_uri", callbackUrl.ToString() },
            { "response_type", "code" },
            { "state", state },
            { "scope", string.Join(' ', additionalScopes) },
        };

        var loginUrl = new Uri(QueryHelpers.AddQueryString(authorizationEndpoint, queryParams));
        logger.LogDebug("Generated entra login url {url} for accessService {service}", loginUrl,
            accessService.Id);

        return loginUrl;

    }

    

    /// <summary>
    /// Exchange authentication code for access token for logged-in user
    /// </summary>
    public async Task<IReadOnlyCollection<string>> GetDlcsRolesForCode(OidcConfiguration oidcConfiguration,
        AccessService accessService, string code, CancellationToken cancellationToken)
    {

        var provider = oidcConfiguration.Provider.ToLowerInvariant();

        var auth0Token = await GetAuthToken(oidcConfiguration, accessService, code, cancellationToken);
        if (auth0Token == null) return Array.Empty<string>();


        string issuer;
        Uri jwksUri;
        
        switch (provider)
        {
            case "entra":
                issuer = oidcConfiguration.Domain.EnsureEndsWith("/v2.0/");
                jwksUri = new UriBuilder(oidcConfiguration.Domain.EnsureEndsWith("/") +
                                         ".well-known/openid-configuration").Uri;
                break;
            case "auth0":
                issuer = oidcConfiguration.Domain.EnsureEndsWith("/");
                jwksUri = new UriBuilder(oidcConfiguration.Domain) { Path = "/.well-known/jwks.json" }.Uri;
                break;
            default:
                throw  new NotSupportedException($"provider is not supported {provider}");
                
        }
        
        var audience = oidcConfiguration.ClientId;
       //var jwksUri = GetJwksUri(oidcConfiguration);
       
        var claimsPrincipal =
            await jwtTokenHandler.GetClaimsFromToken(auth0Token.IdToken, jwksUri,
                issuer, audience, oidcConfiguration.ClientSecret,  oidcConfiguration.Provider,  cancellationToken);
        if (claimsPrincipal == null) return Array.Empty<string>();
        
        var dlcsRoles = claimsConverter.GetDlcsRolesFromClaims(claimsPrincipal, oidcConfiguration);
        return dlcsRoles.Success ? dlcsRoles.Value! : Array.Empty<string>();
    }

   

    private async Task<Auth0TokenResponse?> GetAuthToken(OidcConfiguration oidcConfiguration,
        AccessService accessService,
        string code, CancellationToken cancellationToken)
    {
        try
        {
            var callbackUrl = urlPathProvider.GetAccessServiceOAuthCallbackPath(accessService);

            var provider = oidcConfiguration.Provider.ToLowerInvariant();


            //Entra
            var tokenEndpoint = provider switch
            {
                "entra" => oidcConfiguration.Domain.EnsureEndsWith("/") + "oauth2/v2.0/token",
                "auth0" => new UriBuilder(oidcConfiguration.Domain)
                {
                    Path = "/oauth/token"
                }.Uri.ToString(),
                _ => throw new NotSupportedException("provider not supported")
            };

            var data = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "client_id", oidcConfiguration.ClientId },
                { "client_secret", oidcConfiguration.ClientSecret },
                { "code", code },
                { "redirect_uri", callbackUrl.ToString() },
            };

            var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
            request.Content = new FormUrlEncodedContent(data);
            var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var auth0Token =
                await response.Content.ReadFromJsonAsync<Auth0TokenResponse>(cancellationToken: cancellationToken);
            return auth0Token.ThrowIfNull(nameof(auth0Token));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected exception getting accessToken from {provider} for {ClientId}",
                oidcConfiguration.Provider, oidcConfiguration.ClientId);
            return null;
        }

    }


}

// ReSharper disable once ClassNeverInstantiated.Global
public class Auth0TokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = null!;

    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = null!;

    [JsonPropertyName("id_token")] public string IdToken { get; set; } = null!;

    [JsonPropertyName("token_type")] public string TokenType { get; set; } = null!;

    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
}
