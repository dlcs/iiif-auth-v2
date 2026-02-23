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
        var url = oidcConfiguration.Provider.ToLowerInvariant() switch
        {
            "auth0" => GetAuth0LoginUrl(oidcConfiguration, accessService, state),
            "entra" => GetEntraLoginUrl(oidcConfiguration, accessService, state),
            _ => throw new NotSupportedException($"Unsupported OIDC provider: {oidcConfiguration.Provider}"),
        };

        return url;
    }



    /// <remarks>See https://auth0.com/docs/api/authentication#-get-authorize- </remarks>
    private Uri GetEntraLoginUrl(OidcConfiguration oidcConfiguration, AccessService accessService, string state)
    {
        var callbackUrl = urlPathProvider.GetAccessServiceOAuthCallbackPath(accessService);

        var additionalScopes = oidcConfiguration.Scopes?.Split(",", StringSplitOptions.RemoveEmptyEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList() ?? new List<string>();

        additionalScopes.Add("openid");
        additionalScopes.Add("profile");
        additionalScopes.Add("offline_access");

        var authorizationEndpoint = oidcConfiguration.Domain.EnsureEndsWith("/") + "oauth2/v2.0/authorize";
      

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
        /// Get URI to redirect user for authorizing with auth0
        /// </summary>
        /// <remarks>See https://auth0.com/docs/api/authentication#-get-authorize- </remarks>
    private Uri GetAuth0LoginUrl(OidcConfiguration oidcConfiguration, AccessService accessService, string state)
    {
        var callbackUrl = urlPathProvider.GetAccessServiceOAuthCallbackPath(accessService);

        var additionalScopes = oidcConfiguration.Scopes?.Split(",", StringSplitOptions.RemoveEmptyEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList() ?? new List<string>();

        additionalScopes.Add("openid");

        var authorizationEndpoint = new UriBuilder(oidcConfiguration.Domain)
        {
            Path = "/authorize"
        };

        var queryParams = new Dictionary<string, string?>
        {
            { "client_id", oidcConfiguration.ClientId },
            { "redirect_uri", callbackUrl.ToString() },
            { "response_type", "code" },
            { "state", state },
            { "scope", string.Join(' ', additionalScopes) },
        };

        var loginUrl = new Uri(QueryHelpers.AddQueryString(authorizationEndpoint.Uri.ToString(), queryParams));
        logger.LogDebug("Generated auth0 login url {url} for accessService {service}", loginUrl,
            accessService.Id);
        return loginUrl;
    }



    /// <summary>
    /// Exchange authentication code for access token for logged in user
    /// </summary>
    public async Task<IReadOnlyCollection<string>> GetDlcsRolesForCode(OidcConfiguration oidcConfiguration,
        AccessService accessService, string code, CancellationToken cancellationToken)
    {
        var auth0Token = await GetAuthToken(oidcConfiguration, accessService, code, cancellationToken);
        if (auth0Token == null) return Array.Empty<string>();

        // For auth0 "iss" = auth0 domain (ending in /) and "aud" = clientId 


        //This should really come from jwtweb .well-known/openid-configuration
        var issuer = oidcConfiguration.Provider.ToLowerInvariant() switch
        {
           "entra" => oidcConfiguration.Domain.EnsureEndsWith("/v2.0"),
           _ => oidcConfiguration.Domain.EnsureEndsWith("/") 
        };
        
        var audience = oidcConfiguration.ClientId;
   
        
        var claimsPrincipal =
            await jwtTokenHandler.GetClaimsFromToken(auth0Token.IdToken, GetJwksUri(oidcConfiguration),
                issuer, audience, oidcConfiguration.ClientSecret,  oidcConfiguration.Provider,  cancellationToken);
        if (claimsPrincipal == null) return Array.Empty<string>();
        
        var dlcsRoles = claimsConverter.GetDlcsRolesFromClaims(claimsPrincipal, oidcConfiguration);
        return dlcsRoles.Success ? dlcsRoles.Value! : Array.Empty<string>();
    }

    private Uri GetJwksUri(OidcConfiguration oidcConfiguration)
    {
        return oidcConfiguration.Provider.ToLowerInvariant() switch
        {
            "auth0" => new UriBuilder(oidcConfiguration.Domain) { Path = "/.well-known/jwks.json" }.Uri,
            "entra" => new UriBuilder(oidcConfiguration.Domain.EnsureEndsWith("/") + ".well-known/openid-configuration").Uri,
            _ => throw new NotSupportedException($"Unsupported OIDC provider: {oidcConfiguration.Provider}")
        };
    }


    private Task<Auth0TokenResponse?> GetAuthToken(OidcConfiguration oidcConfiguration, AccessService accessService,
        string code, CancellationToken cancellationToken)
    {
        return oidcConfiguration.Provider.ToLowerInvariant() switch
        {
            "auth0" => GetAuth0Token(oidcConfiguration, accessService, code, cancellationToken),
            "entra" => GetEntraToken(oidcConfiguration, accessService, code, cancellationToken),
            _ => throw new NotSupportedException($"Unsupported OIDC provider: {oidcConfiguration.Provider}"),
        };
    }

    private async Task<Auth0TokenResponse?> GetEntraToken(OidcConfiguration oidcConfiguration, AccessService accessService, string code, CancellationToken cancellationToken)
    {
        try
        {
            var callbackUrl = urlPathProvider.GetAccessServiceOAuthCallbackPath(accessService);
            var tokenEndpoint = oidcConfiguration.Domain.EnsureEndsWith("/") + "oauth2/v2.0/token";
            
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
            logger.LogError(ex, "Unexpected exception getting accessToken from auth0 for {ClientId}",
                oidcConfiguration.ClientId);
            return null;
        }
    }

    

    private async Task<Auth0TokenResponse?> GetAuth0Token(OidcConfiguration oidcConfiguration,
        AccessService accessService, string code, CancellationToken cancellationToken)
    {
        try
        {
            var callbackUrl = urlPathProvider.GetAccessServiceOAuthCallbackPath(accessService);
            var tokenEndpoint = new UriBuilder(oidcConfiguration.Domain)
            {
                Path = "/oauth/token"
            };

            var data = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "client_id", oidcConfiguration.ClientId },
                { "client_secret", oidcConfiguration.ClientSecret },
                { "code", code },
                { "redirect_uri", callbackUrl.ToString() },
            };

            var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint.Uri);
            request.Content = new FormUrlEncodedContent(data);
            var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var auth0Token =
                await response.Content.ReadFromJsonAsync<Auth0TokenResponse>(cancellationToken: cancellationToken);
            return auth0Token.ThrowIfNull(nameof(auth0Token));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected exception getting accessToken from auth0 for {ClientId}",
                oidcConfiguration.ClientId);
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
