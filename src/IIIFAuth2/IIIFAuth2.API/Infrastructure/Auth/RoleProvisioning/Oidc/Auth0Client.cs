using System.Text.Json.Serialization;
using Auth0.AuthenticationApi.Builders;
using Auth0.AuthenticationApi.Models;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Utils;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Oidc;

public interface IAuth0Client
{
    /// <summary>
    /// Get URI to redirect user for authorizing with auth0
    /// </summary>
    /// <remarks>See https://auth0.com/docs/api/authentication#-get-authorize- </remarks>
    Uri GetAuthLoginUrl(OidcConfiguration oidcConfiguration, AccessService accessService, string state);

    /// <summary>
    /// Exchange authentication code for access tokens for logged in user
    /// </summary>
    Task<IReadOnlyCollection<string>> GetDlcsRolesForCode(OidcConfiguration oidcConfiguration,
        AccessService accessService, string code, CancellationToken cancellationToken);
}

public class Auth0Client : IAuth0Client
{
    private readonly IUrlPathProvider urlPathProvider;
    private readonly HttpClient httpClient;
    private readonly IJwtTokenHandler jwtTokenHandler;
    private readonly ClaimsConverter claimsConverter;
    private readonly ILogger<Auth0Client> logger;

    public Auth0Client(
        IUrlPathProvider urlPathProvider,
        HttpClient httpClient,
        IJwtTokenHandler jwtTokenHandler,
        ClaimsConverter claimsConverter,
        ILogger<Auth0Client> logger)
    {
        this.urlPathProvider = urlPathProvider;
        this.httpClient = httpClient;
        this.jwtTokenHandler = jwtTokenHandler;
        this.claimsConverter = claimsConverter;
        this.logger = logger;
    }
    
    /// <summary>
    /// Get URI to redirect user for authorizing with auth0
    /// </summary>
    /// <remarks>See https://auth0.com/docs/api/authentication#-get-authorize- </remarks>
    public Uri GetAuthLoginUrl(OidcConfiguration oidcConfiguration, AccessService accessService, string state)
    {
        var callbackUrl = urlPathProvider.GetAccessServiceOAuthCallbackPath(accessService);
        
        var additionalScopes = oidcConfiguration.Scopes?.Split(",", StringSplitOptions.RemoveEmptyEntries) ??
                               Array.Empty<string>();

        /* don't use .WithRedirectUrl(Uri uri) and instead pass string. The former uses .OriginalString and will always
         * include the port number (so https://dlcs.digirati.io/ => https://dlcs.digirati.io:443/). This differs from
         * how the redirect URI is build for code exchange and causes 403 error */
        var authBuilder = new AuthorizationUrlBuilder(oidcConfiguration.Domain)
            .WithClient(oidcConfiguration.ClientId)
            .WithRedirectUrl(callbackUrl.ToString())
            .WithResponseType(AuthorizationResponseType.Code)
            .WithState(state)
            .WithScopes(additionalScopes.Append("openid").ToArray());

        var loginUrl = authBuilder.Build();
        logger.LogDebug("Generated auth0 login url {Auth0Url} for accessService {AccessServiceId}", loginUrl,
            accessService.Id);
        return loginUrl;
    }

    /// <summary>
    /// Exchange authentication code for access token for logged in user
    /// </summary>
    public async Task<IReadOnlyCollection<string>> GetDlcsRolesForCode(OidcConfiguration oidcConfiguration,
        AccessService accessService, string code, CancellationToken cancellationToken)
    {
        var auth0Token = await GetAuth0Token(oidcConfiguration, accessService, code, cancellationToken);
        if (auth0Token == null) return Array.Empty<string>();

        // For auth0 "iss" = auth0 domain (ending in /) and "aud" = clientId 
        var issuer = oidcConfiguration.Domain.EnsureEndsWith("/");
        var audience = oidcConfiguration.ClientId;
        
        var claimsPrincipal =
            await jwtTokenHandler.GetClaimsFromToken(auth0Token.IdToken, GetJwksUri(oidcConfiguration),
                issuer, audience, oidcConfiguration.ClientSecret, cancellationToken);
        if (claimsPrincipal == null) return Array.Empty<string>();
        
        var dlcsRoles = claimsConverter.GetDlcsRolesFromClaims(claimsPrincipal, oidcConfiguration);
        return dlcsRoles.Success ? dlcsRoles.Value! : Array.Empty<string>();
    }

    private Uri GetJwksUri(OidcConfiguration oidcConfiguration)
        => new UriBuilder(oidcConfiguration.Domain) { Path = "/.well-known/jwks.json" }.Uri;

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
