using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IIIFAuth2.API.Settings;
using IIIFAuth2.API.Utils;
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
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns><see cref="ClaimsPrincipal"/> if jwt is valid, else null</returns>
    Task<ClaimsPrincipal?> GetClaimsFromToken(string jwtToken, Uri jwksUri, string issuer, string audience,
        string? clientSecret, string provider, CancellationToken cancellationToken);
}

public class JwtTokenHandler : IJwtTokenHandler
{
    private readonly HttpClient httpClient;
    private readonly IAppCache appCache;
    private readonly ILogger<JwtTokenHandler> logger;
    private readonly AuthSettings authSettings;

    public JwtTokenHandler(HttpClient httpClient, IAppCache appCache, IOptions<AuthSettings> authOptions,
        ILogger<JwtTokenHandler> logger)
    {
        this.httpClient = httpClient;
        this.appCache = appCache;
        this.logger = logger;
        authSettings = authOptions.Value;
    }

    /// <inheritdoc />
    public async Task<ClaimsPrincipal?> GetClaimsFromToken(string jwtToken, Uri jwksUri, string issuer,
        string audience, string? clientSecret, string? provider, CancellationToken cancellationToken)
    {


        return provider?.ToLower() switch
        {
            "entra" => await GetClaimsFromTokenEntra(jwtToken, jwksUri, issuer, audience, clientSecret, cancellationToken),
            _ => await ClaimsFromTokenAuth0(jwtToken, jwksUri, issuer, audience, clientSecret, cancellationToken)

        };
        
    }

    private async Task<ClaimsPrincipal?> ClaimsFromTokenAuth0(string jwtToken, Uri jwksUri, string issuer, string audience,
        string? clientSecret, CancellationToken cancellationToken)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
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




    private async Task<ClaimsPrincipal?> GetClaimsFromTokenEntra(string jwtToken, Uri jwksUri, string issuer, string audience,
        string? clientSecret, CancellationToken cancellationToken)
    {

        var jwKeySet = await GetWebKeySetForDomain(jwksUri, cancellationToken);


        //this does look kinda crumby
        var domain = jwksUri.ToString().Replace(".well-known/openid-configuration", "");
        var jwksPath = new UriBuilder(domain + "discovery/v2.0/keys");

        // 1. Manually fetch the JWKS JSON string
       // using var httpClient = new HttpClient();
        //string jwksJson = await httpClient.GetStringAsync(jwksPath);

        // 2. Deserialize the JSON into a JsonWebKeySet
        //var jwks = new JsonWebKeySet(jwksJson);
       // var signingKeys = jwks.GetSigningKeys(); // Extracts the SecurityKeys

       var jwks = await GetWebKeySetForDomain(jwksPath.Uri, cancellationToken);
       var signingKeys = jwks.GetSigningKeys(); // Extracts the SecurityKeys

        // 3. Define your validation parameters
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            // The issuer for v2.0 tokens usually follows this pattern:
            ValidIssuer = $"{domain.EnsureEndsWith("/")}v2.0",
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            IssuerSigningKeys = signingKeys, // Use the keys manually loaded from the URL
            ValidateIssuerSigningKey = true
        };

        // 4. Perform the validation
        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(jwtToken, validationParameters, out var validatedToken);
            return principal;
        }
        catch (Exception ex)
        {
            throw new Exception($"Manual validation failed: {ex.Message}");
        }
    }
}