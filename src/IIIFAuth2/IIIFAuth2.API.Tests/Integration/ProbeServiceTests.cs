using System.Net;
using IIIF.Auth.V2;
using IIIF.Serialisation;
using IIIFAuth2.API.Tests.TestingInfrastructure;
using IIIFAuth2.Core.Data;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Core.Models.Domain;
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
    private static readonly string AppointmentRole = Uri.EscapeDataString(DatabaseFixture.AppointmentRoleUri);
    private readonly DatabaseFixture dbFixture;

    public ProbeServiceTests(AuthWebApplicationFactory factory, DatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        this.dbFixture = dbFixture;
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
    [Fact]
    public async Task GetProbeService_Returns401_WithAppointmentNote_IfAppointmentRole_NoBearerToken()
    {
        // Arrange
        var path = $"probe_internal/99/2/foo?roles={AppointmentRole}";

        // Act
        var authProbeResult = await GetProbe(path);

        // Assert
        authProbeResult.Status.Should().Be(401);
        authProbeResult.Heading["en"].Should().ContainSingle(s => s == "Missing credentials");
        authProbeResult.Note["en"].Should()
            .ContainSingle(s => s == "Authorising credentials not found, or enter your appointment key");
    }

    [Fact]
    public async Task GetProbeService_Returns401_AppointmentRequired_IfSessionHasNoAppointment()
    {
        // Arrange
        const string accessToken = nameof(GetProbeService_Returns401_AppointmentRequired_IfSessionHasNoAppointment);
        await AddSessionWithAppointment(accessToken, null);
        var path = $"probe_internal/99/2/foo?roles={AppointmentRole}";

        // Act
        var authProbeResult = await GetProbe(path, accessToken);

        // Assert
        authProbeResult.Status.Should().Be(401);
        authProbeResult.Heading["en"].Should().ContainSingle(s => s == "Appointment required");
        authProbeResult.Note["en"].Should().ContainSingle(s => s == "Enter your appointment key");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetProbeService_Returns200_IfSessionHasAppointment_PatternsEmptyOrCustomerAndSpaceOnly(
        bool customerAndSpace)
    {
        // Arrange
        var accessToken = $"{nameof(GetProbeService_Returns200_IfSessionHasAppointment_PatternsEmptyOrCustomerAndSpaceOnly)}{customerAndSpace}";
        await AddSessionWithAppointment(accessToken, a =>
        {
            if (customerAndSpace) a.AssetPatterns = new AssetPatterns { Customer = [99], Space = [2] };
        });
        var path = $"probe_internal/99/2/foo?roles={AppointmentRole}";

        // Act
        var authProbeResult = await GetProbe(path, accessToken);

        // Assert
        authProbeResult.Status.Should().Be(200);
    }

    [Fact]
    public async Task GetProbeService_Returns200_IfSessionHasRole_EvenWithoutAppointment()
    {
        // Arrange
        const string accessToken = nameof(GetProbeService_Returns200_IfSessionHasRole_EvenWithoutAppointment);
        await AddSessionWithAppointment(accessToken, null);
        var path = $"probe_internal/99/2/foo?roles=foo,{AppointmentRole}";

        // Act
        var authProbeResult = await GetProbe(path, accessToken);

        // Assert
        authProbeResult.Status.Should().Be(200);
    }

    public static TheoryData<string, Action<Appointment>, string, string> AppointmentDenials => new()
    {
        {
            "not-started", a => a.Start = new DateTime(2099, 1, 2, 9, 30, 0, DateTimeKind.Utc),
            "Your appointment has not started", "Your appointment starts at 2099-01-02 09:30:00Z"
        },
        {
            "not-active", a => a.Active = false,
            "Appointment not yet approved", "Your appointment is awaiting approval"
        },
        {
            "ended", a => a.End = new DateTime(2020, 5, 6, 17, 0, 0, DateTimeKind.Utc),
            "Your appointment has ended", "Your appointment ended at 2020-05-06 17:00:00Z"
        },
        {
            "string1", a => a.AssetPatterns = new AssetPatterns { String1 = ["LIDDLE/*"] },
            "Not part of your appointment", "This item is not included in your appointment"
        },
        {
            "space", a => a.AssetPatterns = new AssetPatterns { Space = [3] },
            "Not part of your appointment", "This item is not included in your appointment"
        },
    };

    [Theory]
    [MemberData(nameof(AppointmentDenials))]
    public async Task GetProbeService_Returns403_WithReason_IfAppointmentDoesNotGrantAccess(string name,
        Action<Appointment> configure, string heading, string note)
    {
        // Arrange
        var accessToken = $"{nameof(GetProbeService_Returns403_WithReason_IfAppointmentDoesNotGrantAccess)}{name}";
        await AddSessionWithAppointment(accessToken, configure);
        var path = $"probe_internal/99/2/foo?roles={AppointmentRole}";

        // Act
        var authProbeResult = await GetProbe(path, accessToken);

        // Assert
        authProbeResult.Status.Should().Be(403);
        authProbeResult.Heading["en"].Should().ContainSingle(s => s == heading);
        authProbeResult.Note["en"].Should().ContainSingle(s => s == note);
    }

    private async Task<AuthProbeResult2> GetProbe(string path, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (accessToken != null) request.Headers.Add("Authorization", $"Bearer {accessToken}");
        var response = await httpClient.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
    }

    /// <summary>
    /// Add a session with "foo" role, holding an appointment for the appointment role (unless configure is null)
    /// </summary>
    private async Task AddSessionWithAppointment(string accessToken, Action<Appointment>? configure)
    {
        var session = CreateSessionUser(accessToken);
        session.CookieId = accessToken;
        if (configure != null)
        {
            var appointment = new Appointment
            {
                Id = Guid.NewGuid(),
                Customer = 99,
                RoleProviderId = dbFixture.AppointmentRoleProviderId,
                Token = accessToken,
                Roles = [DatabaseFixture.AppointmentRoleUri],
                Start = DateTime.UtcNow.AddHours(-1),
                End = DateTime.UtcNow.AddHours(1),
                Active = true,
            };
            configure(appointment);
            session.SessionAppointments.Add(new SessionAppointment { Appointment = appointment });
        }

        await using var context = dbFixture.CreateNewAuthServiceContext();
        await context.SessionUsers.AddAsync(session);
        await context.SaveChangesAsync();
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"roles\":[]}")]
    [InlineData("{\"roles\":[\" \"]}")]
    [InlineData("{\"roles\":\"not-an-array\"}")]
    [InlineData("not-json")]
    [InlineData("")]
    public async Task PostProbeService_Returns400StatusProperty_IfRolesMissingOrBodyInvalid(string body)
    {
        // Act
        var authProbeResult = await PostProbe("probe_internal/99/2/foo", body);

        // Assert
        authProbeResult.Status.Should().Be(400, "BadRequest (400) expected");
        authProbeResult.Heading["en"].Should().ContainSingle(s => s == "Bad Request");
    }

    [Fact]
    public async Task PostProbeService_Returns400StatusProperty_IfAssetIdInvalid()
    {
        var authProbeResult = await PostProbe("probe_internal/12345", "{\"roles\":[\"foo\"]}");

        authProbeResult.Status.Should().Be(400, "BadRequest (400) expected");
    }

    [Fact]
    public async Task PostProbeService_Returns401StatusProperty_IfNoBearerToken()
    {
        var authProbeResult = await PostProbe("probe_internal/99/2/foo",
            $"{{\"roles\":[\"{DatabaseFixture.AppointmentRoleUri}\"]}}");

        authProbeResult.Status.Should().Be(401);
        authProbeResult.Note["en"].Should()
            .ContainSingle(s => s == "Authorising credentials not found, or enter your appointment key");
    }

    [Fact]
    public async Task PostProbeService_Returns200StatusProperty_IfBearerTokenValid_AndHasRequiredRoles()
    {
        // Arrange
        const string accessToken = nameof(PostProbeService_Returns200StatusProperty_IfBearerTokenValid_AndHasRequiredRoles);
        await AddSessionWithAppointment(accessToken, null);

        // Act
        var authProbeResult = await PostProbe("probe_internal/99/2/foo/adjunct", "{\"roles\":[\"bar\",\"foo\"]}",
            accessToken);

        // Assert
        authProbeResult.Status.Should().Be(200);
    }

    [Theory]
    [InlineData("LIDDLE/WW2/HFIU/003", 200)]
    [InlineData("LIDDLE/WW2/HFIU", 403)]
    [InlineData("LIDDLE/WW2/EVAC/014", 403)]
    public async Task PostProbeService_EvaluatesAppointmentPatterns_AgainstBodyAsset(string string1, int expected)
    {
        // Arrange
        var accessToken = $"{nameof(PostProbeService_EvaluatesAppointmentPatterns_AgainstBodyAsset)}{string1}";
        await AddSessionWithAppointment(accessToken,
            a => a.AssetPatterns = new AssetPatterns { Space = [2], String1 = ["LIDDLE/WW2/HFIU/*"] });
        var body = $"{{\"roles\":[\"{DatabaseFixture.AppointmentRoleUri}\"],\"asset\":{{\"string1\":\"{string1}\"}}}}";

        // Act
        var authProbeResult = await PostProbe("probe_internal/99/2/foo", body, accessToken);

        // Assert
        authProbeResult.Status.Should().Be(expected);
        if (expected == 403)
        {
            authProbeResult.Heading["en"].Should().ContainSingle(s => s == "Not part of your appointment");
        }
    }

    [Fact]
    public async Task PostProbeService_MatchesAllAssetFields()
    {
        // Arrange
        const string accessToken = nameof(PostProbeService_MatchesAllAssetFields);
        await AddSessionWithAppointment(accessToken, a => a.AssetPatterns = new AssetPatterns
        {
            Scopes = ["b76hr45e"], Tags = ["ww2"], String1 = ["a"], String2 = ["b*"], String3 = ["c"],
            Number1 = [1], Number2 = [2], Number3 = [3]
        });
        var body = $$"""
                     {"roles":["{{DatabaseFixture.AppointmentRoleUri}}"],
                      "asset":{"space":2,"scopes":["other","b76hr45e"],"tags":["ww2"],"string1":"a","string2":"bee",
                               "string3":"c","number1":1,"number2":2,"number3":3} }
                     """;

        // Act
        var authProbeResult = await PostProbe("probe_internal/99/2/foo", body, accessToken);

        // Assert
        authProbeResult.Status.Should().Be(200);
    }

    [Theory]
    [InlineData("scopes", "{\"scopes\":[\"b76hr45e\"]}", 200)]
    [InlineData("manifests", "{\"manifests\":[\"b76hr45e\"]}", 200)]
    [InlineData("scopes-preferred", "{\"scopes\":[\"other\"],\"manifests\":[\"b76hr45e\"]}", 403)]
    [InlineData("manifests-ignored", "{\"scopes\":[\"b76hr45e\"],\"manifests\":[\"other\"]}", 200)]
    public async Task PostProbeService_ReadsScopes_OrManifestsIfNoScopes(string name, string asset, int expected)
    {
        // Arrange
        var accessToken = $"{nameof(PostProbeService_ReadsScopes_OrManifestsIfNoScopes)}{name}";
        await AddSessionWithAppointment(accessToken,
            a => a.AssetPatterns = new AssetPatterns { Scopes = ["b76hr45e"] });
        var body = $"{{\"roles\":[\"{DatabaseFixture.AppointmentRoleUri}\"],\"asset\":{asset}}}";

        // Act
        var authProbeResult = await PostProbe("probe_internal/99/2/foo", body, accessToken);

        // Assert
        authProbeResult.Status.Should().Be(expected);
    }

    private async Task<AuthProbeResult2> PostProbe(string path, string body, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
        if (accessToken != null) request.Headers.Add("Authorization", $"Bearer {accessToken}");
        var response = await httpClient.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
    }
}
