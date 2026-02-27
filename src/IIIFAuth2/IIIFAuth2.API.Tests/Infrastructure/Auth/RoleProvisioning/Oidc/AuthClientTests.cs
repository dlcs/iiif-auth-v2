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
using Microsoft.Net.Http.Headers;

namespace IIIFAuth2.API.Tests.Infrastructure.Auth.RoleProvisioning.Oidc;

public class AuthClientTests
{
    private readonly AuthClient sut;
    private readonly IUrlPathProvider urlPathProvider;
    private readonly IJwtTokenHandler jwtTokenHandler;
    private readonly ClaimsConverter claimsConverter;
    private readonly ControllableHttpMessageHandler messageHandler;

    public AuthClientTests()
    {
        urlPathProvider = A.Fake<IUrlPathProvider>();
        A.CallTo(() => urlPathProvider.GetAccessServiceOAuthCallbackPath(A<AccessService>._))
            .Returns(new Uri("http://test.example/callback"));
        jwtTokenHandler = A.Fake<IJwtTokenHandler>();
        claimsConverter = new ClaimsConverter(new NullLogger<ClaimsConverter>());

        messageHandler = new ControllableHttpMessageHandler();

        sut = new AuthClient(urlPathProvider, new HttpClient(messageHandler), jwtTokenHandler, claimsConverter,
            new NullLogger<AuthClient>());
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void GetAuthLoginUrl_Correct_NullOrEmptyAdditionalScopes(string scopes)
    {
        // Arrange
        var accessService = new AccessService();
        var auth0Config = new OidcConfiguration
        {
            Domain = "https://dlcs-dev.uk.auth0.com",
            ClientId = "test-id",
            Scopes = scopes,
            Provider = "auth0"
        };
        
        var expected = new Uri("https://dlcs-dev.uk.auth0.com/authorize?client_id=test-id&redirect_uri=http%3A%2F%2Ftest.example%2Fcallback&response_type=code&state=foo&scope=openid");

        // Act
        var actual = sut.GetAuthLoginUrl(auth0Config, accessService, "foo");

        // Assert
        actual.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void GetAuthLoginUrl_Correct_AdditionalScopes()
    {
        // Arrange
        var accessService = new AccessService();
        var auth0Config = new OidcConfiguration
        {
            Domain = "https://dlcs-dev.uk.auth0.com",
            ClientId = "test-id",
            Scopes = "scope1,scope2,",
            Provider = "auth0"
        };
        A.CallTo(() => urlPathProvider.GetAccessServiceOAuthCallbackPath(accessService))
            .Returns(new Uri("http://test.example/callback"));
        var expected =
            new Uri(
                "https://dlcs-dev.uk.auth0.com/authorize?client_id=test-id&redirect_uri=http%3A%2F%2Ftest.example%2Fcallback&response_type=code&state=foo&scope=scope1 scope2 openid");

        // Act
        var actual = sut.GetAuthLoginUrl(auth0Config, accessService, "foo");

        // Assert
        actual.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task GetDlcsRolesForCode_MakesCorrectTokenExchangeRequest()
    {
        // Arrange
        var accessService = new AccessService();
        var auth0Config = new OidcConfiguration
        {
            Domain = "https://dlcs-dev.uk.auth0.com",
            ClientId = "test-id",
            ClientSecret = "test-secret",
            Provider = "auth0"
        };
        messageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.Forbidden));
        HttpRequestMessage request = null;
        messageHandler.RegisterCallback(message => request = message);

        var expectedData =
            "grant_type=authorization_code&client_id=test-id&client_secret=test-secret&code=12345&redirect_uri=http%3A%2F%2Ftest.example%2Fcallback";
        
        // Act
        await sut.GetDlcsRolesForCode(auth0Config, accessService, "12345", CancellationToken.None);
        
        // Assert
        var formData = await request.Content.ReadAsStringAsync();
        formData.Should().Be(expectedData);
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri.ToString().Should().Be("https://dlcs-dev.uk.auth0.com/oauth/token");
    }

    [Fact]
    public async Task GetDlcsRolesForCode_ReturnsEmptyList_IfTokenExchangeFails()
    {
        // Arrange
        var accessService = new AccessService();
        var auth0Config = new OidcConfiguration
        {
            Domain = "https://dlcs-dev.uk.auth0.com",
            ClientId = "test-id",
            ClientSecret = "test-secret",
            Provider = "auth0",
        };
        messageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.Forbidden));
        
        // Act
        var actual = await sut.GetDlcsRolesForCode(auth0Config, accessService, "12345", CancellationToken.None);
        
        // Assert
        messageHandler.CallsMade.Should().Contain(p => p == "https://dlcs-dev.uk.auth0.com/oauth/token");
        actual.Should().BeEmpty();
    }
    
    [Fact]
    public async Task GetDlcsRolesForCode_PassesIdTokenToTokenHandler()
    {
        // Arrange
        const string idToken = "my-id-token";
        var accessService = new AccessService();
        var auth0Config = new OidcConfiguration
        {
            Domain = "https://dlcs-dev.uk.auth0.com",
            ClientId = "test-id",
            ClientSecret = "test-secret",
            Provider = "auth0",
        };

        var jwksUri = new Uri("https://dlcs-dev.uk.auth0.com/.well-known/jwks.json");
        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponseMessage.Content =
            new StringContent("{ \"id_token\":\"my-id-token\" }", Encoding.UTF8, "application/json");
        messageHandler.SetResponse(httpResponseMessage);
        
        // Act
        await sut.GetDlcsRolesForCode(auth0Config, accessService, "12345", CancellationToken.None);
        
        // Assert
        A.CallTo(() => jwtTokenHandler.GetClaimsFromToken(idToken, jwksUri, "https://dlcs-dev.uk.auth0.com/", "test-id",
            "test-secret", "auth0",A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task GetDlcsRolesForCode_ReturnsEmptyRoles_IfNoMappedClaims()
    {
        // Arrange
        const string idToken = "my-id-token";
        var accessService = new AccessService();
        const string claimType = "http://test.claim";
        var auth0Config = new OidcConfiguration
        {
            Domain = "https://dlcs-dev.uk.auth0.com",
            ClientId = "test-id",
            ClientSecret = "test-secret",
            ClaimType = claimType,
            Provider = "auth0",
        };
        A.CallTo(() => jwtTokenHandler.GetClaimsFromToken(idToken, A<Uri>._, A<string>._, A<string>._,
                A<string>._, "auth0", A<CancellationToken>._))
            .Returns(new ClaimsPrincipal(new ClaimsIdentity()));
        
        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponseMessage.Content =
            new StringContent("{ \"id_token\":\"my-id-token\" }", Encoding.UTF8, "application/json");
        messageHandler.SetResponse(httpResponseMessage);
        
        // Act
        var actual = await sut.GetDlcsRolesForCode(auth0Config, accessService, "12345", CancellationToken.None);
        
        // Assert
        actual.Should().BeEmpty();
    }
    
    [Fact]
    public async Task GetDlcsRolesForCode_ReturnsMappedClaims()
    {
        // Arrange
        const string idToken = "my-id-token";
        var accessService = new AccessService();
        const string claimType = "http://test.claim";
        var mappedRoles = new[] { "https://test/role" };
        var auth0Config = new OidcConfiguration
        {
            Domain = "https://dlcs-dev.uk.auth0.com",
            ClientId = "test-id",
            ClientSecret = "test-secret",
            ClaimType = claimType,
            Provider = "auth0",
            Mapping = new Dictionary<string, string[]>
            {
                { "foobar", mappedRoles }
            }
        };
        A.CallTo(() => jwtTokenHandler.GetClaimsFromToken(idToken, A<Uri>._, A<string>._, A<string>._,
                A<string>._, "auth0",A<CancellationToken>._))
            .Returns(new ClaimsPrincipal(new ClaimsIdentity(new []{new Claim(claimType, "foobar")})));
        
        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponseMessage.Content =
            new StringContent("{ \"id_token\":\"my-id-token\" }", Encoding.UTF8, "application/json");
        messageHandler.SetResponse(httpResponseMessage);
        
        // Act
        var actual = await sut.GetDlcsRolesForCode(auth0Config, accessService, "12345", CancellationToken.None);
        
        // Assert
        actual.Should().BeEquivalentTo(mappedRoles);
    }
}