using System.Net;
using IIIF.Auth.V2;
using IIIF.Serialisation;
using IIIFAuth2.API.Tests.TestingInfrastructure;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Core.Models.Domain;
using IIIFAuth2.Tests.Common;

namespace IIIFAuth2.API.Tests.Integration;

/// <summary>
/// Probe and verify-access with role entries (AND within an entry, OR across entries). Sessions hold role "foo"
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class RoleCombinationTests : IClassFixture<AuthWebApplicationFactory>
{
    private const string InRange = "10.0.0.5";
    private const string OutOfRange = "10.0.1.5";
    private static readonly string IpRole = Uri.EscapeDataString(DatabaseFixture.IpRestrictionRoleUri);
    private static readonly string AppointmentRole = Uri.EscapeDataString(DatabaseFixture.AppointmentRoleUri);
    private static readonly string IpAndFoo = $"{IpRole}%2Bfoo";
    private static readonly string IpAndAppointment = $"{IpRole}%2B{AppointmentRole}";
    private readonly HttpClient httpClient;
    private readonly DatabaseFixture dbFixture;

    public RoleCombinationTests(AuthWebApplicationFactory factory, DatabaseFixture dbFixture)
    {
        this.dbFixture = dbFixture;
        httpClient = factory.WithConnectionString(dbFixture.ConnectionString).CreateClient();
        dbFixture.CleanUp();
    }

    [Fact]
    public async Task IpAndSessionRole_200_IfInRange_AndSessionHasRole()
    {
        var token = await AddSession(nameof(IpAndSessionRole_200_IfInRange_AndSessionHasRole));

        var result = await Probe(IpAndFoo, token, InRange);

        result.Status.Should().Be(200);
    }

    [Fact]
    public async Task IpAndSessionRole_200_IfUnencodedPlus()
    {
        var token = await AddSession(nameof(IpAndSessionRole_200_IfUnencodedPlus));

        // "+" in query string decodes to a space, which is also treated as AND
        var result = await Probe($"{IpRole}+foo", token, InRange);

        result.Status.Should().Be(200);
    }

    [Fact]
    public async Task IpAndSessionRole_401_IfInRange_NoToken()
    {
        var result = await Probe(IpAndFoo, null, InRange);

        result.Status.Should().Be(401);
        result.Heading["en"].Should().ContainSingle(s => s == "Missing credentials");
    }

    [Fact]
    public async Task IpAndSessionRole_403Location_IfOutOfRange_WithSession()
    {
        var token = await AddSession(nameof(IpAndSessionRole_403Location_IfOutOfRange_WithSession));

        var result = await Probe(IpAndFoo, token, OutOfRange);

        AssertLocation(result);
    }

    [Fact]
    public async Task IpAndSessionRole_403Location_IfOutOfRange_NoToken()
    {
        // No session needed, the entry can't be met from this location
        var result = await Probe(IpAndFoo, null, OutOfRange);

        AssertLocation(result);
    }

    [Fact]
    public async Task IpAndSessionRole_403Forbidden_IfInRange_SessionLacksRole()
    {
        var token = await AddSession(nameof(IpAndSessionRole_403Forbidden_IfInRange_SessionLacksRole));

        var result = await Probe($"{IpRole}%2Bbar", token, InRange);

        result.Status.Should().Be(403);
        result.Heading["en"].Should().ContainSingle(s => s == "Forbidden");
    }

    [Fact]
    public async Task IpAndAppointment_200_IfInRange_WithAppointment()
    {
        var token = await AddSession(nameof(IpAndAppointment_200_IfInRange_WithAppointment), _ => { });

        var result = await Probe(IpAndAppointment, token, InRange);

        result.Status.Should().Be(200);
    }

    [Fact]
    public async Task IpAndAppointment_403Location_IfOutOfRange_WithAppointment()
    {
        var token = await AddSession(nameof(IpAndAppointment_403Location_IfOutOfRange_WithAppointment), _ => { });

        var result = await Probe(IpAndAppointment, token, OutOfRange);

        AssertLocation(result);
    }

    [Fact]
    public async Task IpAndAppointment_401AppointmentRequired_IfInRange_SessionWithoutAppointment()
    {
        var token = await AddSession(nameof(IpAndAppointment_401AppointmentRequired_IfInRange_SessionWithoutAppointment));

        var result = await Probe(IpAndAppointment, token, InRange);

        result.Status.Should().Be(401);
        result.Heading["en"].Should().ContainSingle(s => s == "Appointment required");
    }

    [Fact]
    public async Task IpAndAppointment_401WithAppointmentNote_IfInRange_NoToken()
    {
        var result = await Probe(IpAndAppointment, null, InRange);

        result.Status.Should().Be(401);
        result.Note["en"].Should()
            .ContainSingle(s => s == "Authorising credentials not found, or enter your appointment key");
    }

    [Fact]
    public async Task IpAndAppointment_403NotMatched_IfInRange_AssetNotInAppointment()
    {
        var token = await AddSession(nameof(IpAndAppointment_403NotMatched_IfInRange_AssetNotInAppointment),
            a => a.AssetPatterns = new AssetPatterns { Space = [3] });

        var result = await Probe(IpAndAppointment, token, InRange);

        result.Status.Should().Be(403);
        result.Heading["en"].Should().ContainSingle(s => s == "Not part of your appointment");
    }

    [Theory]
    [InlineData(InRange)]
    [InlineData(OutOfRange)]
    public async Task SessionRoleOrIpAndAppointment_200_IfSessionHasRole_RegardlessOfIp(string clientIp)
    {
        var token = await AddSession(
            $"{nameof(SessionRoleOrIpAndAppointment_200_IfSessionHasRole_RegardlessOfIp)}{clientIp}");

        var result = await Probe($"foo,{IpAndAppointment}", token, clientIp);

        result.Status.Should().Be(200);
    }

    [Fact]
    public async Task ClosestEntryReported_IfAllEntriesBlocked()
    {
        // ip+appointment has a met appointment, so is closer than "bar" which the session lacks
        var token = await AddSession(nameof(ClosestEntryReported_IfAllEntriesBlocked), _ => { });

        var result = await Probe($"bar,{IpAndAppointment}", token, OutOfRange);

        AssertLocation(result);
    }

    [Fact]
    public async Task InconsistentEntries_BehaveAsSmallestEntry()
    {
        var token = await AddSession(nameof(InconsistentEntries_BehaveAsSmallestEntry));

        (await Probe("foo%2Bbar,foo", token, OutOfRange)).Status.Should().Be(200);
        (await Probe("bar%2Bfoo,bar", token, OutOfRange)).Status.Should().Be(403);
    }

    [Theory]
    [InlineData(InRange, HttpStatusCode.OK)]
    [InlineData(OutOfRange, HttpStatusCode.Forbidden)]
    public async Task VerifyAccess_EvaluatesEntries(string clientIp, HttpStatusCode expected)
    {
        var cookieId = await AddSession($"{nameof(VerifyAccess_EvaluatesEntries)}{clientIp}");
        var request = new HttpRequestMessage(HttpMethod.Get, $"verifyaccess/99/2/foo?roles={IpAndFoo}");
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
        request.Headers.Add("X-Forwarded-For", clientIp);

        var response = await httpClient.SendAsync(request);

        response.StatusCode.Should().Be(expected);
    }

    private static void AssertLocation(AuthProbeResult2 result)
    {
        result.Status.Should().Be(403);
        result.Heading["en"].Should().ContainSingle(s => s == "Not available at your location");
        result.Note["en"].Should().ContainSingle(s => s == "Only available in the Research Centre");
    }

    private async Task<AuthProbeResult2> Probe(string roles, string? accessToken, string clientIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"probe_internal/99/2/foo?roles={roles}");
        if (accessToken != null) request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Headers.Add("X-Forwarded-For", clientIp);
        var response = await httpClient.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
    }

    /// <summary>
    /// Add a session with role "foo", holding an appointment for the appointment role if configure is not null.
    /// Returns the value used as both access token and cookie id
    /// </summary>
    private async Task<string> AddSession(string token, Action<Appointment>? configure = null)
    {
        var session = new SessionUser
        {
            Id = Guid.NewGuid(),
            CookieId = token,
            AccessToken = token,
            Customer = 99,
            Created = DateTime.UtcNow,
            Roles = ["foo"],
            Expires = DateTime.UtcNow.AddMinutes(5),
            Origin = "http://irrelevant-for-this/",
        };
        if (configure != null)
        {
            var appointment = new Appointment
            {
                Id = Guid.NewGuid(),
                Customer = 99,
                RoleProviderId = dbFixture.AppointmentRoleProviderId,
                Token = token,
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
        return token;
    }
}
