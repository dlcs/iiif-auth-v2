using System.Net;
using IIIF.Auth.V2;
using IIIF.Serialisation;
using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Tests.TestingInfrastructure;

namespace IIIFAuth2.API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class ProbeServiceTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly HttpClient httpClient;
    private readonly AuthServicesContext dbContext;

    public ProbeServiceTests(AuthWebApplicationFactory factory, DatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .CreateClient();

        dbFixture.CleanUp();
    }
    
    [Fact]
    public async Task GetProbeService_Returns400StatusProperty_IfRolesMissing()
    {
        // Arrange
        const string path = "probe/99/2/assetname";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(400);
    }
    
    [Fact]
    public async Task GetProbeService_Returns400StatusProperty_IfAssetIdInvalid()
    {
        // Arrange
        const string path = "probe/12345?roles=hello";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(400);
    }

    [Fact]
    public async Task GetProbeService_Returns401StatusProperty_IfNoBearerToken()
    {
        // Arrange
        const string path = "probe/99/2/foo?roles=clickthrough";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(401);
        authProbeResult.Heading["en"].Should().ContainSingle(s => s == "Missing credentials");
        authProbeResult.Note["en"].Should().ContainSingle(s => s == "Authorising credentials not found");
    }
    
    [Fact]
    public async Task GetProbeService_Returns401StatusProperty_IfAuthHeaderProvided_NotBearerToken()
    {
        // Arrange
        const string path = "probe/99/2/foo?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", "Basic amV3ZWxzOmJpbm9jdWxhcnM=");
            
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(401);
        authProbeResult.Heading["en"].Should().ContainSingle(s => s == "Missing credentials");
        authProbeResult.Note["en"].Should().ContainSingle(s => s == "Authorising credentials not found");
    }

    [Fact]
    public async Task GetProbeService_Returns401StatusProperty_IfBearerTokenProvided_ButNotInDatabase()
    {
        // Arrange
        const string path = "probe/99/2/foo?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", "Bearer foo-bar");
        
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(401);
        authProbeResult.Heading["en"].Should().ContainSingle(s => s == "Invalid credentials");
        authProbeResult.Note["en"].Should().ContainSingle(s => s == "Authorising credentials invalid");
    }
    
    [Fact]
    public async Task GetProbeService_Returns401StatusProperty_IfBearerTokenProvided_ButForDifferentCustomer()
    {
        // Arrange
        const string accessToken =
            nameof(GetProbeService_Returns401StatusProperty_IfBearerTokenProvided_ButForDifferentCustomer);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(accessToken, customer: 10));
        await dbContext.SaveChangesAsync();
        
        const string path = "probe/99/2/foo?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        
        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(401);
        authProbeResult.Heading["en"].Should().ContainSingle(s => s == "Invalid credentials");
        authProbeResult.Note["en"].Should().ContainSingle(s => s == "Authorising credentials invalid");
    }
    
    [Fact]
    public async Task GetProbeService_Returns401StatusProperty_IfBearerTokenProvidedForExpiredSession()
    {
        // Arrange
        const string accessToken =
            nameof(GetProbeService_Returns401StatusProperty_IfBearerTokenProvidedForExpiredSession);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(accessToken, expires: DateTime.UtcNow.AddMinutes(-10)));
        await dbContext.SaveChangesAsync();
        
        const string path = "probe/99/2/foo?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        
        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(401);
        authProbeResult.Heading["en"].Should().ContainSingle(s => s == "Expired session");
        authProbeResult.Note["en"].Should().ContainSingle(s => s == "Session has expired");
    }

    [Fact]
    public async Task GetProbeService_Returns403StatusProperty_IfBearerTokenValid_ButMissingRequiredRoles()
    {
        // Arrange
        const string accessToken =
            nameof(GetProbeService_Returns403StatusProperty_IfBearerTokenValid_ButMissingRequiredRoles);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(accessToken));
        await dbContext.SaveChangesAsync();
        
        const string path = "probe/99/2/foo?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        
        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(403);
        authProbeResult.Heading["en"].Should().ContainSingle(s => s == "Forbidden");
        authProbeResult.Note["en"].Should().ContainSingle(s => s == "Session does not have required roles");
    }
    
    [Fact]
    public async Task GetProbeService_Returns200StatusProperty_IfBearerTokenValid_AndHasRequiredRoles()
    {
        // Arrange
        const string accessToken =
            nameof(GetProbeService_Returns200StatusProperty_IfBearerTokenValid_AndHasRequiredRoles);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(accessToken));
        await dbContext.SaveChangesAsync();
        
        const string path = "probe/99/2/foo?roles=clickthrough,foo";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        
        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(200);
        authProbeResult.Heading.Should().BeNull();
        authProbeResult.Note.Should().BeNull();
    }
    
    [Fact]
    public async Task GetProbeService_Returns200StatusProperty_ExtendsExpires_IfBearerTokenValid_AndLastCheckedNull()
    {
        // Arrange
        const string accessToken =
            nameof(GetProbeService_Returns200StatusProperty_ExtendsExpires_IfBearerTokenValid_AndLastCheckedNull);
        var sessionUser = await dbContext.SessionUsers.AddAsync(CreateSessionUser(accessToken));
        await dbContext.SaveChangesAsync();
        
        const string path = "probe/99/2/foo?roles=clickthrough,foo";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        
        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await dbContext.Entry(sessionUser.Entity).ReloadAsync();
        sessionUser.Entity.LastChecked.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(3));
    }
    
    [Fact]
    public async Task GetProbeService_Returns200StatusProperty_ExtendsExpires_IfBearerTokenValid_AndLastCheckedLongAgo()
    {
        // Arrange
        const string accessToken =
            nameof(GetProbeService_Returns200StatusProperty_ExtendsExpires_IfBearerTokenValid_AndLastCheckedLongAgo);
        var sessionUser =
            await dbContext.SessionUsers.AddAsync(CreateSessionUser(accessToken,
                lastChecked: DateTime.UtcNow.AddHours(-1)));
        await dbContext.SaveChangesAsync();
        
        const string path = "probe/99/2/foo?roles=clickthrough,foo";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        
        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await dbContext.Entry(sessionUser.Entity).ReloadAsync();
        sessionUser.Entity.LastChecked.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(3));
    }
    
    [Fact]
    public async Task GetProbeService_Returns200_DoesNotExtendsExpires_IfBearerTokenValid_AndLastCheckedRecently()
    {
        // Arrange
        var lastChecked = DateTime.UtcNow.AddSeconds(-100);
        const string accessToken =
            nameof(GetProbeService_Returns200_DoesNotExtendsExpires_IfBearerTokenValid_AndLastCheckedRecently);
        var sessionUser =
            await dbContext.SessionUsers.AddAsync(CreateSessionUser(accessToken, lastChecked: lastChecked));
        await dbContext.SaveChangesAsync();
        
        const string path = "probe/99/2/foo?roles=clickthrough,foo";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        
        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await dbContext.Entry(sessionUser.Entity).ReloadAsync();
        sessionUser.Entity.LastChecked.Should().BeCloseTo(lastChecked, TimeSpan.FromMilliseconds(100));
    }
    
    [Fact]
    public async Task GetProbeService_ExtendsCookie_IfBearerTokenValid()
    {
        // Arrange
        const string accessToken = nameof(GetProbeService_ExtendsCookie_IfBearerTokenValid);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(accessToken));
        await dbContext.SaveChangesAsync();
        
        const string path = "probe/99/2/foo?roles=clickthrough,foo";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        
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

    private static SessionUser CreateSessionUser(string accessToken, int customer = 99, DateTime? expires = null,
        DateTime? lastChecked = null)
        => new()
        {
            Id = Guid.NewGuid(),
            CookieId = "cookie-id",
            AccessToken = accessToken,
            Customer = customer,
            Created = DateTime.UtcNow,
            Roles = new List<string> { "foo" },
            Expires = expires ?? DateTime.UtcNow.AddMinutes(5),
            Origin = "http://irrelevant-for-this/",
            LastChecked = lastChecked
        };
}