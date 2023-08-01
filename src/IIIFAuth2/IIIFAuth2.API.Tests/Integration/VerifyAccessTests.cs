using System.Net;
using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Tests.TestingInfrastructure;

namespace IIIFAuth2.API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class VerifyAccessTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly HttpClient httpClient;
    private readonly AuthServicesContext dbContext;

    public VerifyAccessTests(AuthWebApplicationFactory factory, DatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .CreateClient();

        dbFixture.CleanUp();
    }
    
    [Fact]
    public async Task VerifyAccess_Returns400_IfRolesMissing()
    {
        // Arrange
        const string path = "verifyaccess/99/2/assetname";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task VerifyAccess_Returns400_IfAssetIdInvalid()
    {
        // Arrange
        const string path = "verifyaccess/12345?roles=hello";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task VerifyAccess_Returns401_IfNoCookie()
    {
        // Arrange
        const string path = "verifyaccess/99/2/foo?roles=clickthrough";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task VerifyAccess_Returns401_IfCookieProvided_InvalidFormat()
    {
        // Arrange
        const string path = "verifyaccess/99/2/foo?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", "dlcs-auth2-99=unexpected-value;");
            
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task VerifyAccess_Returns401_IfCookieProvidedWithId_ButIdNotInDatabase()
    {
        // Arrange
        const string path = "verifyaccess/99/2/foo?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", "dlcs-auth2-99=id=123456789;");
            
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task VerifyAccess_Returns401_IfCookieProvidedWithId_ButForDifferentCustomer()
    {
        // Arrange
        // Arrange
        const string cookieId =
            nameof(VerifyAccess_Returns401_IfCookieProvidedWithId_ButForDifferentCustomer);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId, customer: 10));
        await dbContext.SaveChangesAsync();
        
        const string path = "verifyaccess/99/2/foo?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
            
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task VerifyAccess_Returns403_IfCookieProvidedWithId_ButSessionDoesNotHaveRoles()
    {
        // Arrange
        // Arrange
        const string cookieId = nameof(VerifyAccess_Returns403_IfCookieProvidedWithId_ButSessionDoesNotHaveRoles);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId));
        await dbContext.SaveChangesAsync();
        
        const string path = "verifyaccess/99/2/foo?roles=foo";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
            
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
    
    [Fact]
    public async Task VerifyAccess_Returns401_IfCookieForExpiredSession()
    {
        // Arrange
        // Arrange
        const string cookieId = nameof(VerifyAccess_Returns401_IfCookieForExpiredSession);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId, expires: DateTime.UtcNow.AddMinutes(-10)));
        await dbContext.SaveChangesAsync();
        
        const string path = "verifyaccess/99/2/foo?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
            
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task VerifyAccess_Returns200_IfCookieValid()
    {
        // Arrange
        // Arrange
        const string cookieId = nameof(VerifyAccess_Returns200_IfCookieValid);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId));
        await dbContext.SaveChangesAsync();
        
        const string path = "verifyaccess/99/2/foo?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
            
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
    
    [Fact]
    public async Task VerifyAccess_ExtendsCookie_IfCookieValid()
    {
        // Arrange
        // Arrange
        const string cookieId = nameof(VerifyAccess_ExtendsCookie_IfCookieValid);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId));
        await dbContext.SaveChangesAsync();
        
        const string path = "verifyaccess/99/2/foo?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
            
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        response.Headers.Should().ContainKey("Set-Cookie");
        var cookie = response.Headers.SingleOrDefault(header => header.Key == "Set-Cookie").Value.First();
        cookie.Should()
            .StartWith("dlcs-auth2-99")
            .And.Contain("samesite=none")
            .And.Contain("secure;");
    }

    [Fact]
    public async Task VerifyAccess_Returns200_ExtendsExpires_IfCookieValid_AndLastCheckedNull()
    {
        // Arrange
        const string cookieId = nameof(VerifyAccess_Returns200_ExtendsExpires_IfCookieValid_AndLastCheckedNull);
        var sessionUser = await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId));
        await dbContext.SaveChangesAsync();
        
        const string path = "verifyaccess/99/2/foo?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
        
        // Act
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        await dbContext.Entry(sessionUser.Entity).ReloadAsync();
        sessionUser.Entity.LastChecked.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task VerifyAccess_Returns200_ExtendsExpires_IfCookieValid_AndLastCheckedLongAgo()
    {
        // Arrange
        const string cookieId = nameof(VerifyAccess_Returns200_ExtendsExpires_IfCookieValid_AndLastCheckedLongAgo);
        var sessionUser =
            await dbContext.SessionUsers.AddAsync(
                CreateSessionUser(cookieId, lastChecked: DateTime.UtcNow.AddHours(-1)));
        await dbContext.SaveChangesAsync();
        
        const string path = "verifyaccess/99/2/foo?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
        
        // Act
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await dbContext.Entry(sessionUser.Entity).ReloadAsync();
        sessionUser.Entity.LastChecked.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task VerifyAccess_Returns200_DoesNotExtendsExpires_IfCookieValid_AndLastCheckedRecently()
    {
        // Arrange
        var lastChecked = DateTime.UtcNow.AddSeconds(-100);
        const string cookieId = nameof(VerifyAccess_Returns200_DoesNotExtendsExpires_IfCookieValid_AndLastCheckedRecently);
        var sessionUser =
            await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId, lastChecked: lastChecked));
        await dbContext.SaveChangesAsync();
        
        const string path = "verifyaccess/99/2/foo?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
        
        // Act
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await dbContext.Entry(sessionUser.Entity).ReloadAsync();
        sessionUser.Entity.LastChecked.Should().BeCloseTo(lastChecked, TimeSpan.FromMilliseconds(100));
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
            Roles = new List<string> { "clickthrough" },
            Expires = expires ?? DateTime.UtcNow.AddMinutes(5),
            Origin = origin,
            LastChecked = lastChecked
        };
}