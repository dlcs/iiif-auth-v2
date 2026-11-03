using FakeItEasy;
using IIIFAuth2.API.Infrastructure.Auth;
using IIIFAuth2.API.Infrastructure.Auth.Models;
using IIIFAuth2.API.Tests.TestingInfrastructure;
using IIIFAuth2.Core.Data;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Core.Models.Domain;
using IIIFAuth2.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IIIFAuth2.API.Tests.Infrastructure.Auth;

/// <remarks>
/// Each test uses a unique customer as customer records are cached with no expiry
/// </remarks>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class AppointmentRoleEvaluatorTests
{
    private const int Space = 4;
    private readonly AuthServicesContext dbContext;
    private readonly ILogger<AppointmentRoleEvaluator> logger = A.Fake<ILogger<AppointmentRoleEvaluator>>();
    private readonly AppointmentRoleEvaluator sut;

    public AppointmentRoleEvaluatorTests(DatabaseFixture dbFixture)
    {
        dbContext = dbFixture.CreateNewAuthServiceContext();
        dbFixture.CleanUp();

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("dlcs.test.example");
        var contextAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => contextAccessor.HttpContext).Returns(context);
        sut = new AppointmentRoleEvaluator(dbContext, contextAccessor, logger);
    }

    [Fact]
    public async Task Evaluate_Empty_IfNoAppointmentRoles()
    {
        const int customer = 9101;
        var clickthrough = await AddRole(customer, "clickthrough", ClickthroughConfig());
        var session = await AddSession(customer);

        var result = await sut.Evaluate(customer, Space, [clickthrough], session, null);

        result.Should().BeSameAs(AppointmentRoleEvaluation.Empty);
    }

    [Fact]
    public async Task Evaluate_NoAppointment_IfNoSession()
    {
        const int customer = 9102;
        var role = await AddRole(customer, "appointment:a", AppointmentConfig());

        var result = await sut.Evaluate(customer, Space, [role], null, null);

        result.AppointmentRoles.Should().BeEquivalentTo(role);
        result.GrantedRoles.Should().BeEmpty();
        result.Reason.Should().Be(AppointmentDenialReason.NoAppointment);
    }

    [Fact]
    public async Task Evaluate_NoAppointment_IfSessionHoldsNone()
    {
        const int customer = 9103;
        var role = await AddRole(customer, "appointment:a", AppointmentConfig());
        var session = await AddSession(customer);

        var result = await sut.Evaluate(customer, Space, [role], session, null);

        result.Reason.Should().Be(AppointmentDenialReason.NoAppointment);
    }

    [Fact]
    public async Task Evaluate_NoAppointment_IfHeldAppointmentGrantsDifferentRole()
    {
        const int customer = 9104;
        var requested = await AddRole(customer, "appointment:a", AppointmentConfig());
        var other = await AddRole(customer, "appointment:b", AppointmentConfig());
        var session = await AddSession(customer, NewAppointment(customer, other));

        var result = await sut.Evaluate(customer, Space, [requested], session, null);

        result.AppointmentRoles.Should().BeEquivalentTo(requested);
        result.Reason.Should().Be(AppointmentDenialReason.NoAppointment);
    }

    [Fact]
    public async Task Evaluate_Granted_IfActiveInWindowAndPatternsMatch()
    {
        const int customer = 9105;
        var role = await AddRole(customer, "appointment:a", AppointmentConfig());
        var clickthrough = await AddRole(customer, "clickthrough", ClickthroughConfig());
        var session = await AddSession(customer,
            NewAppointment(customer, role, a => a.AssetPatterns = new AssetPatterns
            {
                Customer = [customer], Space = [Space], String1 = ["LIDDLE/WW2/HFIU/*"]
            }));

        var result = await sut.Evaluate(customer, Space, [role, clickthrough], session,
            new AssetMetadata { String1 = "LIDDLE/WW2/HFIU/003" });

        result.AppointmentRoles.Should().BeEquivalentTo(role);
        result.GrantedRoles.Should().BeEquivalentTo(role);
        result.Reason.Should().BeNull();
        AssertMismatchLogged(0);
    }

    [Fact]
    public async Task Evaluate_Granted_IfAnyHeldAppointmentMatches()
    {
        const int customer = 9106;
        var role = await AddRole(customer, "appointment:a", AppointmentConfig());
        var session = await AddSession(customer,
            NewAppointment(customer, role, a => a.Active = false),
            NewAppointment(customer, role));

        var result = await sut.Evaluate(customer, Space, [role], session, null);

        result.GrantedRoles.Should().BeEquivalentTo(role);
    }

    [Fact]
    public async Task Evaluate_NotStarted_WithStart()
    {
        const int customer = 9107;
        var role = await AddRole(customer, "appointment:a", AppointmentConfig());
        var start = DateTime.UtcNow.AddDays(1);
        var session = await AddSession(customer, NewAppointment(customer, role, a => a.Start = start));

        var result = await sut.Evaluate(customer, Space, [role], session, null);

        result.Reason.Should().Be(AppointmentDenialReason.NotStarted);
        result.Start.Should().BeCloseTo(start, TimeSpan.FromMilliseconds(1));
        result.End.Should().BeNull();
    }

    [Fact]
    public async Task Evaluate_NotActive()
    {
        const int customer = 9108;
        var role = await AddRole(customer, "appointment:a", AppointmentConfig());
        var session = await AddSession(customer, NewAppointment(customer, role, a => a.Active = false));

        var result = await sut.Evaluate(customer, Space, [role], session, null);

        result.Reason.Should().Be(AppointmentDenialReason.NotActive);
    }

    [Fact]
    public async Task Evaluate_Ended_WithEnd()
    {
        const int customer = 9109;
        var role = await AddRole(customer, "appointment:a", AppointmentConfig());
        var end = DateTime.UtcNow.AddMinutes(-5);
        var session = await AddSession(customer, NewAppointment(customer, role, a => a.End = end));

        var result = await sut.Evaluate(customer, Space, [role], session, null);

        result.Reason.Should().Be(AppointmentDenialReason.Ended);
        result.End.Should().BeCloseTo(end, TimeSpan.FromMilliseconds(1));
        result.Start.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("LIDDLE/WW2/EVAC/014")]
    public async Task Evaluate_AssetNotMatched(string? string1)
    {
        var customer = string1 == null ? 9110 : 9111;
        var role = await AddRole(customer, "appointment:a", AppointmentConfig());
        var session = await AddSession(customer, NewAppointment(customer, role,
            a => a.AssetPatterns = new AssetPatterns { String1 = ["LIDDLE/WW2/HFIU/*"] }));

        var result = await sut.Evaluate(customer, Space, [role], session,
            string1 == null ? null : new AssetMetadata { String1 = string1 });

        result.Reason.Should().Be(AppointmentDenialReason.AssetNotMatched);
    }

    [Fact]
    public async Task Evaluate_AssetNotMatched_IfDifferentSpace()
    {
        const int customer = 9112;
        var role = await AddRole(customer, "appointment:a", AppointmentConfig());
        var session = await AddSession(customer, NewAppointment(customer, role,
            a => a.AssetPatterns = new AssetPatterns { Space = [Space + 1] }));

        var result = await sut.Evaluate(customer, Space, [role], session, null);

        result.Reason.Should().Be(AppointmentDenialReason.AssetNotMatched);
    }

    [Fact]
    public async Task Evaluate_ReportsHighestPriorityReason()
    {
        const int customer = 9113;
        var role = await AddRole(customer, "appointment:a", AppointmentConfig());
        var session = await AddSession(customer,
            NewAppointment(customer, role, a => a.AssetPatterns = new AssetPatterns { Space = [Space + 1] }),
            NewAppointment(customer, role, a => a.End = DateTime.UtcNow.AddMinutes(-1)),
            NewAppointment(customer, role, a => a.Active = false));

        var result = await sut.Evaluate(customer, Space, [role], session, null);

        result.Reason.Should().Be(AppointmentDenialReason.NotActive);
    }

    [Fact]
    public async Task Evaluate_ProviderIsAuthoritative_AndMismatchLogged()
    {
        const int customer = 9114;
        var unprefixed = await AddRole(customer, "by-appointment", AppointmentConfig());
        var prefixedClickthrough = await AddRole(customer, "appointment:clickthrough", ClickthroughConfig());

        var result = await sut.Evaluate(customer, Space, [unprefixed, prefixedClickthrough], null, null);

        result.AppointmentRoles.Should().BeEquivalentTo(unprefixed);
        AssertMismatchLogged(2);
    }

    private void AssertMismatchLogged(int times)
        => A.CallTo(logger)
            .Where(call => call.Method.Name == nameof(ILogger.Log) &&
                           call.GetArgument<LogLevel>(0) == LogLevel.Debug &&
                           (call.GetArgument<object>(2) ?? string.Empty).ToString()!.Contains("mismatch"))
            .MustHaveHappened(times, Times.Exactly);

    private Guid providerId;

    private static RoleProviderConfiguration AppointmentConfig()
        => new()
        {
            [RoleProviderConfiguration.DefaultKey] = new AppointmentConfiguration
                { Config = RoleProviderType.Appointment }
        };

    private static RoleProviderConfiguration ClickthroughConfig()
        => new()
        {
            [RoleProviderConfiguration.DefaultKey] = new ClickthroughConfiguration
                { Config = RoleProviderType.Clickthrough }
        };

    private async Task<string> AddRole(int customer, string name, RoleProviderConfiguration configuration)
    {
        var roleId = $"http://dlcs.test/{customer}/{name}";
        var roleProvider = new RoleProvider { Id = Guid.NewGuid(), Customer = customer, Configuration = configuration };
        if (configuration.Values.Any(c => c is AppointmentConfiguration)) providerId = roleProvider.Id;
        var accessService = new AccessService
        {
            Id = Guid.NewGuid(),
            Customer = customer,
            Name = $"service-{Guid.NewGuid():N}",
            Profile = AccessServiceProfiles.Active,
            RoleProvider = roleProvider,
        };
        await dbContext.AccessServices.AddAsync(accessService);
        await dbContext.Roles.AddAsync(new Role
            { Id = roleId, Customer = customer, Name = name, AccessServiceId = accessService.Id });
        await dbContext.SaveChangesAsync();
        return roleId;
    }

    private Appointment NewAppointment(int customer, string role, Action<Appointment>? configure = null)
    {
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            Customer = customer,
            RoleProviderId = providerId,
            Token = Guid.NewGuid().ToString("N"),
            Roles = [role],
            Start = DateTime.UtcNow.AddHours(-1),
            End = DateTime.UtcNow.AddHours(1),
            Active = true,
        };
        configure?.Invoke(appointment);
        return appointment;
    }

    private async Task<SessionUser> AddSession(int customer, params Appointment[] appointments)
    {
        var session = new SessionUser
        {
            Id = Guid.NewGuid(),
            Customer = customer,
            CookieId = Guid.NewGuid().ToString(),
            AccessToken = Guid.NewGuid().ToString("N"),
            Origin = "http://localhost/",
            Expires = DateTime.UtcNow.AddHours(1),
            SessionAppointments = appointments.Select(a => new SessionAppointment { Appointment = a }).ToList(),
        };
        await dbContext.SessionUsers.AddAsync(session);
        await dbContext.SaveChangesAsync();
        return session;
    }
}
