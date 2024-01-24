using FakeItEasy;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.API.Tests.Infrastructure.Web;

public class UrlPathProviderTests
{
    private const string CurrentHost = "dlcs.test.example";
    private const string OtherHost = "dlcs.test.other";

    [Fact]
    public void GetGesturePostbackRelativePath_HandlesNoConfiguredDefault()
    {
        // Arrange
        var gestureTemplates = new Dictionary<string, string>
        {
            [OtherHost] = "/access/specific-host"
        };
        var sut = GetSut(CurrentHost, gestureTemplates);
        
        // Act
        var result = sut.GetGesturePostbackRelativePath(123);
        
        // Asset
        result.IsAbsoluteUri.Should().BeFalse();
        result.ToString().Should().Be("/access/123/gesture");
    }
    
    [Fact]
    public void GetGesturePostbackRelativePath_HandlesNoConfiguredDefault_WithPathBase()
    {
        // Arrange
        var gestureTemplates = new Dictionary<string, string>
        {
            [OtherHost] = "/access/specific-host"
        };
        var sut = GetSut(CurrentHost, gestureTemplates, "auth/v2/");
        
        // Act
        var result = sut.GetGesturePostbackRelativePath(123);
        
        // Asset
        result.IsAbsoluteUri.Should().BeFalse();
        result.ToString().Should().Be("auth/v2/access/123/gesture");
    }
    
    [Fact]
    public void GetGesturePostbackRelativePath_UsesConfiguredDefault()
    {
        // Arrange
        var gestureTemplates = new Dictionary<string, string>
        {
            ["Default"] = "/access/other",
            [OtherHost] = "/access/specific-host"
        };
        var sut = GetSut(CurrentHost, gestureTemplates);
        
        // Act
        var result = sut.GetGesturePostbackRelativePath(123);
        
        // Asset
        result.IsAbsoluteUri.Should().BeFalse();
        result.ToString().Should().Be("/access/other");
    }
    
    [Fact]
    public void GetGesturePostbackRelativePath_UsesSpecifiedHost_IfFound()
    {
        // Arrange
        var gestureTemplates = new Dictionary<string, string>
        {
            ["Default"] = "/access/other",
            [CurrentHost] = "/{customerId}/access/gesture"
        };
        var sut = GetSut(CurrentHost, gestureTemplates);
        
        // Act
        var result = sut.GetGesturePostbackRelativePath(123);
        
        // Asset
        result.IsAbsoluteUri.Should().BeFalse();
        result.ToString().Should().Be("/123/access/gesture");
    }

    [Fact]
    public void GetAccessServiceOAuthCallbackPath_Correct()
    {
        // Arrange
        var sut = GetSut(CurrentHost);
        var accessService = new AccessService { Customer = 99, Name = "ghosts" };
        var expected = new Uri("https://dlcs.test.example/auth/v2/access/99/ghosts/oauth2/callback");
        
        // Act
        var result = sut.GetAccessServiceOAuthCallbackPath(accessService);
        
        // Assert
        result.Should().BeEquivalentTo(expected);
    }
    
    private UrlPathProvider GetSut(string host, Dictionary<string, string>? gestureTemplates = null, string? pathBase = null)
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Host = new HostString(host);
        request.Scheme = "https";
        var contextAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => contextAccessor.HttpContext).Returns(context);

        var authSettings = new AuthSettings { GesturePathTemplateForDomain = gestureTemplates ?? new() };
        var apiSettings = Options.Create(new ApiSettings { Auth = authSettings, PathBase = pathBase });
        
        return new UrlPathProvider(contextAccessor, apiSettings);
    }
}