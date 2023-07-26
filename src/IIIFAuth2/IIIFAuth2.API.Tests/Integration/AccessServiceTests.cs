using System.Net;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using IIIFAuth2.API.Data;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Tests.Infrastructure;
using IIIFAuth2.API.Tests.TestingInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace IIIFAuth2.API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class AccessServiceTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly HttpClient httpClient;
    private readonly AuthServicesContext dbContext;

    public AccessServiceTests(AuthWebApplicationFactory factory, DatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .CreateClient();

        dbFixture.CleanUp();
    }
    
    [Fact]
    public async Task AccessService_Returns400_IfOriginNotProvided()
    {
        // Arrange
        const string path = "/access/1/foo";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task AccessService_Returns404_IfAccessServiceNotFound()
    {
        // Arrange
        const string path = "/access/1/foo?origin=http://whatever.here";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task AccessService_Clickthrough_RendersSignificantGestureView_IfDifferentHost()
    {
        // Arrange
        const string path = "/access/99/clickthrough?origin=http://whatever.here";
            
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var htmlParser = new HtmlParser();
        var document = htmlParser.ParseDocument(await response.Content.ReadAsStreamAsync());
        var label = document.QuerySelector("p") as IHtmlParagraphElement;
        label.TextContent.Should().Be("Test-Message");
    }
    
    [Fact]
    public async Task AccessService_Clickthrough_RendersSignificantGestureView_WithRoleProvisionToken_IfDifferentHost()
    {
        // Arrange
        const string path = "/access/99/clickthrough?origin=http://whatever.here";
            
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var htmlParser = new HtmlParser();
        var document = htmlParser.ParseDocument(await response.Content.ReadAsStreamAsync());

        var hidden = document.QuerySelector("form>#SingleUseToken") as IHtmlInputElement;
        var hiddenValue = hidden!.Value;
        
        ExpiringToken.HasExpired(hiddenValue).Should().BeFalse("A valid expiring token is returned");

        var token = await dbContext.RoleProvisionTokens.SingleAsync(t => t.Id == hiddenValue);
        token.Roles.Should().ContainSingle(DatabaseFixture.ClickthroughRoleUri);
        token.Used.Should().BeFalse();
    }
    
    [Fact]
    public async Task AccessService_Clickthrough_CreatesSessionAndSetsCookie_IfSameHost()
    {
        // Arrange
        var path = $"/access/99/clickthrough?origin={httpClient.BaseAddress}";
            
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Headers.Should().ContainKey("Set-Cookie");
        var cookie = response.Headers.SingleOrDefault(header => header.Key == "Set-Cookie").Value.First();
        cookie.Should()
            .StartWith("dlcs-auth2-99")
            .And.Contain("samesite=none")
            .And.Contain("secure;");
        
        // E.g. dlcs-token-99=id%3D76e7d9fb-99ab-4b4f-87b0-f2e3f0e9664e; expires=Tue, 14 Sep 2021 16:53:53 GMT; domain=localhost; path=/; secure; samesite=none
        var toRemoveLength = "dlcs-auth2-99id%3D".Length;
        var cookieId = cookie.Substring(toRemoveLength + 1, cookie.IndexOf(';') - toRemoveLength - 1);
        
        var authToken = await dbContext.SessionUsers.SingleAsync(at => at.CookieId == cookieId);
        authToken.Expires.Should().NotBeBefore(DateTime.UtcNow);
        authToken.Customer.Should().Be(99);
    }
    
    [Fact]
    public async Task AccessService_Clickthrough_RendersWindowClose_IfSameHost()
    {
        // Arrange
        var path = $"/access/99/clickthrough?origin={httpClient.BaseAddress}";
            
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var htmlParser = new HtmlParser();
        var document = htmlParser.ParseDocument(await response.Content.ReadAsStreamAsync());
        var label = document.QuerySelector("p") as IHtmlParagraphElement;
        label.TextContent.Should().Be("This window should close automatically...");
    }
}