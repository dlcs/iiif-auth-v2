using System.Text.Json.Serialization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Auth0.AuthenticationApi.Builders;
using Auth0.AuthenticationApi.Models;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Utils;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Oidc;

public class Auth0Client
{
    private readonly IUrlPathProvider urlPathProvider;
    private readonly HttpClient httpClient;
    private readonly IAppCache appCache;
    private readonly ILogger<Auth0Client> logger;

    public Auth0Client(
        IUrlPathProvider urlPathProvider,
        HttpClient httpClient,
        IAppCache appCache,
        ILogger<Auth0Client> logger)
    {
        this.urlPathProvider = urlPathProvider;
        this.httpClient = httpClient;
        this.appCache = appCache;
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

        var authBuilder = new AuthorizationUrlBuilder(oidcConfiguration.Domain)
            .WithClient(oidcConfiguration.ClientId)
            .WithRedirectUrl(callbackUrl)
            .WithResponseType(AuthorizationResponseType.Code)
            .WithState(state)
            .WithScopes(additionalScopes.Append("openid").ToArray());

        var loginUrl = authBuilder.Build();
        return loginUrl;
    }

    /// <summary>
    /// Exchange authentication code for access tokens for logged in user
    /// </summary>
    public async Task<IReadOnlyCollection<string>> GetDlcsRolesForCode(OidcConfiguration oidcConfiguration,
        AccessService accessService, string code, CancellationToken cancellationToken)
    {
        // TODO - log errors
        var auth0Token = await GetAuth0Token(oidcConfiguration, accessService, code, cancellationToken);

        var claimsPrincipal = GetClaimsFromToken(auth0Token, oidcConfiguration.Domain, cancellationToken);
        
        // TODO - determine DLCS roles based on the OidcConfiguration object

        return Array.Empty<string>();
    }

    private async Task<Auth0TokenResponse> GetAuth0Token(OidcConfiguration oidcConfiguration,
        AccessService accessService, string code, CancellationToken cancellationToken)
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
    
    private async Task<ClaimsPrincipal?> GetClaimsFromToken(Auth0TokenResponse auth0Token, string domain,
        CancellationToken cancellationToken)
    {
        try
        {
            var jwks = await GetWebKeySetForDomain(domain, cancellationToken);

            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                IssuerSigningKeys = jwks.GetSigningKeys(),
            };
            var claimsPrincipal = tokenHandler.ValidateToken(auth0Token.IdToken, tokenValidationParameters, out _);
            return claimsPrincipal;
        }
        catch (SecurityTokenException ste)
        {
            logger.LogError(ste, "Received invalid jwt token");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unknown error validating jwt token");
        }

        return null;
    }

    private async Task<JsonWebKeySet> GetWebKeySetForDomain(string auth0Domain, CancellationToken cancellationToken)
    {
        var cacheKey = $"{auth0Domain}:jwks";
        return await appCache.GetOrAddAsync(cacheKey, async () =>
        {
            var builder = new UriBuilder(auth0Domain) { Path = "/.well-known/jwks.json" };
            var jwks = await httpClient.GetFromJsonAsync<JsonWebKeySet>(builder.Uri, cancellationToken);
            return jwks.ThrowIfNull(nameof(jwks));
        }, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });
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
