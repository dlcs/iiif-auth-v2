using FakeItEasy;
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
    
    private UrlPathProvider GetSut(string host, Dictionary<string, string> gestureTemplates)
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Host = new HostString(host);
        request.Scheme = "https";
        var contextAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => contextAccessor.HttpContext).Returns(context);

        var authSettings = new AuthSettings { GesturePathTemplateForDomain = gestureTemplates };
        var apiSettings = Options.Create(new ApiSettings { Auth = authSettings });
        
        return new UrlPathProvider(contextAccessor, apiSettings);
    }
}