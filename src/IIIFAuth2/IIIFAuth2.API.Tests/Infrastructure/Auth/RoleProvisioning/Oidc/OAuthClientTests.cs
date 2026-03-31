using System.Net;
using System.Security.Claims;
using System.Text;
using FakeItEasy;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Oidc;
using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Tests.TestingInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;


namespace IIIFAuth2.API.Tests.Infrastructure.Auth.RoleProvisioning.Oidc;

public class OAuthClientTests
{
    private readonly OAuthClient sut;
    private readonly IUrlPathProvider urlPathProvider;
    private readonly IJwtTokenHandler jwtTokenHandler;
    private readonly ControllableHttpMessageHandler messageHandler;

    public const string EntraDomain = "https://login.microsoftonline.com/bc970ba1-ef24-4df6-9e2e-1e94873f345a";
    public const string Auth0Domain = "https://dlcs-dev.uk.auth0.com";

    public OAuthClientTests()
    {
        urlPathProvider = A.Fake<IUrlPathProvider>();
        A.CallTo(() => urlPathProvider.GetAccessServiceOAuthCallbackPath(A<AccessService>._))
            .Returns(new Uri("http://test.example/callback"));
        jwtTokenHandler = A.Fake<IJwtTokenHandler>();
        var claimsConverter1 = new ClaimsConverter(new NullLogger<ClaimsConverter>());

        messageHandler = new ControllableHttpMessageHandler();

        sut = new OAuthClient(urlPathProvider, new HttpClient(messageHandler), jwtTokenHandler, claimsConverter1,
            new NullLogger<OAuthClient>());

    }

    public static IEnumerable<object?[]> EmptyScopesCases()
    {
        yield return [Auth0Domain, $"{Auth0Domain}/authorize", $"{Auth0Domain}/oauth/token", $"{Auth0Domain}/.well-known/jwks.json", "auth0", ""];
        yield return [Auth0Domain, $"{Auth0Domain}/authorize", $"{Auth0Domain}/oauth/token", $"{Auth0Domain}/.well-known/jwks.json", "auth0", null];
        yield return [EntraDomain, $"{EntraDomain}/oauth2/v2.0/authorize", $"{EntraDomain}/oauth2/v2.0/token", $"{EntraDomain}/discovery/v2.0/keys", "entra", ""];
        yield return [EntraDomain, $"{EntraDomain}/oauth2/v2.0/authorize", $"{EntraDomain}/oauth2/v2.0/token", $"{EntraDomain}/discovery/v2.0/keys", "entra", null];
    }

    [Theory]
    [MemberData(nameof(EmptyScopesCases))]
    public async Task GetAuthLoginUrl_Correct_NullOrEmptyAdditionalScopes(string domain, string authorizeEndpoint, string tokenEndpoint, string jwks, string provider, string? scopes)
    {
        // Arrange
        var accessService = new AccessService();
        var authConfig = new OidcConfiguration
        {
            Domain = domain,
            ClientId = "test-id",
            Scopes = scopes,
            Provider = provider
        };
        var metadata = GetMetadataJson(authorizeEndpoint, tokenEndpoint, domain.TrimEnd('/') + "/", jwks);
        messageHandler.SetResponse(messageHandler.GetResponseMessage(metadata, HttpStatusCode.OK));

        var expected = new Uri($"{authorizeEndpoint}?client_id=test-id&redirect_uri=http%3A%2F%2Ftest.example%2Fcallback&response_type=code&state=foo&scope=openid");

        // Act
        var actual = await sut.GetAuthLoginUrl(authConfig, accessService, "foo", CancellationToken.None);

        // Assert
        actual.Should().BeEquivalentTo(expected);
    }


    public static IEnumerable<object[]> AdditionalScopesCases()
    {
        yield return [
            Auth0Domain,
            $"{Auth0Domain}/authorize", 
            $"{Auth0Domain}/oauth/token", 
            $"{Auth0Domain}/.well-known/jwks.json", 
            "auth0"
        ];
        yield return [
            EntraDomain, 
            $"{EntraDomain}/oauth2/v2.0/authorize", 
            $"{EntraDomain}/oauth2/v2.0/token", 
            $"{EntraDomain}/discovery/v2.0/keys", 
            "entra"
        ];
    }

    [Fact]
    public async Task GetAuthLoginUrl_Throws_WhenAuthorizationEndpointMissing()
    {
        // Arrange
        var accessService = new AccessService();
        var authConfig = new OidcConfiguration
        {
            Domain = "https://dlcs-dev.uk.auth0.com",
            ClientId = "test-id",
            Provider = "auth0"
        };
        var metadataMissingAuthEndpoint = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        messageHandler.SetResponse(metadataMissingAuthEndpoint);

        // Act
        var act = () => sut.GetAuthLoginUrl(authConfig, accessService, "foo", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Check provider as authorization_endpoint can't be located");
    }

    [Theory]
    [MemberData(nameof(AdditionalScopesCases))]
    public async Task GetAuthLoginUrl_Correct_AdditionalScopes(string domain, string authorizeEndpoint, string tokenEndpoint, string jwks, string provider)
    {
        // Arrange
        var accessService = new AccessService();
        var authConfig = new OidcConfiguration
        {
            Domain = domain,
            ClientId = "test-id",
            Scopes = "scope1,scope2,",
            Provider = provider
        };
        A.CallTo(() => urlPathProvider.GetAccessServiceOAuthCallbackPath(accessService))
            .Returns(new Uri("http://test.example/callback"));
        var metadata = GetMetadataJson(authorizeEndpoint, tokenEndpoint, domain.TrimEnd('/') + "/", jwks);
        messageHandler.SetResponse(messageHandler.GetResponseMessage(metadata, HttpStatusCode.OK));
        var expected =
            new Uri(
                $"{authorizeEndpoint}?client_id=test-id&redirect_uri=http%3A%2F%2Ftest.example%2Fcallback&response_type=code&state=foo&scope=openid scope1 scope2");

        // Act
        var actual = await sut.GetAuthLoginUrl(authConfig, accessService, "foo", CancellationToken.None);

        // Assert
        actual.Should().BeEquivalentTo(expected);
    }



    public static IEnumerable<object[]> TokenRequestCases()
    {
        yield return new object[] { "https://dlcs-dev.uk.auth0.com", "https://dlcs-dev.uk.auth0.com/authorize", "https://dlcs-dev.uk.auth0.com/oauth/token", "auth0" };
        yield return new object[] { EntraDomain, $"{EntraDomain}/oauth2/v2.0/authorize", $"{EntraDomain}/oauth2/v2.0/token", "entra" };
    }

    [Theory]
    [MemberData(nameof(TokenRequestCases))]
    public async Task GetDlcsRolesForCode_MakesCorrectTokenExchangeRequest(string domain, string authorizeEndpoint, string tokenEndpoint, string provider)
    {
        // Arrange
        var accessService = new AccessService();
        var authConfig = new OidcConfiguration
        {
            Domain = domain,
            ClientId = "test-id",
            ClientSecret = "test-secret",
            Provider = provider
        };
        var metadata = GetMetadataJson(authorizeEndpoint, tokenEndpoint, domain.TrimEnd('/') + "/", tokenEndpoint.Contains("microsoftonline") ? $"{domain}/discovery/v2.0/keys" : "https://dlcs-dev.uk.auth0.com/.well-known/jwks.json");
        messageHandler.SetResponse(messageHandler.GetResponseMessage(metadata, HttpStatusCode.OK));
        messageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.Forbidden));
        HttpRequestMessage? request = null;
        messageHandler.RegisterCallback(message => request = message);
        const string expectedData = "grant_type=authorization_code&client_id=test-id&client_secret=test-secret&code=12345&redirect_uri=http%3A%2F%2Ftest.example%2Fcallback";
        
        // Act
        await sut.GetDlcsRolesForCode(authConfig, accessService, "12345", CancellationToken.None);
        
        // Assert
        var formData = await request?.Content?.ReadAsStringAsync()!;
        formData.Should().Be(expectedData);
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri?.ToString().Should().Be(tokenEndpoint);
    }


    [Theory]
    [MemberData(nameof(TokenRequestCases))]
    public async Task GetDlcsRolesForCode_ReturnsEmptyList_IfTokenExchangeFails(string domain, string authorizeEndpoint, string tokenEndpoint, string provider)
    {
        // Arrange
        var accessService = new AccessService();
        var authConfig = new OidcConfiguration
        {
            Domain = domain,
            ClientId = "test-id",
            ClientSecret = "test-secret",
            Provider = provider,
        };
        var metadata = GetMetadataJson(authorizeEndpoint, tokenEndpoint, domain.TrimEnd('/') + "/", tokenEndpoint.Contains("microsoftonline") ? $"{domain}/discovery/v2.0/keys" : "https://dlcs-dev.uk.auth0.com/.well-known/jwks.json");
        messageHandler.SetResponse(messageHandler.GetResponseMessage(metadata, HttpStatusCode.OK));
        messageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.Forbidden));
        
        // Act
        var actual = await sut.GetDlcsRolesForCode(authConfig, accessService, "12345", CancellationToken.None);
        
        // Assert
        messageHandler.CallsMade.Should().Contain(p => p == tokenEndpoint);
        actual.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDlcsRolesForCode_ReturnsEmpty_WhenTokenRequestThrows()
    {
        // Arrange
        var accessService = new AccessService();
        var authConfig = new OidcConfiguration
        {
            Domain = EntraDomain,
            ClientId = "test-id",
            ClientSecret = "test-secret",
            Provider = "entra",
        };
        var metadata = GetMetadataJson(
            $"{authConfig.Domain}/oauth2/v2.0/authorize",
            $"{authConfig.Domain}/oauth2/v2.0/token",
            $"{authConfig.Domain}/v2.0",
            $"{authConfig.Domain}/discovery/v2.0/keys");
        messageHandler.SetResponse(messageHandler.GetResponseMessage(metadata, HttpStatusCode.OK));
        messageHandler.RegisterCallback(request =>
        {
            if (request.RequestUri != null && request.RequestUri.ToString().EndsWith("/token"))
            {
                throw new InvalidOperationException("boom");
            }
        });

        // Act
        var actual = await sut.GetDlcsRolesForCode(authConfig, accessService, "12345", CancellationToken.None);

        // Assert
        actual.Should().BeEmpty();
        messageHandler.CallsMade.Should().Contain($"{authConfig.Domain}/oauth2/v2.0/token");
    }



    [Theory]
    [MemberData(nameof(TokenRequestCases))]
    public async Task GetDlcsRolesForCode_PassesIdTokenToTokenHandler(string domain, string authorizeEndpoint, string tokenEndpoint, string provider)
    {
        // Arrange
        const string idToken = "my-id-token";
        var accessService = new AccessService();
        var authConfig = new OidcConfiguration
        {
            Domain = domain,
            ClientId = "test-id",
            ClientSecret = "test-secret",
            Provider = provider,
        };

        var jwksUri = tokenEndpoint.Contains("microsoftonline")
            ? new Uri($"{domain}/discovery/v2.0/keys")
            : new Uri("https://dlcs-dev.uk.auth0.com/.well-known/jwks.json");
        var metadata = GetMetadataJson(authorizeEndpoint, tokenEndpoint, domain.TrimEnd('/') + "/", jwksUri.ToString());
        messageHandler.SetResponse(messageHandler.GetResponseMessage(metadata, HttpStatusCode.OK));
        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(GetTokenResponseJson(idToken), Encoding.UTF8, "application/json")
        };
        messageHandler.SetResponse(httpResponseMessage);

        A.CallTo(() => jwtTokenHandler.GetClaimsFromToken(idToken, A<Uri>._, A<string>._,
            "test-id", "test-secret", provider, A<CancellationToken>._))
            .Returns(Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "1") }))));        

        // Act
        await sut.GetDlcsRolesForCode(authConfig, accessService, "12345", CancellationToken.None);

        // Assert
        messageHandler.CallsMade.Should().Contain(tokenEndpoint);
    }


    [Theory]
    [MemberData(nameof(TokenRequestCases))]
    public async Task GetDlcsRolesForCode_ReturnsEmptyRoles_IfNoMappedClaims(string domain, string authorizeEndpoint, string tokenEndpoint, string provider)
    {
        // Arrange
        const string idToken = "my-id-token";
        var accessService = new AccessService();
        const string claimType = "http://test.claim";
        var authConfig = new OidcConfiguration
        {
            Domain = domain,
            ClientId = "test-id",
            ClientSecret = "test-secret",
            ClaimType = claimType,
            Provider = provider,
        };
        A.CallTo(() => jwtTokenHandler.GetClaimsFromToken(idToken, A<Uri>._, A<string>._, A<string>._,
                A<string>._, provider, A<CancellationToken>._))
            .Returns(Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal(new ClaimsIdentity())));
        var metadata = GetMetadataJson(authorizeEndpoint, tokenEndpoint, domain.TrimEnd('/') + "/", tokenEndpoint.Contains("microsoftonline") ? $"{domain}/discovery/v2.0/keys" : "https://dlcs-dev.uk.auth0.com/.well-known/jwks.json");
        messageHandler.SetResponse(messageHandler.GetResponseMessage(metadata, HttpStatusCode.OK));
        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(GetTokenResponseJson(idToken), Encoding.UTF8, "application/json")
        };
        messageHandler.SetResponse(httpResponseMessage);
        messageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));
        
        // Act
        var actual = await sut.GetDlcsRolesForCode(authConfig, accessService, "12345", CancellationToken.None);
        
        // Assert
        actual.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(TokenRequestCases))]
    public async Task GetDlcsRolesForCode_ReturnsEmpty_WhenClaimsPrincipalNull(string domain, string authorizeEndpoint, string tokenEndpoint, string provider)
    {
        // Arrange
        const string idToken = "my-id-token";
        var accessService = new AccessService();
        var authConfig = new OidcConfiguration
        {
            Domain = domain,
            ClientId = "test-id",
            ClientSecret = "test-secret",
            Provider = provider,
        };
        A.CallTo(() => jwtTokenHandler.GetClaimsFromToken(idToken, A<Uri>._, A<string>._, A<string>._,
                A<string>._, provider, A<CancellationToken>._))
            .Returns(Task.FromResult<ClaimsPrincipal?>(null));
        var metadata = GetMetadataJson(authorizeEndpoint, tokenEndpoint, domain.TrimEnd('/') + "/", tokenEndpoint.Contains("microsoftonline") ? $"{domain}/discovery/v2.0/keys" : "https://dlcs-dev.uk.auth0.com/.well-known/jwks.json");
        messageHandler.SetResponse(messageHandler.GetResponseMessage(metadata, HttpStatusCode.OK));
        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(GetTokenResponseJson(idToken), Encoding.UTF8, "application/json")
        };
        messageHandler.SetResponse(httpResponseMessage);
        messageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));
        
        // Act
        var actual = await sut.GetDlcsRolesForCode(authConfig, accessService, "12345", CancellationToken.None);
        
        // Assert
        actual.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDlcsRolesForCode_ReturnsEmpty_WhenMetadataFetchThrows()
    {
        // Arrange
        var accessService = new AccessService();
        var authConfig = new OidcConfiguration
        {
            Domain = EntraDomain,
            ClientId = "test-id",
            ClientSecret = "test-secret",
            Provider = "entra",
        };
        messageHandler.RegisterCallback(_ => throw new HttpRequestException("metadata fail"));

        // Act
        var actual = await sut.GetDlcsRolesForCode(authConfig, accessService, "12345", CancellationToken.None);

        // Assert
        actual.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(TokenRequestCases))]
    public async Task GetDlcsRolesForCode_ReturnsMappedClaims(string domain, string authorizeEndpoint, string tokenEndpoint, string provider)
    {
        // Arrange
        const string idToken = "my-id-token";
        var accessService = new AccessService();
        const string claimType = "http://test.claim";
        var mappedRoles = new[] { "https://test/role" };
        var authConfig = new OidcConfiguration
        {
            Domain = domain,
            ClientId = "test-id",
            ClientSecret = "test-secret",
            ClaimType = claimType,
            Provider = provider,
            Mapping = new Dictionary<string, string[]>
            {
                { "foobar", mappedRoles }
            }
        };
        A.CallTo(() => jwtTokenHandler.GetClaimsFromToken(A<string>._, A<Uri>._, A<string>._, A<string>._,
                A<string>._, provider, A<CancellationToken>._))
            .Returns(Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal(new ClaimsIdentity(new []{new Claim(claimType, "foobar")}))));
        var metadata = GetMetadataJson(authorizeEndpoint, tokenEndpoint, domain.TrimEnd('/') + "/", tokenEndpoint.Contains("microsoftonline") ? $"{domain}/discovery/v2.0/keys" : "https://dlcs-dev.uk.auth0.com/.well-known/jwks.json");
        messageHandler.SetResponse(messageHandler.GetResponseMessage(metadata, HttpStatusCode.OK));
        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(GetTokenResponseJson(idToken), Encoding.UTF8, "application/json")
        };
        messageHandler.SetResponse(httpResponseMessage);
        
        // Act
        var actual = await sut.GetDlcsRolesForCode(authConfig, accessService, "12345", CancellationToken.None);

        // Assert
        actual.Should().Contain(mappedRoles[0]);
    }

    private static string GetMetadataJson(string authEndpoint, string tokenEndpoint, string issuer, string jwksUri)
        => $"{{ \"authorization_endpoint\":\"{authEndpoint}\", \"token_endpoint\":\"{tokenEndpoint}\", \"issuer\":\"{issuer}\", \"jwks_uri\":\"{jwksUri}\" }}";

    private static string GetTokenResponseJson(string idToken)
        => $"{{ \"access_token\":\"at\", \"refresh_token\":\"rt\", \"id_token\":\"{idToken}\", \"token_type\":\"Bearer\", \"expires_in\":3600 }}";
}