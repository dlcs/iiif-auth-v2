using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IIIFAuth2.API.Settings;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Oidc;

public interface IJwtTokenHandler
{
    /// <summary>
    /// Validate JWT token and return <see cref="ClaimsPrincipal"/> if successful
    /// </summary>
    /// <param name="jwtToken">JWT id token string</param>
    /// <param name="jwksUri">Path where jwks can be found</param>
    /// <param name="issuer">Valid "iss" value</param>
    /// <param name="audience">Valid "aud" value</param>
    /// <param name="clientSecret">ClientSecret, if known. Used for symmetric validation</param>
    /// <param name="provider">Provider such as auth0 or entra </param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns><see cref="ClaimsPrincipal"/> if jwt is valid, else null</returns>
    Task<ClaimsPrincipal?> GetClaimsFromToken(string jwtToken, Uri jwksUri, string issuer, string audience,
        string? clientSecret, string provider, CancellationToken cancellationToken);
}

public class JwtTokenHandler(
    HttpClient httpClient,
    IAppCache appCache,
    IOptions<AuthSettings> authOptions,
    ILogger<JwtTokenHandler> logger)
    : IJwtTokenHandler
{
    private readonly AuthSettings authSettings = authOptions.Value;

    /// <inheritdoc />
    public async Task<ClaimsPrincipal?> GetClaimsFromToken(string jwtToken, Uri jwksUri, string issuer,
        string audience, string? clientSecret, string provider, CancellationToken cancellationToken)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var alg = tokenHandler.ReadJwtToken(jwtToken).Header.Alg;
            var issuerSigningKeys = await GetSigningKeys(alg, jwksUri, clientSecret, cancellationToken);

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = issuerSigningKeys,
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ValidateActor = false,
                ValidateTokenReplay = false,
            };

            return tokenHandler.ValidateToken(jwtToken, tokenValidationParameters, out _);
        }
        catch (SecurityTokenException ste)
        {
            logger.LogError(ste, "Received invalid {Provider} jwt token", provider);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unknown error validating {Provider} jwt token", provider);
        }

        return null;
    }


    private async Task<IList<SecurityKey>> GetSigningKeys(string algorithm, Uri jwksUri, string? clientSecret, CancellationToken
        cancellationToken)
    {
        // jwks used for "alg": "RS256"
        var jwks = await GetWebKeySetForDomain(jwksUri, cancellationToken);
        var issuerSigningKeys = jwks.GetSigningKeys();

        if (!string.IsNullOrWhiteSpace(clientSecret) && algorithm.StartsWith("HS", StringComparison.OrdinalIgnoreCase))
        {
            // client-secret for "alg": "HS256"
            issuerSigningKeys.Add(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(clientSecret)));
        }
        return issuerSigningKeys;
    }

    private async Task<JsonWebKeySet> GetWebKeySetForDomain(Uri jwksPath, CancellationToken cancellationToken)
    {
        var cacheKey = $"{jwksPath}:jwks";
        return await appCache.GetOrAddAsync(cacheKey, async () =>
        {
            logger.LogDebug("Refreshing jwks cache from {JWKSPath}", jwksPath);
            var jwks = await httpClient.GetStringAsync(jwksPath, cancellationToken);
            return new JsonWebKeySet(jwks);
        }, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(authSettings.JwksTtl) });
    }

}