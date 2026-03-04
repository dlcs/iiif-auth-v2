using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Utils;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Oidc;

public interface IOAuthClient
{
    /// <summary>
    /// Get URI to redirect user for authorizing with auth0, entra (and other ODIC compliant providers)
    /// </summary>
    /// <remarks>
    /// Auth0: https://auth0.com/docs/api/authentication#-get-authorize
    /// Entra: https://learn.microsoft.com/en-us/azure/active-directory/develop/v2-oauth2-auth-code-flow
    /// </remarks>
    Task<Uri> GetAuthLoginUrl(OidcConfiguration oidcConfiguration, AccessService accessService, string state, CancellationToken cancellationToken);

    /// <summary>
    /// Exchange authentication code for access tokens for logged-in user
    /// </summary>
    Task<IReadOnlyCollection<string>> GetDlcsRolesForCode(OidcConfiguration oidcConfiguration,
        AccessService accessService, string code, CancellationToken cancellationToken);
}

public class OAuthClient(
    IUrlPathProvider urlPathProvider,
    HttpClient httpClient,
    IJwtTokenHandler jwtTokenHandler,
    ClaimsConverter claimsConverter,
    ILogger<OAuthClient> logger) : IOAuthClient
{
    //Should be consistent with OIDC spec, but can be overridden in case of non-compliant providers or future changes.
    //Can also consider making this provider specific if needed.
    private const string DefaultMetadataPath = "/.well-known/openid-configuration";
    
    private sealed record OidcMetadata(
        [property: JsonPropertyName("authorization_endpoint")] string? AuthorizationEndpoint,
        [property: JsonPropertyName("token_endpoint")] string? TokenEndpoint,
        [property: JsonPropertyName("issuer")] string? Issuer,
        [property: JsonPropertyName("jwks_uri")] string? JwksUri);
    
    private static string NormalizeDomain(string domain) => domain.TrimEnd('/');

    /// <summary>
    /// Get URI to redirect user for authorizing with auth0 and Entra (and other ODIC compliant providers)
    /// </summary>
    /// <remarks>
    /// Auth0: https://auth0.com/docs/api/authentication#-get-authorize
    /// Entra: https://learn.microsoft.com/en-us/azure/active-directory/develop/v2-oauth2-auth-code-flow
    /// </remarks>
    public async Task<Uri> GetAuthLoginUrl(OidcConfiguration oidcConfiguration, AccessService accessService, string state, CancellationToken cancellationToken)
    {
        var domain = NormalizeDomain(oidcConfiguration.Domain);
        var callbackUrl = urlPathProvider.GetAccessServiceOAuthCallbackPath(accessService);

        var scopes = BuildScopes(oidcConfiguration);

        var metadata = await GetMetadata(domain, cancellationToken);
        if (metadata?.AuthorizationEndpoint == null)
        {
            throw new InvalidOperationException("Check provider as authorization_endpoint can't be located");
        }

        var queryParams = new Dictionary<string, string?>
        {
            { "client_id", oidcConfiguration.ClientId },
            { "redirect_uri", callbackUrl.ToString() },
            { "response_type", "code" },
            { "state", state },
            { "scope", string.Join(' ', scopes) },
        };

        var loginUrl = new Uri(QueryHelpers.AddQueryString(metadata.AuthorizationEndpoint, queryParams));
        logger.LogDebug("Generated {Provider} login url {Url} for accessService {Service}",
            oidcConfiguration.Provider, loginUrl, accessService.Id);

        return loginUrl;
    }
    
    /// <summary>
    /// Exchange authentication code for access token for logged-in user
    /// </summary>
    public async Task<IReadOnlyCollection<string>> GetDlcsRolesForCode(OidcConfiguration oidcConfiguration,
        AccessService accessService, string code, CancellationToken cancellationToken)
    {
        var domain = NormalizeDomain(oidcConfiguration.Domain);
        var metadata = await GetMetadata(domain, cancellationToken);
        if (metadata == null || metadata.TokenEndpoint == null || metadata.Issuer == null || metadata.JwksUri == null)
        {
            logger.LogError("OIDC metadata missing required fields for provider {Provider}", oidcConfiguration.Provider);
            return [];
        }

        var oidcToken = await GetAuthToken(oidcConfiguration, accessService, code, metadata.TokenEndpoint, cancellationToken);
        if (oidcToken == null)
        {
            return [];
        }
        
        var issuer = metadata.Issuer;
        var jwksUri = new Uri(metadata.JwksUri);
        var audience = oidcConfiguration.ClientId;
       
        var claimsPrincipal =
            await jwtTokenHandler.GetClaimsFromToken(oidcToken.IdToken, jwksUri,
                issuer, audience, oidcConfiguration.ClientSecret,  oidcConfiguration.Provider,  cancellationToken);
        if (claimsPrincipal == null)
        {
            return [];
        }
        
        var dlcsRoles = claimsConverter.GetDlcsRolesFromClaims(claimsPrincipal, oidcConfiguration);
        return dlcsRoles.Success ? dlcsRoles.Value! : [];
    }

    private static HashSet<string> BuildScopes(OidcConfiguration oidcConfiguration)
    {
        //default to "openid" scope as it's required for OIDC and should be included
        //even if provider doesn't specify it. Then add provider specific default scopes
        //followed by any additional scopes specified in configuration.
        var scopes = new HashSet<string>(["openid"], StringComparer.OrdinalIgnoreCase);
        
        if (!string.IsNullOrWhiteSpace(oidcConfiguration.Scopes))
        {
            scopes.UnionWith(
                oidcConfiguration.Scopes
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return scopes;
    }
    
    private async Task<OAuthTokenResponse?> GetAuthToken(OidcConfiguration oidcConfiguration,
        AccessService accessService,
        string code,
        string tokenEndpoint,
        CancellationToken cancellationToken)
    {
        try
        {
            var callbackUrl = urlPathProvider.GetAccessServiceOAuthCallbackPath(accessService);
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

            var authToken =
                await response.Content.ReadFromJsonAsync<OAuthTokenResponse>(cancellationToken: cancellationToken);
            return authToken.ThrowIfNull(nameof(authToken));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected exception getting accessToken from {Provider} for {ClientId}",
                oidcConfiguration.Provider, oidcConfiguration.ClientId);
            return null;
        }
    }

    private async Task<OidcMetadata?> GetMetadata(string domain, CancellationToken cancellationToken)
    {
        var metadataEndpoint = new Uri(new Uri(domain.EnsureEndsWith("/")), DefaultMetadataPath.TrimStart('/'));
        try
        {
            using var response = await httpClient.GetAsync(metadataEndpoint, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var metadata = await JsonSerializer.DeserializeAsync<OidcMetadata>(stream, cancellationToken: cancellationToken);
            return metadata;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch OIDC metadata from {MetadataEndpoint}", metadataEndpoint);
            return null;
        }
    }
}

internal class OAuthTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = null!;
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = null!;
    [JsonPropertyName("id_token")] public string IdToken { get; set; } = null!;
    [JsonPropertyName("token_type")] public string TokenType { get; set; } = null!;
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
}
