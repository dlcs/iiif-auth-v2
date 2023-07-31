using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Auth;
using IIIFAuth2.API.Settings;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.API.Tests.Infrastructure.Auth;

public class AuthAspectManagerTests
{
    private readonly IHttpContextAccessor contextAccessor;
    private readonly HttpRequest request;

    public AuthAspectManagerTests()
    {
        var context = new DefaultHttpContext();
        request = context.Request;
        contextAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => contextAccessor.HttpContext).Returns(context);
        request.Host = new HostString("test.example");
        request.Scheme = "https";
    }
    
    [Fact]
    public void GetAuthCookieKey_ReturnsExpected()
    {
        // Arrange
        const string cookieNameFormat = "id-{0}";
        const int customer = 99;
        const string expected = "id-99";

        var sut = GetSut();

        // Act
        var actual = sut.GetAuthCookieKey(cookieNameFormat, customer);

        // Assert
        actual.Should().Be(expected);
    }
    
    [Fact]
    public void GetCookieValueForId_ReturnsExpected()
    {
        // Arrange
        const string cookieId = "1212121212";
        const string expected = "id=1212121212";
        
        var sut = GetSut();

        // Act
        var actual = sut.GetCookieValueForId(cookieId);

        // Assert
        actual.Should().Be(expected);
    }
    
    [Fact]
    public void IssueCookie_AppendsCookieToResponse_WithCurrentDomain_IfUserCurrentDomainForCookieTrue()
    {
        // Arrange
        var sut = GetSut(useCurrentDomainForCookie: true);
        var cookieId = Guid.NewGuid().ToString();
        var authToken = new SessionUser
        {
            CookieId = cookieId,
            Customer = 99
        };
        
        // Act
        sut.IssueCookie(authToken);
        var cookie = contextAccessor.HttpContext.Response.Headers["Set-Cookie"].ToString();

        // Assert
        cookie.Should()
            .Contain($"id%3D{cookieId}")
            .And.Contain("domain=test.example;")
            .And.Contain("secure;")
            .And.Contain("samesite=none");
    }
    
    [Fact]
    public void IssueCookie_AppendsCookieToResponse_WithAdditionalDomains_IfSpecified()
    {
        // Arrange
        var sut = GetSut(useCurrentDomainForCookie: false, additionalDomains: "another.example");
        var cookieId = Guid.NewGuid().ToString();
        var authToken = new SessionUser
        {
            CookieId = cookieId,
            Customer = 99
        };
        
        // Act
        sut.IssueCookie(authToken);
        var cookie = contextAccessor.HttpContext.Response.Headers["Set-Cookie"].ToString();

        // Assert
        cookie.Should()
            .Contain($"id%3D{cookieId}")
            .And.Contain("domain=another.example;")
            .And.Contain("secure;")
            .And.Contain("samesite=none");
    }
    
    [Fact]
    public void IssueCookie_AppendsCookieToResponse_PerDomain()
    {
        // Arrange
        var sut = GetSut(useCurrentDomainForCookie: true, additionalDomains: "another.example");
        var cookieId = Guid.NewGuid().ToString();
        var authToken = new SessionUser
        {
            CookieId = cookieId,
            Customer = 99
        };
        
        // Act
        sut.IssueCookie(authToken);
        var cookies = contextAccessor.HttpContext.Response.Headers["Set-Cookie"];

        void ValidateCookie(string host, string cookie)
        {
            // Assert
            cookie.Should()
                .Contain($"id%3D{cookieId}")
                .And.Contain($"domain={host};")
                .And.Contain("secure;")
                .And.Contain("samesite=none");
        }
        
        ValidateCookie("another.example", cookies[0]);
        ValidateCookie("test.example", cookies[1]);
    }
    
    [Fact]
    public void GetCookieValueForCustomer_Null_IfNoCookies()
    {
        // Arrange
        var sut = GetSut();
        
        // Act
        var cookieValue = sut.GetCookieValueForCustomer(123);
        
        // Assert
        cookieValue.Should().BeNull();
    }
    
    [Fact]
    public void GetCookieValueForCustomer_Null_IfNoCookieForDifferentCustomer()
    {
        // Arrange
        var sut = GetSut();
        request.Headers.Append("Cookie", "auth-token-999=whatever");

        // Act
        var cookieValue = sut.GetCookieValueForCustomer(123);
        
        // Assert
        cookieValue.Should().BeNull();
    }
    
    [Fact]
    public void GetCookieValueForCustomer_ReturnsCookieValue_IfFound()
    {
        // Arrange
        var sut = GetSut();
        request.Headers.Append("Cookie", "auth-token-123=whatever");

        // Act
        var cookieValue = sut.GetCookieValueForCustomer(123);
        
        // Assert
        cookieValue.Should().Be("whatever");
    }
    
    [Fact]
    public void GetCookieIdFromValue_ReturnsExpected()
    {
        // Arrange
        const string cookieValue = "id=1212121212";
        const string expected = "1212121212";
        
        var sut = GetSut();

        // Act
        var actual = sut.GetCookieIdFromValue(cookieValue);

        // Assert
        actual.Should().Be(expected);
    }
    
    [Fact]
    public void GetCookieIdFromValue_Null_IfCookiesDoesNotStartAsExpected()
    {
        // Arrange
        const string cookieValue = "121id=2121212";

        var sut = GetSut();

        // Act
        var actual = sut.GetCookieIdFromValue(cookieValue);

        // Assert
        actual.Should().BeNull();
    }

    [Fact]
    public void GetAccessToken_Null_IfNoAuthHeader()
    {
        // Arrange
        var sut = GetSut();
        
        // Act
        var bearerToken = sut.GetAccessToken();
        
        // Assert
        bearerToken.Should().BeNull();
    }

    [Theory]
    [InlineData("basic Zm9vOmJhcg==")]
    [InlineData("digest username=\"Test\" algorithm=MD5")]
    [InlineData("negotiate foo")]
    [InlineData("random bar")]
    public void GetAccessToken_Null_IfAuthHeaderProvided_ButNotBearer(string authHeaderValue)
    {
        // Arrange
        var sut = GetSut();
        request.Headers.Append("Authorization", authHeaderValue);
        
        // Act
        var bearerToken = sut.GetAccessToken();
        
        // Assert
        bearerToken.Should().BeNull();
    }
    
    [Theory]
    [InlineData("bearer foo-bar")]
    [InlineData("Bearer foo-bar")]
    public void GetAccessToken_ReturnsValue_IfProvided(string authHeaderValue)
    {
        // Arrange
        var sut = GetSut();
        request.Headers.Append("Authorization", authHeaderValue);
        
        // Act
        var bearerToken = sut.GetAccessToken();
        
        // Assert
        bearerToken.Should().Be("foo-bar");
    }
    
    private AuthAspectManager GetSut(bool useCurrentDomainForCookie = true, params string[] additionalDomains)
    {
        var options = Options.Create(new AuthSettings
        {
            CookieDomains = additionalDomains.ToList(),
            CookieNameFormat = "auth-token-{0}",
            UseCurrentDomainForCookie = useCurrentDomainForCookie
        });
        return new AuthAspectManager(contextAccessor, options);
    }
}