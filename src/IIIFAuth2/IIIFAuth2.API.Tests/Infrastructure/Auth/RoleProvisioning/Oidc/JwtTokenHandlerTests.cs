using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Oidc;
using IIIFAuth2.API.Settings;
using IIIFAuth2.API.Tests.TestingInfrastructure;
using LazyCache;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IIIFAuth2.API.Tests.Infrastructure.Auth.RoleProvisioning.Oidc;

public class JwtTokenHandlerTests
{
    private readonly ControllableHttpMessageHandler messageHandler;
    private readonly JwtTokenHandler sut;

    public JwtTokenHandlerTests()
    {
        messageHandler = new ControllableHttpMessageHandler();
        IAppCache appCache1 = new CachingService();

        sut = new JwtTokenHandler(
            new HttpClient(messageHandler),
            appCache1,
            Options.Create(new AuthSettings { JwksTtl = 600 }),
            new NullLogger<JwtTokenHandler>());
    }

    [Fact]
    public async Task GetClaimsFromToken_ValidRs256Token_ReturnsPrincipal()
    {
        // Arrange
        const string issuer = "https://issuer.example";
        const string audience = "test-aud";
        var (token, jwksJson) = CreateRs256Token(issuer, audience, "user-1");
        var jwksUri = new Uri($"https://issuer.example/{Guid.NewGuid()}/jwks.json");
        messageHandler.SetResponse(messageHandler.GetResponseMessage(jwksJson, HttpStatusCode.OK));

        // Act
        var actual = await sut.GetClaimsFromToken(token, jwksUri, issuer, audience, null, "provider", CancellationToken.None);

        // Assert
        actual.Should().NotBeNull();
        actual!.FindFirst("sub")!.Value.Should().Be("user-1");
        messageHandler.CallsMade.Should().Contain(jwksUri.ToString());
    }

    [Fact]
    public async Task GetClaimsFromToken_ValidHs256Token_UsesClientSecret()
    {
        // Arrange
        const string issuer = "https://issuer.example";
        const string audience = "test-aud";
        const string clientSecret = "super-secret-key-for-tests-12345";
        var token = CreateHs256Token(issuer, audience, clientSecret, "user-2");
        var jwksUri = new Uri($"https://issuer.example/{Guid.NewGuid()}/jwks.json");
        messageHandler.SetResponse(messageHandler.GetResponseMessage("{\"keys\":[]}", HttpStatusCode.OK));

        // Act
        var actual = await sut.GetClaimsFromToken(token, jwksUri, issuer, audience, clientSecret, "provider", CancellationToken.None);

        // Assert
        actual.Should().NotBeNull();
        actual!.FindFirst("sub")!.Value.Should().Be("user-2");
        messageHandler.CallsMade.Should().Contain(jwksUri.ToString());
    }

    [Fact]
    public async Task GetClaimsFromToken_InvalidAudience_ReturnsNull()
    {
        // Arrange
        const string issuer = "https://issuer.example";
        const string audience = "test-aud";
        var (token, jwksJson) = CreateRs256Token(issuer, audience, "user-3");
        var jwksUri = new Uri($"https://issuer.example/{Guid.NewGuid()}/jwks.json");
        messageHandler.SetResponse(messageHandler.GetResponseMessage(jwksJson, HttpStatusCode.OK));

        // Act
        var actual = await sut.GetClaimsFromToken(token, jwksUri, issuer, "wrong-aud", null, "provider", CancellationToken.None);

        // Assert
        actual.Should().BeNull();
    }

    [Fact]
    public async Task GetClaimsFromToken_InvalidSignature_ReturnsNull()
    {
        // Arrange
        const string issuer = "https://issuer.example";
        const string audience = "test-aud";
        var (token, _) = CreateRs256Token(issuer, audience, "user-4");
        var (_, mismatchedJwks) = CreateRs256Token(issuer, audience, "user-5");
        var jwksUri = new Uri($"https://issuer.example/{Guid.NewGuid()}/jwks.json");
        messageHandler.SetResponse(messageHandler.GetResponseMessage(mismatchedJwks, HttpStatusCode.OK));

        // Act
        var actual = await sut.GetClaimsFromToken(token, jwksUri, issuer, audience, null, "provider", CancellationToken.None);

        // Assert
        actual.Should().BeNull();
        messageHandler.CallsMade.Should().Contain(jwksUri.ToString());
    }

    [Fact]
    public async Task GetClaimsFromToken_MalformedToken_ReturnsNull()
    {
        // Arrange
        const string issuer = "https://issuer.example";
        const string audience = "test-aud";
        var jwksUri = new Uri($"https://issuer.example/{Guid.NewGuid()}/jwks.json");
        messageHandler.SetResponse(messageHandler.GetResponseMessage("{\"keys\":[]}", HttpStatusCode.OK));

        // Act
        var actual = await sut.GetClaimsFromToken("not-a-token", jwksUri, issuer, audience, null, "provider", CancellationToken.None);

        // Assert
        actual.Should().BeNull();
    }

    private static (string token, string jwksJson) CreateRs256Token(string issuer, string audience, string subject)
    {
        using var rsa = RSA.Create(2048);
        var publicParameters = rsa.ExportParameters(false);
        var keyId = Guid.NewGuid().ToString();
        var rsaSecurityKey = new RsaSecurityKey(rsa) { KeyId = keyId };
        var credentials = new SigningCredentials(rsaSecurityKey, SecurityAlgorithms.RsaSha256);
        var jwt = new JwtSecurityToken(
            issuer,
            audience,
            [new Claim("sub", subject)],
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(5),
            credentials);
        var token = new JwtSecurityTokenHandler().WriteToken(jwt);

        var jwksJson =
            $"{{\"keys\":[{{\"kty\":\"RSA\",\"kid\":\"{keyId}\",\"use\":\"sig\",\"alg\":\"RS256\",\"e\":\"{Base64UrlEncoder.Encode(publicParameters.Exponent)}\",\"n\":\"{Base64UrlEncoder.Encode(publicParameters.Modulus)}\"}}]}}";

        // Sanity check JWKS can validate the token with default parameters (issuer/audience checked later)
        var localParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = new JsonWebKeySet(jwksJson).GetSigningKeys(),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false
        };
        new JwtSecurityTokenHandler().ValidateToken(token, localParams, out _);
        return (token, jwksJson);
    }

    private static string CreateHs256Token(string issuer, string audience, string secret, string subject)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer,
            audience,
            [new Claim("sub", subject)],
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(5),
            credentials);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

}
