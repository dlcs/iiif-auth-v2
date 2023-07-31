using System.Net;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Tests.TestingInfrastructure;

namespace IIIFAuth2.API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class AccessTokenServiceTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly HttpClient httpClient;
    private readonly AuthServicesContext dbContext;

    public AccessTokenServiceTests(AuthWebApplicationFactory factory, DatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .CreateClient();

        dbFixture.CleanUp();
    }
    
    [Fact]
    public async Task AccessService_Returns200_WithInvalidRequestErrorProfile_IfMessageIdNotProvided()
    {
        // Arrange
        const string path = "/access/99/token?origin=http://whatever.example";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await ValidateResponse(response, expectedProfile: "invalidRequest");
    }
    
    [Fact]
    public async Task AccessService_Returns200_WithInvalidRequestErrorProfile_IfOriginNotProvided()
    {
        // Arrange
        const string path = "/access/99/token?messageId=12345";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await ValidateResponse(response, expectedProfile: "invalidRequest");
    }

    [Fact]
    public async Task AccessService_Returns200_WithMissingAspectErrorProfile_IfNoCookieProvided()
    {
        // Arrange
        const string path = "/access/99/token?messageId=12345&origin=http://localhost";
        
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await ValidateResponse(response, expectedProfile: "missingAspect");
    }
    
    [Fact]
    public async Task AccessService_Returns200_WithInvalidAspectErrorProfile_IfCookieProvided_ButInvalidFormat()
    {
        // Arrange
        const string path = "/access/99/token?messageId=12345&origin=http://localhost";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", "dlcs-auth2-99=unexpected-value;");
        
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await ValidateResponse(response, expectedProfile: "invalidAspect");
    }

    [Fact]
    public async Task AccessService_Returns200_WithInvalidAspectErrorProfile_IfCookieProvidedWithId_ButIdNotInDatabase()
    {
        // Arrange
        const string path = "/access/99/token?messageId=12345&origin=http://localhost";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", "dlcs-auth2-99=id=123456789;");
        
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await ValidateResponse(response, expectedProfile: "invalidAspect");
    }
    
    [Fact]
    public async Task AccessService_Returns200_WithInvalidAspectErrorProfile_IfCookieProvidedWithId_ButForDifferentCustomer()
    {
        // Arrange
        const string cookieId =
            nameof(AccessService_Returns200_WithInvalidAspectErrorProfile_IfCookieProvidedWithId_ButForDifferentCustomer);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId, customer: 10));
        await dbContext.SaveChangesAsync();
        
        const string path = "/access/99/token?messageId=12345&origin=http://localhost";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
        
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await ValidateResponse(response, expectedProfile: "invalidAspect");
    }
    
    [Fact]
    public async Task AccessService_Returns200_WithExpiredAspectErrorProfile_IfCookieForExpiredSession()
    {
        // Arrange
        const string cookieId =
            nameof(AccessService_Returns200_WithExpiredAspectErrorProfile_IfCookieForExpiredSession);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId, expires: DateTime.UtcNow.AddMinutes(-10)));
        await dbContext.SaveChangesAsync();
        
        const string path = "/access/99/token?messageId=12345&origin=http://localhost";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
        
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await ValidateResponse(response, expectedProfile: "expiredAspect");
    }
    
    [Fact]
    public async Task AccessService_Returns200_WithInvalidOriginErrorProfile_IfCookieForDifferentOrigin()
    {
        // Arrange
        const string cookieId =
            nameof(AccessService_Returns200_WithInvalidOriginErrorProfile_IfCookieForDifferentOrigin);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId));
        await dbContext.SaveChangesAsync();
        
        const string path = "/access/99/token?messageId=12345&origin=http://whatever.here";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
        
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await ValidateResponse(response, expectedProfile: "invalidOrigin");
    }

    [Fact]
    public async Task AccessService_Returns200_WithAccessToken_IfCookieValid()
    {
        // Arrange
        const string cookieId = nameof(AccessService_Returns200_WithAccessToken_IfCookieValid);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId));
        await dbContext.SaveChangesAsync();
        
        const string path = "/access/99/token?messageId=12345&origin=http://localhost";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
        
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await ValidateResponse(response, expectedAccessToken: "found-access-token");
    }

    [Fact]
    public async Task AccessService_Returns200_ExtendsExpires_IfCookieValid_AndLastCheckedNull()
    {
        // Arrange
        const string cookieId = nameof(AccessService_Returns200_ExtendsExpires_IfCookieValid_AndLastCheckedNull);
        var sessionUser = await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId));
        await dbContext.SaveChangesAsync();
        
        const string path = "/access/99/token?messageId=12345&origin=http://localhost";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
        
        // Act
        await httpClient.SendAsync(request);
        
        // Assert
        await dbContext.Entry(sessionUser.Entity).ReloadAsync();
        sessionUser.Entity.LastChecked.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task AccessService_Returns200_ExtendsExpires_IfCookieValid_AndLastCheckedLongAgo()
    {
        // Arrange
        const string cookieId = nameof(AccessService_Returns200_ExtendsExpires_IfCookieValid_AndLastCheckedLongAgo);
        var sessionUser =
            await dbContext.SessionUsers.AddAsync(
                CreateSessionUser(cookieId, lastChecked: DateTime.UtcNow.AddHours(-1)));
        await dbContext.SaveChangesAsync();
        
        const string path = "/access/99/token?messageId=12345&origin=http://localhost";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
        
        // Act
        await httpClient.SendAsync(request);
        
        // Assert
        await dbContext.Entry(sessionUser.Entity).ReloadAsync();
        sessionUser.Entity.LastChecked.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task AccessService_Returns200_DoesNotExtendsExpires_IfCookieValid_AndLastCheckedRecently()
    {
        // Arrange
        var lastChecked = DateTime.UtcNow.AddSeconds(-100);
        const string cookieId = nameof(AccessService_Returns200_DoesNotExtendsExpires_IfCookieValid_AndLastCheckedRecently);
        var sessionUser =
            await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId, lastChecked: lastChecked));
        await dbContext.SaveChangesAsync();
        
        const string path = "/access/99/token?messageId=12345&origin=http://localhost";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
        
        // Act
        await httpClient.SendAsync(request);
        
        // Assert
        await dbContext.Entry(sessionUser.Entity).ReloadAsync();
        sessionUser.Entity.LastChecked.Should().Be(lastChecked);
    }

    private static async Task ValidateResponse(HttpResponseMessage response, string? expectedProfile = null,
        string? expectedAccessToken = null)
    {
        var htmlParser = new HtmlParser();
        var document = htmlParser.ParseDocument(await response.Content.ReadAsStreamAsync());
        var el = document.QuerySelector("script") as IHtmlScriptElement;

        if (!string.IsNullOrEmpty(expectedProfile))
        {
            el.Text.Should().Contain("\"type\": \"AuthAccessTokenError2\"",
                "result is AuthAccessTokenError2");
            el.Text.Should().Contain($"\"profile\": \"{expectedProfile}\"",
                $"expected profile is '{expectedProfile}'");
        }

        if (!string.IsNullOrEmpty(expectedAccessToken))
        {
            el.Text.Should().Contain("\"type\": \"AuthAccessToken2\"",
                "result is AuthAccessToken2");
            el.Text.Should().Contain($"\"accessToken\": \"{expectedAccessToken}\"");
            el.Text.Should().Contain("\"messageId\": \"12345\"");
        }
    }

    private static SessionUser CreateSessionUser(string cookieId, int customer = 99, string origin = "http://localhost/",
        DateTime? expires = null, DateTime? lastChecked = null)
        => new()
        {
            Id = Guid.NewGuid(),
            CookieId = cookieId,
            AccessToken = "found-access-token",
            Customer = customer,
            Created = DateTime.UtcNow,
            Roles = new List<string> { "foo" },
            Expires = expires ?? DateTime.UtcNow.AddMinutes(5),
            Origin = origin,
            LastChecked = lastChecked
        };
}