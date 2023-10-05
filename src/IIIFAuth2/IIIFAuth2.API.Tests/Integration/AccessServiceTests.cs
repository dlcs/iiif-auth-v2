using System.Net;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Models.Domain;
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
        token.Origin.Should().Be("http://whatever.here/");
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
    public async Task AccessService_Clickthrough_CreatesSessionAndSetsCookie_IfSameHost_ObeysXForwardedProto()
    {
        // Arrange
        var baseAddress = httpClient.BaseAddress!.ToString().Replace("http://", "https://");
        var path = $"/access/99/clickthrough?origin={baseAddress}";
            
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("x-forwarded-proto", "https");
        var response = await httpClient.SendAsync(request);
        
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
    public async Task AccessService_Clickthrough_CreatesSessionAndSetsCookie_IfDifferentHost_ButMatchesCookieHost()
    {
        // Arrange
        var path = $"/access/99/clickthrough?origin=https://{DatabaseFixture.CookieDomain}";
            
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
    public async Task AccessService_Clickthrough_CreatesSessionAndSetsCookie_IfDifferentHost_ButSubdomainOfCookieHost()
    {
        // Arrange
        var path = $"/access/99/clickthrough?origin=https://iiif.{DatabaseFixture.CookieDomain}";
            
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
        var label = document.QuerySelector("p:nth-child(2)") as IHtmlParagraphElement;
        label.TextContent.Should().Be("This window should close automatically...");
    }

    [Fact]
    public async Task SignificantGesture_Returns400_IfNoSingleUseToken()
    {
        // Arrange
        const string path = "/access/gesture";
            
        // Act
        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>()
        });
        var response = await httpClient.PostAsync(path, formContent);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignificantGesture_RendersWindowClose_WithError_IfTokenExpired()
    {
        // Arrange
        const string path = "/access/gesture";
        var expiredToken = ExpiringToken.GenerateNewToken(DateTime.UtcNow.AddHours(-1));
            
        // Act
        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("singleUseToken", expiredToken)
        });
        var response = await httpClient.PostAsync(path, formContent);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        await ValidateWindowCloseWithError(response);
    }
    
    [Fact]
    public async Task SignificantGesture_RendersWindowClose_WithError_IfTokenValidButNotInDatabase()
    {
        // Arrange
        const string path = "/access/gesture";
        var expiredToken = ExpiringToken.GenerateNewToken(DateTime.UtcNow.AddHours(1));
            
        // Act
        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("singleUseToken", expiredToken)
        });
        var response = await httpClient.PostAsync(path, formContent);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        await ValidateWindowCloseWithError(response);
    }
    
    [Fact]
    public async Task SignificantGesture_RendersWindowClose_WithError_IfTokenValidButUsed()
    {
        // Arrange
        const string path = "/access/gesture";
        var validToken = ExpiringToken.GenerateNewToken(DateTime.UtcNow.AddHours(1));
        await dbContext.RoleProvisionTokens.AddAsync(CreateToken(validToken, true, Array.Empty<string>()));
        await dbContext.SaveChangesAsync();
            
        // Act
        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("singleUseToken", validToken)
        });
        var response = await httpClient.PostAsync(path, formContent);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        await ValidateWindowCloseWithError(response);
    }
    
    [Fact]
    public async Task SignificantGesture_CreatesSession_AndSetsCookie_AndMarksTokenAsUsed()
    {
        // Arrange
        const string path = "/access/gesture";
        var validToken = ExpiringToken.GenerateNewToken(DateTime.UtcNow.AddHours(1));
        var roles = new string[] { DatabaseFixture.ClickthroughRoleUri };
        var tokenEntity = await dbContext.RoleProvisionTokens.AddAsync(CreateToken(validToken, false, roles));
        var beforeVersion = tokenEntity.Entity.Version;
        await dbContext.SaveChangesAsync();
        
        // Act
        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("singleUseToken", validToken)
        });
        var response = await httpClient.PostAsync(path, formContent);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Headers.Should().ContainKey("Set-Cookie");
        var cookie = response.Headers.Single(header => header.Key == "Set-Cookie").Value.First();
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
        authToken.Roles.Should().BeEquivalentTo(roles);
        authToken.Origin.Should().Be("http://localhost");
        
        await dbContext.Entry(tokenEntity.Entity).ReloadAsync();
        tokenEntity.Entity.Used.Should().BeTrue("token is now used");
        tokenEntity.Entity.Version.Should().NotBe(beforeVersion);
    }

    [Fact]
    public async Task Logout_Returns204_IfNoCookieProvided()
    {
        // Arrange
        const string path = "/access/99/clickthrough/logout";
        
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
    
    [Fact]
    public async Task Logout_Returns204_IfCookieProvided_ButInvalidFormat()
    {
        // Arrange
        const string path = "/access/99/clickthrough/logout";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", "dlcs-auth2-99=unexpected-value;");
        
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
    
    [Fact]
    public async Task Logout_Returns204_IfCookieProvidedWithId_ButIdNotInDatabase()
    {
        // Arrange
        const string path = "/access/99/clickthrough/logout";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", "dlcs-auth2-99=id=123456789;");
        
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
    
    [Fact]
    public async Task Logout_Returns204_AndMarksAsExpired_IfCookieValid_AndInDatabase()
    {
        // Arrange
        const string cookieId = nameof(Logout_Returns204_AndMarksAsExpired_IfCookieValid_AndInDatabase);
        var sessionUser =
            await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId, expires: DateTime.UtcNow.AddHours(1)));
        await dbContext.SaveChangesAsync();
        
        const string path = "/access/99/clickthrough/logout";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
        
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await dbContext.Entry(sessionUser.Entity).ReloadAsync();
        sessionUser.Entity.LastChecked.Should().HaveValue();
        sessionUser.Entity.Expires.Should().BeBefore(DateTime.UtcNow);
    }
    
    [Fact]
    public async Task Logout_SetsExpiredCookie_IfCookieValid()
    {
        // Arrange
        const string cookieId = nameof(Logout_SetsExpiredCookie_IfCookieValid);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId));
        await dbContext.SaveChangesAsync();
        
        const string path = "/access/99/clickthrough/logout";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
        
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        response.Headers.Should().ContainKey("Set-Cookie");
        var cookie = response.Headers.SingleOrDefault(header => header.Key == "Set-Cookie").Value.First();

        var expires = DateTime.Parse(cookie.Split(';').Single(c => c.Trim().StartsWith("expires")).Split('=')[1]);
        expires.Should().BeBefore(DateTime.UtcNow);
    }

    private static async Task ValidateWindowCloseWithError(HttpResponseMessage response)
    {
        var htmlParser = new HtmlParser();
        var document = htmlParser.ParseDocument(await response.Content.ReadAsStreamAsync());
        var err = document.QuerySelector("p") as IHtmlParagraphElement;
        err.TextContent.Should().Be("Token invalid or expired");
        var label = document.QuerySelector("p:nth-child(2)") as IHtmlParagraphElement;
        label.TextContent.Should().Be("This window should close automatically...");
    }

    private static RoleProvisionToken CreateToken(string token, bool used, string[] roles)
        => new()
        {
            Id = token, Created = DateTime.UtcNow, Customer = 99, Roles = roles.ToList(), Used = used,
            Origin = "http://localhost"
        };
    
    private static SessionUser CreateSessionUser(string cookieId, int customer = 99, DateTime? expires = null)
        => new()
        {
            Id = Guid.NewGuid(),
            CookieId = cookieId,
            AccessToken = "found-access-token",
            Customer = 99,
            Created = DateTime.UtcNow,
            Roles = new List<string> { "foo" },
            Expires = expires ?? DateTime.UtcNow.AddMinutes(5),
            Origin = "http://localhost/",
            LastChecked = null
        };
}