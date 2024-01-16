using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Oidc;

public interface IJwtTokenHandler
{
    /// <summary>
    /// Validate JWT token and return <see cref="ClaimsPrincipal"/> if successfully parsed.
    /// </summary>
    /// <param name="jwtToken">JWT id token string</param>
    /// <param name="jwksUri">Path where jwks can be found</param>
    /// <param name="issuer">Valid "iss" value</param>
    /// <param name="audience">Valid "aud" value</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns><see cref="ClaimsPrincipal"/> if jwt is valid, else null</returns>
    Task<ClaimsPrincipal?> GetClaimsFromToken(string jwtToken, Uri jwksUri, string issuer, string audience,
        CancellationToken cancellationToken);
}

public class JwtTokenHandler : IJwtTokenHandler
{
    private readonly HttpClient httpClient;
    private readonly IAppCache appCache;
    private readonly ILogger<JwtTokenHandler> logger;

    public JwtTokenHandler(HttpClient httpClient, IAppCache appCache, ILogger<JwtTokenHandler> logger)
    {
        this.httpClient = httpClient;
        this.appCache = appCache;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<ClaimsPrincipal?> GetClaimsFromToken(string jwtToken, Uri jwksUri, string issuer,
        string audience, CancellationToken cancellationToken)
    {
        try
        {
            var jwks = await GetWebKeySetForDomain(jwksUri, cancellationToken);

            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = jwks.GetSigningKeys(),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ValidateActor = false,
                ValidateTokenReplay = false,
            };
            var claimsPrincipal = tokenHandler.ValidateToken(jwtToken, tokenValidationParameters, out _);
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
    
    private async Task<JsonWebKeySet> GetWebKeySetForDomain(Uri jwksPath, CancellationToken cancellationToken)
    {
        var cacheKey = $"{jwksPath}:jwks";
        return await appCache.GetOrAddAsync(cacheKey, async () =>
        {
            var jwks = await httpClient.GetStringAsync(jwksPath, cancellationToken);
            return new JsonWebKeySet(jwks);
        }, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });
    }
}