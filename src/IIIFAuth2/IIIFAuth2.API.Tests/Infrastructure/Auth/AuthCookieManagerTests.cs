using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Auth;
using IIIFAuth2.API.Settings;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.API.Tests.Infrastructure.Auth;

public class AuthCookieManagerTests
{
    private readonly IHttpContextAccessor contextAccessor;
    private readonly HttpRequest request;

    public AuthCookieManagerTests()
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
    
    private AuthCookieManager GetSut(bool useCurrentDomainForCookie = true, params string[] additionalDomains)
    {
        var options = Options.Create(new AuthSettings
        {
            CookieDomains = additionalDomains.ToList(),
            CookieNameFormat = "auth-token-{0}",
            UseCurrentDomainForCookie = useCurrentDomainForCookie
        });
        return new AuthCookieManager(contextAccessor, options);
    }
}