using System.Net;
using IIIF.Auth.V2;
using IIIF.Serialisation;
using IIIFAuth2.API.Tests.TestingInfrastructure;
using IIIFAuth2.Core.Data;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Tests.Common;

namespace IIIFAuth2.API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class ProbeServiceTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly HttpClient httpClient;
    private readonly AuthServicesContext dbContext;
    private readonly string connectionString;
    private static readonly string IpRole = Uri.EscapeDataString(DatabaseFixture.IpRestrictionRoleUri);

    public ProbeServiceTests(AuthWebApplicationFactory factory, DatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        connectionString = dbFixture.ConnectionString;
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .CreateClient();

        dbFixture.CleanUp();
    }
    
    [Fact]
    public async Task GetProbeService_Returns400StatusProperty_IfRolesMissing()
    {
        // Arrange
        const string path = "probe_internal/99/2/assetname";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(400, "BadRequest (400) expected");
    }
    
    [Fact]
    public async Task GetProbeService_Returns400StatusProperty_IfAssetIdInvalid()
    {
        // Arrange
        const string path = "probe_internal/12345?roles=hello";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(400, "BadRequest (400) expected");
    }

    [Fact]
    public async Task GetProbeService_Returns401StatusProperty_IfNoBearerToken()
    {
        // Arrange
        const string path = "probe_internal/99/2/foo?roles=clickthrough";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(401, "Unauthorized (401) expected");
        authProbeResult.Heading["en"].Should().ContainSingle(s => s == "Missing credentials");
        authProbeResult.Note["en"].Should().ContainSingle(s => s == "Authorising credentials not found");
    }
    
    [Fact]
    public async Task GetProbeService_Returns401StatusProperty_IfAuthHeaderProvided_NotBearerToken()
    {
        // Arrange
        const string path = "probe_internal/99/2/foo?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", "Basic amV3ZWxzOmJpbm9jdWxhcnM=");
            
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(401, "Unauthorized (401) expected");
        authProbeResult.Heading["en"].Should().ContainSingle(s => s == "Missing credentials");
        authProbeResult.Note["en"].Should().ContainSingle(s => s == "Authorising credentials not found");
    }

    [Fact]
    public async Task GetProbeService_Returns401StatusProperty_IfBearerTokenProvided_ButNotInDatabase()
    {
        // Arrange
        const string path = "probe_internal/99/2/foo?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", "Bearer foo-bar");
        
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(401, "Unauthorized (401) expected");
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
        
        const string path = "probe_internal/99/2/foo?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        
        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(401, "Unauthorized (401) expected");
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
        
        const string path = "probe_internal/99/2/foo?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        
        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(401, "Unauthorized (401) expected");
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
        
        const string path = "probe_internal/99/2/foo?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        
        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(403, "Forbidden (403) expected");
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
        
        const string path = "probe_internal/99/2/foo?roles=clickthrough,foo";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        
        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(200, "OK (200) expected");
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
        
        const string path = "probe_internal/99/2/foo?roles=clickthrough,foo";
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
        
        const string path = "probe_internal/99/2/foo?roles=clickthrough,foo";
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
        
        const string path = "probe_internal/99/2/foo?roles=clickthrough,foo";
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
        
        const string path = "probe_internal/99/2/foo?roles=clickthrough,foo";
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

    [Fact]
    public async Task GetAdjunctProbeService_Returns400StatusProperty_IfRolesMissing()
    {
        // Arrange
        const string path = "probe_internal/99/2/assetname/adjunct-1";

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(400, "BadRequest (400) expected");
    }

    [Fact]
    public async Task GetAdjunctProbeService_Returns400StatusProperty_IfAssetIdInvalid()
    {
        // Arrange
        const string path = "probe_internal/not-a-customer/2/assetname/adjunct-1?roles=hello";

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(400, "BadRequest (400) expected");
    }

    [Fact]
    public async Task GetAdjunctProbeService_Returns401StatusProperty_IfNoBearerToken()
    {
        // Arrange
        const string path = "probe_internal/99/2/foo/adjunct-1?roles=clickthrough";

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(401, "Unauthorized (401) expected");
        authProbeResult.Heading["en"].Should().ContainSingle(s => s == "Missing credentials");
        authProbeResult.Note["en"].Should().ContainSingle(s => s == "Authorising credentials not found");
    }

    [Fact]
    public async Task GetAdjunctProbeService_Returns401StatusProperty_IfBearerTokenProvided_ButNotInDatabase()
    {
        // Arrange
        const string path = "probe_internal/99/2/foo/adjunct-1?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", "Bearer foo-bar");

        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(401, "Unauthorized (401) expected");
        authProbeResult.Heading["en"].Should().ContainSingle(s => s == "Invalid credentials");
        authProbeResult.Note["en"].Should().ContainSingle(s => s == "Authorising credentials invalid");
    }

    [Fact]
    public async Task GetAdjunctProbeService_Returns401StatusProperty_IfBearerTokenProvidedForExpiredSession()
    {
        // Arrange
        const string accessToken =
            nameof(GetAdjunctProbeService_Returns401StatusProperty_IfBearerTokenProvidedForExpiredSession);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(accessToken, expires: DateTime.UtcNow.AddMinutes(-10)));
        await dbContext.SaveChangesAsync();

        const string path = "probe_internal/99/2/foo/adjunct-1?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(401, "Unauthorized (401) expected");
        authProbeResult.Heading["en"].Should().ContainSingle(s => s == "Expired session");
        authProbeResult.Note["en"].Should().ContainSingle(s => s == "Session has expired");
    }

    [Fact]
    public async Task GetAdjunctProbeService_Returns403StatusProperty_IfBearerTokenValid_ButMissingRequiredRoles()
    {
        // Arrange
        const string accessToken =
            nameof(GetAdjunctProbeService_Returns403StatusProperty_IfBearerTokenValid_ButMissingRequiredRoles);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(accessToken));
        await dbContext.SaveChangesAsync();

        const string path = "probe_internal/99/2/foo/adjunct-1?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(403, "Forbidden (403) expected");
        authProbeResult.Heading["en"].Should().ContainSingle(s => s == "Forbidden");
        authProbeResult.Note["en"].Should().ContainSingle(s => s == "Session does not have required roles");
    }

    [Fact]
    public async Task GetAdjunctProbeService_Returns200StatusProperty_IfBearerTokenValid_AndHasRequiredRoles()
    {
        // Arrange
        const string accessToken =
            nameof(GetAdjunctProbeService_Returns200StatusProperty_IfBearerTokenValid_AndHasRequiredRoles);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(accessToken));
        await dbContext.SaveChangesAsync();

        const string path = "probe_internal/99/2/foo/adjunct-1?roles=clickthrough,foo";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(200, "OK (200) expected");
        authProbeResult.Heading.Should().BeNull();
        authProbeResult.Note.Should().BeNull();
    }

    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("2001:db8:1007::1")]
    [InlineData("::ffff:10.0.0.5")]
    public async Task GetProbeService_Returns200StatusProperty_IfNoBearerToken_AndClientIpInRange(string clientIp)
    {
        // Arrange
        var path = $"probe_internal/99/2/foo?roles={IpRole}";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Forwarded-For", clientIp);

        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(200, "OK (200) expected");
        authProbeResult.Heading.Should().BeNull();
        authProbeResult.Note.Should().BeNull();
    }

    [Fact]
    public async Task GetProbeService_Returns200StatusProperty_IfBearerTokenInvalid_AndClientIpInRange()
    {
        // Arrange
        var path = $"probe_internal/99/2/foo?roles=foo,{IpRole}";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", "Bearer not-a-known-token");
        request.Headers.Add("X-Forwarded-For", "10.0.0.5");

        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(200, "OK (200) expected");
    }

    [Theory]
    [InlineData("10.0.1.5")]
    [InlineData("2001:db8:1008::1")]
    [InlineData(null)]
    public async Task GetProbeService_Returns403StatusProperty_IfOnlyIpRoles_AndClientIpNotInRange(string? clientIp)
    {
        // Arrange
        var path = $"probe_internal/99/2/foo?roles={IpRole}";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (clientIp != null) request.Headers.Add("X-Forwarded-For", clientIp);

        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(403, "Forbidden (403) expected");
        authProbeResult.Heading["en"].Should().ContainSingle(s => s == "Not available at your location");
        authProbeResult.Note["en"].Should().ContainSingle(s => s == "Only available in the Research Centre");
    }

    [Fact]
    public async Task GetProbeService_Returns403StatusProperty_WithGenericNote_IfUseGenericLocationWording()
    {
        // Arrange
        var client = CreateGenericLocationWordingClient(connectionString);
        var request = new HttpRequestMessage(HttpMethod.Get, $"probe_internal/99/2/foo?roles={IpRole}");
        request.Headers.Add("X-Forwarded-For", "10.0.1.5");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(403, "Forbidden (403) expected");
        authProbeResult.Heading["en"].Should().ContainSingle(s => s == "Not available at your location");
        authProbeResult.Note["en"].Should().ContainSingle(s => s == "Only available from permitted locations");
    }

    [Fact]
    public async Task GetProbeService_Returns401StatusProperty_IfIpAndOtherRoles_NoBearerToken_AndClientIpNotInRange()
    {
        // Arrange
        var path = $"probe_internal/99/2/foo?roles=foo,{IpRole}";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Forwarded-For", "10.0.1.5");

        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(401, "Unauthorized (401) expected");
        authProbeResult.Heading["en"].Should().ContainSingle(s => s == "Missing credentials");
    }

    [Fact]
    public async Task GetProbeService_Returns200StatusProperty_IfIpAndOtherRoles_BearerTokenValid_AndClientIpNotInRange()
    {
        // Arrange
        const string accessToken =
            nameof(GetProbeService_Returns200StatusProperty_IfIpAndOtherRoles_BearerTokenValid_AndClientIpNotInRange);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(accessToken));
        await dbContext.SaveChangesAsync();

        var path = $"probe_internal/99/2/foo?roles=foo,{IpRole}";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Headers.Add("X-Forwarded-For", "10.0.1.5");

        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        var authProbeResult = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        authProbeResult.Status.Should().Be(200, "OK (200) expected");
    }

    private static HttpClient CreateGenericLocationWordingClient(string connectionString)
        => new AuthWebApplicationFactory()
            .WithConnectionString(connectionString)
            .WithConfigValue("UseGenericLocationWording", "true")
            .CreateClient();

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