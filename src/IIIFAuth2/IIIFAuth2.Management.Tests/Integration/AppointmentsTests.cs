using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FakeItEasy;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Core.Models.Domain;
using IIIFAuth2.Management.Infrastructure.Dlcs;
using IIIFAuth2.Management.Tests.TestingInfrastructure;
using IIIFAuth2.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IIIFAuth2.Management.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class AppointmentsTests : IClassFixture<ManagementWebApplicationFactory>
{
    private readonly HttpClient httpClient;
    private readonly DatabaseFixture dbFixture;
    private const string Path = "/customers/99/auth/appointments";

    public AppointmentsTests(ManagementWebApplicationFactory factory, DatabaseFixture dbFixture)
    {
        this.dbFixture = dbFixture;
        dbFixture.CleanUp();

        var dlcsApiClient = A.Fake<IDlcsApiClient>();
        A.CallTo(() => dlcsApiClient.IsRequestAuthenticated(A<int>._, A<CancellationToken>._)).Returns(true);

        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithTestServices(services => services.AddSingleton(dlcsApiClient))
            .CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", "dmFsaWQ6Y3JlZHM=");
    }

    private static object ValidBody(bool active = false, object? assetPatterns = null) => new
    {
        name = "Alice Smith",
        email = "alice@example.com",
        notes = "PhD research",
        start = "2099-07-01T09:00:00Z",
        end = "2099-07-01T12:00:00Z",
        active,
        approvedBy = active ? "jbloggs" : null,
        roles = new[] { DatabaseFixture.AppointmentRoleUri },
        assetPatterns = assetPatterns ?? new { string1 = new[] { "ExampleItem/2/1" } },
    };

    private async Task<JsonNode> Create(object? body = null)
    {
        var response = await httpClient.PostAsJsonAsync(Path, body ?? ValidBody());
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonNode>())!;
    }

    private static string IdOf(JsonNode json) => json["@id"]!.GetValue<string>().Split('/').Last();

    [Fact]
    public async Task Create_Returns201_WithGeneratedToken_AndStores()
    {
        var response = await httpClient.PostAsJsonAsync(Path, ValidBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
        var token = json["token"]!.GetValue<string>();
        token.Should().MatchRegex("^[a-z0-9]{12}$");
        json["@type"]!.GetValue<string>().Should().Be("Appointment");
        json["@context"]!.GetValue<string>().Should().Be("http://www.w3.org/ns/hydra/context.jsonld");
        json["start"]!.GetValue<string>().Should().Be("2099-07-01T09:00:00Z");
        json["created"].Should().NotBeNull();
        json["approvedBy"].Should().BeNull("nulls are omitted");
        json["dateApproved"].Should().BeNull();
        response.Headers.Location!.ToString().Should().Be(json["@id"]!.GetValue<string>());

        var stored = await dbFixture.DbContext.Appointments.AsNoTracking().SingleAsync(a => a.Token == token);
        json["@id"]!.GetValue<string>().Should().EndWith($"{Path}/{stored.Id}");
        json["@id"]!.GetValue<string>().Should().NotContain(token, "token is a secret so not used in URIs");
        stored.Customer.Should().Be(DatabaseFixture.Customer);
        stored.RoleProviderId.Should().Be(dbFixture.AppointmentRoleProviderId);
        stored.AssetPatterns.Customer.Should().BeEquivalentTo([DatabaseFixture.Customer], "customer defaulted");
        stored.AssetPatterns.String1.Should().BeEquivalentTo("ExampleItem/2/1");
    }
    
    [Fact]
    public async Task Create_Returns201_WithGeneratedToken_AndStores_SetsCustomerFromRequestIfEmptyAssetPattern()
    {
        var response = await httpClient.PostAsJsonAsync(Path, ValidBody(assetPatterns: new { }));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
        var token = json["token"]!.GetValue<string>();
        token.Should().MatchRegex("^[a-z0-9]{12}$");
        json["@type"]!.GetValue<string>().Should().Be("Appointment");
        json["@context"]!.GetValue<string>().Should().Be("http://www.w3.org/ns/hydra/context.jsonld");
        json["start"]!.GetValue<string>().Should().Be("2099-07-01T09:00:00Z");
        json["created"].Should().NotBeNull();
        json["approvedBy"].Should().BeNull("nulls are omitted");
        json["dateApproved"].Should().BeNull();
        response.Headers.Location!.ToString().Should().Be(json["@id"]!.GetValue<string>());

        var stored = await dbFixture.DbContext.Appointments.AsNoTracking().SingleAsync(a => a.Token == token);
        json["@id"]!.GetValue<string>().Should().EndWith($"{Path}/{stored.Id}");
        json["@id"]!.GetValue<string>().Should().NotContain(token, "token is a secret so not used in URIs");
        stored.Customer.Should().Be(DatabaseFixture.Customer);
        stored.RoleProviderId.Should().Be(dbFixture.AppointmentRoleProviderId);
        stored.AssetPatterns.Customer.Should().BeEquivalentTo([DatabaseFixture.Customer], "customer defaulted");
        stored.AssetPatterns.String1.Should().BeNull("default ValidBody() adds string1 so this confirms reset");
    }

    [Fact]
    public async Task Create_Active_SetsDateApproved()
    {
        var json = await Create(ValidBody(active: true));

        json["dateApproved"].Should().NotBeNull();
        json["approvedBy"]!.GetValue<string>().Should().Be("jbloggs");
    }

    public static TheoryData<string, string> InvalidBodies => new()
    {
        { """{"email":"a@b.c","start":"2099-07-01T09:00:00Z","end":"2099-07-01T12:00:00Z","active":false,"roles":["r"],"assetPatterns":{}}""", "The name field is required" },
        { """{"name":"a","email":"not-an-email","start":"2099-07-01T09:00:00Z","end":"2099-07-01T12:00:00Z","active":false,"roles":["http://dlcs.test/99/appointment:token-based"],"assetPatterns":{}}""", "The email field is not a valid e-mail address" },
        { """{"name":"a","email":"a@b.c","start":"2099-07-01T12:00:00Z","end":"2099-07-01T09:00:00Z","active":false,"roles":["http://dlcs.test/99/appointment:token-based"],"assetPatterns":{}}""", "end must be after start" },
        { """{"name":"a","email":"a@b.c","start":"2020-07-01T09:00:00Z","end":"2020-07-01T12:00:00Z","active":false,"roles":["http://dlcs.test/99/appointment:token-based"],"assetPatterns":{}}""", "end cannot be in the past" },
        { """{"name":"a","email":"a@b.c","start":"2020-07-01T09:00:00Z","end":"2099-07-01T12:00:00Z","active":false,"roles":["http://dlcs.test/99/appointment:token-based"],"assetPatterns":{}}""", "start cannot be before today" },
        { """{"name":"a","email":"a@b.c","start":"2099-07-01T09:00:00Z","end":"2099-07-01T12:00:00Z","active":true,"roles":["http://dlcs.test/99/appointment:token-based"],"assetPatterns":{}}""", "approvedBy is required" },
        { """{"name":"a","email":"a@b.c","start":"2099-07-01T09:00:00Z","end":"2099-07-01T12:00:00Z","active":false,"approvedBy":"x","roles":["http://dlcs.test/99/appointment:token-based"],"assetPatterns":{}}""", "approvedBy can only be set" },
        { """{"name":"a","email":"a@b.c","start":"2099-07-01T09:00:00Z","end":"2099-07-01T12:00:00Z","active":false,"roles":[],"assetPatterns":{}}""", "roles cannot be empty" },
        { """{"name":"a","email":"a@b.c","start":"2099-07-01T09:00:00Z","end":"2099-07-01T12:00:00Z","active":false,"roles":["http://dlcs.test/99/nope"],"assetPatterns":{}}""", "Unknown roles" },
        { """{"name":"a","email":"a@b.c","start":"2099-07-01T09:00:00Z","end":"2099-07-01T12:00:00Z","active":false,"roles":["http://dlcs.test/99/clickthrough"],"assetPatterns":{}}""", "Appointment role-provider" },
        { """{"name":"a","email":"a@b.c","start":"2099-07-01T09:00:00Z","end":"2099-07-01T12:00:00Z","active":false,"roles":["http://dlcs.test/99/clickthrough","http://dlcs.test/99/appointment:token-based"],"assetPatterns":{}}""", "same Appointment role-provider" },
        { """{"name":"a","email":"a@b.c","start":"not-a-date"}""", "start" },
        { """{"name":"a","email":"a@b.c","start":"2099-07-01T09:00:00Z","end":"2099-07-01T12:00:00Z","active":false,"roles":["http://dlcs.test/99/appointment:token-based"],"assetPatterns":{"customer":[99,2]}}""", "assetPatterns.customer can only be 99" },
        { """{"name":"a","email":"a@b.c","start":"2099-07-01T09:00:00Z","end":"2099-07-01T12:00:00Z","active":false,"roles":["http://dlcs.test/99/appointment:token-based"]}""", "The assetPatterns field is required." },
    };

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task Create_Returns400_HydraError_IfInvalid(string body, string expectedDetail)
    {
        var response = await httpClient.PostAsync(Path,
            new StringContent(body, new MediaTypeHeaderValue("application/json")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
        json["@type"]!.GetValue<string>().Should().Be("Error");
        json["status"]!.GetValue<int>().Should().Be(400);
        json["detail"]!.GetValue<string>().Should().Contain(expectedDetail);
    }

    [Fact]
    public async Task Create_StartComparedByDay_AllowsEarlierToday_RejectsYesterday()
    {
        var today = DateTime.UtcNow.Date;
        object Body(DateTime start) => new
        {
            name = "a", email = "a@b.c", start, end = today.AddDays(2), active = false,
            roles = new[] { DatabaseFixture.AppointmentRoleUri }, assetPatterns = new { }
        };

        var todayResponse = await httpClient.PostAsJsonAsync(Path, Body(today));
        var yesterdayResponse = await httpClient.PostAsJsonAsync(Path, Body(today.AddTicks(-1)));

        todayResponse.StatusCode.Should().Be(HttpStatusCode.Created, "midnight today is allowed");
        yesterdayResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await yesterdayResponse.Content.ReadFromJsonAsync<JsonNode>())!["detail"]!.GetValue<string>().Should()
            .Contain("start cannot be before today");
    }

    [Fact]
    public async Task Get_Returns404_IfUnknown_OrToken_OrOtherCustomer()
    {
        var created = await Create();
        var id = IdOf(created);

        (await httpClient.GetAsync($"{Path}/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await httpClient.GetAsync($"{Path}/{created["token"]!.GetValue<string>()}")).StatusCode.Should()
            .Be(HttpStatusCode.NotFound, "token can't be used as identifier");
        (await httpClient.GetAsync($"/customers/100/auth/appointments/{id}")).StatusCode.Should()
            .Be(HttpStatusCode.NotFound);
        (await httpClient.GetAsync($"{Path}/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Put_ReplacesFields_KeepsTokenAndCreated()
    {
        var created = await Create();
        var token = created["token"]!.GetValue<string>();
        var id = IdOf(created);

        var body = new
        {
            name = "Bob", email = "bob@example.com", start = "2099-08-01T09:00:00Z", end = "2099-08-02T09:00:00Z",
            active = true, approvedBy = "jbloggs", roles = new[] { DatabaseFixture.AppointmentRoleUri },
            assetPatterns = new { space = new[] { 4 }, customer = new[] { DatabaseFixture.Customer } },
            token = "ignored", created = "2020-01-01T00:00:00Z"
        };
        var response = await httpClient.PutAsJsonAsync($"{Path}/{id}", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
        json["token"]!.GetValue<string>().Should().Be(token);
        json["created"]!.GetValue<string>().Should().Be(created["created"]!.GetValue<string>());
        json["name"]!.GetValue<string>().Should().Be("Bob");
        json["notes"].Should().BeNull("omitted optional field cleared by PUT");
        json["dateApproved"].Should().NotBeNull();
        json["assetPatterns"]!["string1"].Should().BeNull();
        json["assetPatterns"]!["space"]!.AsArray().Select(n => n!.GetValue<int>()).Should().BeEquivalentTo([4]);
    }

    [Fact]
    public async Task Put_Returns400_IfMissingFields_And404_IfUnknown()
    {
        var id = IdOf(await Create());

        (await httpClient.PutAsJsonAsync($"{Path}/{id}", new { name = "Bob" })).StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);
        (await httpClient.PutAsJsonAsync($"{Path}/{Guid.NewGuid()}", ValidBody())).StatusCode.Should()
            .Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_Approve_ThenDeactivate_KeepsApprovedBy_ClearsDateApproved()
    {
        var id = IdOf(await Create());

        var approved = await httpClient.PatchAsJsonAsync($"{Path}/{id}", new { active = true, approvedBy = "jb" });
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        var approvedJson = (await approved.Content.ReadFromJsonAsync<JsonNode>())!;
        approvedJson["dateApproved"].Should().NotBeNull();
        approvedJson["name"]!.GetValue<string>().Should().Be("Alice Smith", "omitted fields unchanged");
        approvedJson["notes"]!.GetValue<string>().Should().Be("PhD research");

        var deactivated = await httpClient.PatchAsJsonAsync($"{Path}/{id}", new { active = false });
        deactivated.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = (await deactivated.Content.ReadFromJsonAsync<JsonNode>())!;
        json["active"]!.GetValue<bool>().Should().BeFalse();
        json["approvedBy"]!.GetValue<string>().Should().Be("jb");
        json["dateApproved"].Should().BeNull();

        var reapproved = await httpClient.PatchAsJsonAsync($"{Path}/{id}", new { active = true, approvedBy = "other" });
        var reapprovedJson = (await reapproved.Content.ReadFromJsonAsync<JsonNode>())!;
        reapprovedJson["dateApproved"]!.GetValue<string>().Should()
            .NotBe(approvedJson["dateApproved"]!.GetValue<string>());
    }

    [Fact]
    public async Task Patch_Deactivate_WithEmptyApprovedBy_ClearsApprovedBy()
    {
        // https://github.com/dlcs/iiif-auth-v2/issues/79
        var id = IdOf(await Create());
        (await httpClient.PatchAsJsonAsync($"{Path}/{id}", new { active = true, approvedBy = "jb" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var deactivated = await httpClient.PatchAsJsonAsync($"{Path}/{id}", new { active = false, approvedBy = "" });

        deactivated.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = (await deactivated.Content.ReadFromJsonAsync<JsonNode>())!;
        json["active"]!.GetValue<bool>().Should().BeFalse();
        json["approvedBy"].Should().BeNull();
        json["dateApproved"].Should().BeNull();
        var stored = await dbFixture.DbContext.Appointments.AsNoTracking().SingleAsync(a => a.Id == Guid.Parse(id));
        stored.ApprovedBy.Should().BeNull("empty string is stored as null");
    }

    [Fact]
    public async Task Patch_Activate_WithEmptyApprovedBy_Returns400()
    {
        var id = IdOf(await Create());

        var response = await httpClient.PatchAsJsonAsync($"{Path}/{id}", new { active = true, approvedBy = " " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonNode>())!["detail"]!.GetValue<string>().Should()
            .Contain("approvedBy is required if active");
    }

    [Fact]
    public async Task Patch_Returns400_IfEmailInvalid()
    {
        var id = IdOf(await Create());

        var response = await httpClient.PatchAsJsonAsync($"{Path}/{id}", new { email = "not-an-email" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonNode>())!["detail"]!.GetValue<string>().Should()
            .Contain("email");
    }

    [Fact]
    public async Task Patch_CanSetEndInPast()
    {
        var id = IdOf(await Create());

        var response = await httpClient.PatchAsJsonAsync($"{Path}/{id}",
            new { start = "2020-01-01T00:00:00Z", end = "2020-01-02T00:00:00Z" });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "past end only rejected on create");
    }

    [Fact]
    public async Task Patch_Returns400_IfResultInvalid()
    {
        var id = IdOf(await Create());

        var response = await httpClient.PatchAsJsonAsync($"{Path}/{id}", new { end = "2026-01-01T00:00:00Z" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_Returns204_ThenNotFound_AndRemovesSessionLinks()
    {
        var id = IdOf(await Create());
        var appointment = await dbFixture.DbContext.Appointments.AsNoTracking().SingleAsync(a => a.Id == Guid.Parse(id));
        var session = new SessionUser
        {
            Id = Guid.NewGuid(), Customer = DatabaseFixture.Customer, AccessToken = Guid.NewGuid().ToString(),
            CookieId = Guid.NewGuid().ToString(), Expires = DateTime.UtcNow.AddHours(1), Origin = "http://test",
            Roles = []
        };
        dbFixture.DbContext.SessionUsers.Add(session);
        dbFixture.DbContext.SessionAppointments.Add(new SessionAppointment
            { SessionUserId = session.Id, AppointmentId = appointment.Id });
        await dbFixture.DbContext.SaveChangesAsync();

        (await httpClient.DeleteAsync($"{Path}/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await httpClient.DeleteAsync($"{Path}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await dbFixture.DbContext.SessionAppointments.AnyAsync(sa => sa.AppointmentId == appointment.Id))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Search_FiltersAndPages_NewestFirst()
    {
        await Create(ValidBody(active: true));
        var pending1 = await Create();
        var pending2 = await Create();
        var otherCustomer = new Appointment
        {
            Id = Guid.NewGuid(), Customer = 100, Token = "othercustomer", RoleProviderId = dbFixture.AppointmentRoleProviderId,
            Roles = ["r"], Start = DateTime.UtcNow, End = DateTime.UtcNow.AddDays(1), Name = "Alice"
        };
        dbFixture.DbContext.Appointments.Add(otherCustomer);
        await dbFixture.DbContext.SaveChangesAsync();

        var response = await httpClient.GetAsync($"{Path}?active=false&name=ALICE&pageSize=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
        json["@type"]!.GetValue<string>().Should().Be("Collection");
        json["totalItems"]!.GetValue<int>().Should().Be(2);
        json["member"]!.AsArray().Single()!["token"]!.GetValue<string>().Should()
            .Be(pending2["token"]!.GetValue<string>());
        json["member"]![0]!["@context"].Should().BeNull("only top-level has context");
        var next = json["view"]!["next"]!.GetValue<string>();
        next.Should().Contain("page=2").And.Contain("pageSize=1").And.Contain("active=false").And.Contain("name=ALICE");

        var page2 = (await (await httpClient.GetAsync(next)).Content.ReadFromJsonAsync<JsonNode>())!;
        page2["member"]!.AsArray().Single()!["token"]!.GetValue<string>().Should()
            .Be(pending1["token"]!.GetValue<string>());
    }

    [Fact]
    public async Task Search_AwaitingApproval_ExcludesActiveAndEnded()
    {
        await Create(ValidBody(active: true));
        var future = DateTime.UtcNow.AddDays(1);
        var pending = await Create(new
        {
            name = "a", email = "a@b.c", start = future, end = future.AddHours(1), active = false,
            roles = new[] { DatabaseFixture.AppointmentRoleUri }, assetPatterns = new { }
        });
        // Pending but ended - added directly as create doesn't allow past end
        dbFixture.DbContext.Appointments.Add(new Appointment
        {
            Id = Guid.NewGuid(), Customer = DatabaseFixture.Customer, Token = "pendingended",
            RoleProviderId = dbFixture.AppointmentRoleProviderId, Roles = [DatabaseFixture.AppointmentRoleUri],
            Start = DateTime.UtcNow.AddDays(-2), End = DateTime.UtcNow.AddDays(-1), Name = "a", Email = "a@b.c"
        });
        await dbFixture.DbContext.SaveChangesAsync();

        var json = (await (await httpClient.GetAsync($"{Path}?awaitingApproval=true"))
            .Content.ReadFromJsonAsync<JsonNode>())!;

        json["totalItems"]!.GetValue<int>().Should().Be(1);
        json["member"]![0]!["token"]!.GetValue<string>().Should().Be(pending["token"]!.GetValue<string>());
        json["view"].Should().BeNull("single page");
    }

    // match contains the special character literally, decoy would match if the character were treated as a wildcard
    // in order values are: field - search - match - decoy
    public static TheoryData<string, string, string, string> SpecialCharacterSearches => new()
    {
        { "name", "100%", "Score 100% Smith", "Score 100X Smith" },
        { "name", "a_b", "Alice a_b", "Alice aXb" },
        { "name", @"a\b", @"Alice a\b", "Alice ab" },
        { "email", "100%", "100%@example.com", "100X@example.com" },
        { "email", "a_b", "a_b@example.com", "aXb@example.com" },
        { "email", @"a\b", @"a\b@example.com", "ab@example.com" },
        { "token", "100%", "tok100%en", "tok100Xen" },
        { "token", "a_b", "toka_ben", "tokaXben" },
        { "token", @"a\b", @"toka\ben", "tokaben" },
    };

    [Theory]
    [MemberData(nameof(SpecialCharacterSearches))]
    public async Task Search_MatchesLikeSpecialCharactersLiterally(string field, string search, string match,
        string decoy)
    {
        var matchAppointment = AppointmentWith(field, match);
        dbFixture.DbContext.Appointments.AddRange(matchAppointment, AppointmentWith(field, decoy));
        await dbFixture.DbContext.SaveChangesAsync();

        var json = (await (await httpClient.GetAsync($"{Path}?{field}={Uri.EscapeDataString(search)}"))
            .Content.ReadFromJsonAsync<JsonNode>())!;

        json["totalItems"]!.GetValue<int>().Should().Be(1);
        json["member"]![0]!["token"]!.GetValue<string>().Should().Be(matchAppointment.Token);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData(@"\")]
    public async Task Search_SpecialCharacterOnly_DoesNotMatchAll(string search)
    {
        dbFixture.DbContext.Appointments.Add(AppointmentWith("name", "Alice Smith"));
        await dbFixture.DbContext.SaveChangesAsync();

        var json = (await (await httpClient.GetAsync($"{Path}?name={Uri.EscapeDataString(search)}"))
            .Content.ReadFromJsonAsync<JsonNode>())!;

        json["totalItems"]!.GetValue<int>().Should().Be(0, "LIKE wildcards in search are matched literally");
    }

    private Appointment AppointmentWith(string field, string value)
    {
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(), Customer = DatabaseFixture.Customer, Token = Guid.NewGuid().ToString("N"),
            RoleProviderId = dbFixture.AppointmentRoleProviderId, Roles = [DatabaseFixture.AppointmentRoleUri],
            Start = DateTime.UtcNow, End = DateTime.UtcNow.AddDays(1), Name = "Alice", Email = "alice@example.com"
        };
        switch (field)
        {
            case "name": appointment.Name = value; break;
            case "email": appointment.Email = value; break;
            case "token": appointment.Token = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(field), field, null);
        }
        return appointment;
    }
}
