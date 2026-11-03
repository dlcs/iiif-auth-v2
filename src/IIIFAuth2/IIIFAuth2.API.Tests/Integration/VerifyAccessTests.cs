using System.Net;
using IIIFAuth2.API.Tests.TestingInfrastructure;
using IIIFAuth2.Core.Data;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Core.Models.Domain;
using IIIFAuth2.Tests.Common;

namespace IIIFAuth2.API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class VerifyAccessTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly HttpClient httpClient;
    private readonly AuthServicesContext dbContext;
    private readonly string connectionString;
    private static readonly string IpRole = Uri.EscapeDataString(DatabaseFixture.IpRestrictionRoleUri);
    private static readonly string AppointmentRole = Uri.EscapeDataString(DatabaseFixture.AppointmentRoleUri);
    private readonly DatabaseFixture dbFixture;

    /// <summary>
    /// KnownNetworks for proxy (e.g. Orchestrator) tests. Deliberately within the IpRestriction range (10.0.0.0/24) to
    /// verify that a proxy IP is never treated as the client IP, even if it's in a permitted range
    /// </summary>
    private const string ProxyNetwork = "10.0.0.0/28";
    private const string ProxyIp = "10.0.0.9";

    public VerifyAccessTests(AuthWebApplicationFactory factory, DatabaseFixture dbFixture)
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

    [Fact]
    public async Task VerifyAccessAdjunct_Returns400_IfRolesMissing()
    {
        // Arrange
        const string path = "verifyaccess/99/2/assetname/adjunct-1";

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VerifyAccessAdjunct_Returns400_IfAssetIdInvalid()
    {
        // Arrange
        const string path = "verifyaccess/not-a-customer/2/assetname/adjunct-1?roles=hello";

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VerifyAccessAdjunct_Returns401_IfNoCookie()
    {
        // Arrange
        const string path = "verifyaccess/99/2/foo/adjunct-1?roles=clickthrough";

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VerifyAccessAdjunct_Returns401_IfCookieProvidedWithId_ButIdNotInDatabase()
    {
        // Arrange
        const string path = "verifyaccess/99/2/foo/adjunct-1?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", "dlcs-auth2-99=id=123456789;");

        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VerifyAccessAdjunct_Returns401_IfCookieForExpiredSession()
    {
        // Arrange
        const string cookieId = nameof(VerifyAccessAdjunct_Returns401_IfCookieForExpiredSession);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId, expires: DateTime.UtcNow.AddMinutes(-10)));
        await dbContext.SaveChangesAsync();

        const string path = "verifyaccess/99/2/foo/adjunct-1?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");

        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VerifyAccessAdjunct_Returns403_IfCookieProvidedWithId_ButSessionDoesNotHaveRoles()
    {
        // Arrange
        const string cookieId = nameof(VerifyAccessAdjunct_Returns403_IfCookieProvidedWithId_ButSessionDoesNotHaveRoles);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId));
        await dbContext.SaveChangesAsync();

        const string path = "verifyaccess/99/2/foo/adjunct-1?roles=foo";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");

        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task VerifyAccessAdjunct_Returns200_IfCookieValid()
    {
        // Arrange
        const string cookieId = nameof(VerifyAccessAdjunct_Returns200_IfCookieValid);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId));
        await dbContext.SaveChangesAsync();

        const string path = "verifyaccess/99/2/foo/adjunct-1?roles=clickthrough";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");

        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("2001:db8:1007::1")]
    public async Task VerifyAccess_Returns200_IfNoCookie_AndClientIpInRange(string clientIp)
    {
        // Arrange
        var path = $"verifyaccess/99/2/foo?roles={IpRole}";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Forwarded-For", clientIp);

        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("10.0.1.5")]
    [InlineData(null)]
    public async Task VerifyAccess_Returns403_IfOnlyIpRoles_AndClientIpNotInRange(string? clientIp)
    {
        // Arrange
        var path = $"verifyaccess/99/2/foo?roles={IpRole}";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (clientIp != null) request.Headers.Add("X-Forwarded-For", clientIp);

        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task VerifyAccess_Returns401_IfIpAndOtherRoles_NoCookie_AndClientIpNotInRange()
    {
        // Arrange
        var path = $"verifyaccess/99/2/foo?roles=clickthrough,{IpRole}";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Forwarded-For", "10.0.1.5");

        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VerifyAccess_Returns200_IfIpAndOtherRoles_CookieValid_AndClientIpNotInRange()
    {
        // Arrange
        const string cookieId = nameof(VerifyAccess_Returns200_IfIpAndOtherRoles_CookieValid_AndClientIpNotInRange);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId));
        await dbContext.SaveChangesAsync();

        var path = $"verifyaccess/99/2/foo?roles=clickthrough,{IpRole}";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
        request.Headers.Add("X-Forwarded-For", "10.0.1.5");

        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("10.0.0.200, " + ProxyIp)]
    [InlineData("10.0.0.200")]
    public async Task VerifyAccess_Returns200_IfKnownNetworksConfigured_AndClientIpInRange(string forwardedFor)
    {
        // Arrange - client, proxy (e.g. Orchestrator via load balancer) or client only (direct)
        var client = CreateKnownNetworksClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"verifyaccess/99/2/foo?roles={IpRole}");
        request.Headers.Add("X-Forwarded-For", forwardedFor);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task VerifyAccess_Returns403_IfKnownNetworksConfigured_AndResolvedIpIsKnownProxy()
    {
        // Arrange - e.g. Orchestrator via load balancer without x-forwarded-for, load balancer adds Orchestrator IP.
        // Proxy IP is within IpRestriction range but must not be treated as client
        var client = CreateKnownNetworksClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"verifyaccess/99/2/foo?roles={IpRole}");
        request.Headers.Add("X-Forwarded-For", ProxyIp);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task VerifyAccess_Returns403_IfKnownNetworksConfigured_AndUnknownEntryBeforeClientIp()
    {
        // Arrange - rightmost entry isn't a known proxy so walk stops there, the in-range entry to its left is ignored
        var client = CreateKnownNetworksClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"verifyaccess/99/2/foo?roles={IpRole}");
        request.Headers.Add("X-Forwarded-For", "10.0.0.200, 192.168.1.1");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private HttpClient CreateKnownNetworksClient()
        => new AuthWebApplicationFactory()
            .WithConnectionString(connectionString)
            .WithConfigValue("KnownNetworks", ProxyNetwork)
            .CreateClient();

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
    [Fact]
    public async Task VerifyAccess_Returns401_IfAppointmentRole_NoCookie()
    {
        var response = await httpClient.GetAsync($"verifyaccess/99/2/foo?roles={AppointmentRole}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VerifyAccess_Returns401_IfAppointmentRole_SessionHasNoAppointment()
    {
        // Arrange
        const string cookieId = nameof(VerifyAccess_Returns401_IfAppointmentRole_SessionHasNoAppointment);
        await AddSessionWithAppointment(cookieId, null);

        // Act
        var response = await GetVerifyAccess($"verifyaccess/99/2/foo?roles={AppointmentRole}", cookieId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VerifyAccess_Returns200_IfSessionHasMatchingAppointment()
    {
        // Arrange
        const string cookieId = nameof(VerifyAccess_Returns200_IfSessionHasMatchingAppointment);
        await AddSessionWithAppointment(cookieId, a => a.AssetPatterns = new AssetPatterns { Space = [2] });

        // Act
        var response = await GetVerifyAccess($"verifyaccess/99/2/foo?roles={AppointmentRole}", cookieId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task VerifyAccess_Returns403_IfAppointmentDoesNotMatchAsset()
    {
        // Arrange
        const string cookieId = nameof(VerifyAccess_Returns403_IfAppointmentDoesNotMatchAsset);
        await AddSessionWithAppointment(cookieId,
            a => a.AssetPatterns = new AssetPatterns { String1 = ["LIDDLE/*"] });

        // Act
        var response = await GetVerifyAccess($"verifyaccess/99/2/foo?roles={AppointmentRole}", cookieId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task VerifyAccess_Returns403_IfAppointmentEnded()
    {
        // Arrange
        const string cookieId = nameof(VerifyAccess_Returns403_IfAppointmentEnded);
        await AddSessionWithAppointment(cookieId, a => a.End = DateTime.UtcNow.AddMinutes(-1));

        // Act
        var response = await GetVerifyAccess($"verifyaccess/99/2/foo?roles={AppointmentRole}", cookieId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private Task<HttpResponseMessage> GetVerifyAccess(string path, string cookieId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
        return httpClient.SendAsync(request);
    }

    /// <summary>
    /// Add a session with "clickthrough" role, holding an appointment for the appointment role (unless configure is
    /// null)
    /// </summary>
    private async Task AddSessionWithAppointment(string cookieId, Action<Appointment>? configure)
    {
        var session = CreateSessionUser(cookieId);
        session.AccessToken = cookieId;
        if (configure != null)
        {
            var appointment = new Appointment
            {
                Id = Guid.NewGuid(),
                Customer = 99,
                RoleProviderId = dbFixture.AppointmentRoleProviderId,
                Token = cookieId,
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
}
