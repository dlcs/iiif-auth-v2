using System.Net;
using FakeItEasy;
using IIIFAuth2.API.Infrastructure.Auth;
using IIIFAuth2.API.Infrastructure.Auth.Models;
using IIIFAuth2.API.Infrastructure.Web;
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
public class IpRoleEvaluatorTests
{
    private const string DefaultHost = "dlcs.test.example";
    private readonly AuthServicesContext dbContext;
    private readonly ILogger<IpRoleEvaluator> logger = A.Fake<ILogger<IpRoleEvaluator>>();

    public IpRoleEvaluatorTests(DatabaseFixture dbFixture)
    {
        dbContext = dbFixture.CreateNewAuthServiceContext();
        dbFixture.CleanUp();
    }

    [Fact]
    public async Task Evaluate_Empty_IfNoRoles()
    {
        var sut = GetSut("10.0.0.5");

        var result = await sut.Evaluate(9001, []);

        result.Should().BeSameAs(IpRoleEvaluation.Empty);
    }

    [Fact]
    public async Task Evaluate_Empty_IfRolesNotFound()
    {
        var sut = GetSut("10.0.0.5");

        var result = await sut.Evaluate(9002, ["http://dlcs.test/9002/ip:unknown"]);

        result.Should().BeSameAs(IpRoleEvaluation.Empty);
    }

    [Fact]
    public async Task Evaluate_GrantsOnlyIpRolesMatchingClientIp()
    {
        // Arrange
        const int customer = 9003;
        const string library = "http://dlcs.test/9003/ip:library";
        const string archive = "http://dlcs.test/9003/ip:archive";
        const string clickthrough = "http://dlcs.test/9003/clickthrough";
        await AddRole(customer, library, IpConfig("10.0.0.0/24", "in the Library"));
        await AddRole(customer, archive, IpConfig("192.168.0.0/16", "via the Archive VPN"));
        await AddRole(customer, clickthrough, ClickthroughConfig());
        var sut = GetSut("10.0.0.5");

        // Act
        var result = await sut.Evaluate(customer, [library, archive, clickthrough]);

        // Assert
        result.IpRoles.Should().BeEquivalentTo(library, archive);
        result.GrantedRoles.Should().BeEquivalentTo(library);
        result.LocationPhrases.Should().BeEquivalentTo("in the Library", "via the Archive VPN");
        AssertWarningLogged(0);
    }

    [Fact]
    public async Task Evaluate_OnlyConsidersRequestedRoles()
    {
        // Arrange
        const int customer = 9004;
        const string requested = "http://dlcs.test/9004/ip:requested";
        const string notRequested = "http://dlcs.test/9004/ip:not-requested";
        await AddRole(customer, requested, IpConfig("192.168.0.0/16", "in Requested"));
        await AddRole(customer, notRequested, IpConfig("10.0.0.0/24", "in Not requested"));
        var sut = GetSut("10.0.0.5");

        // Act
        var result = await sut.Evaluate(customer, [requested]);

        // Assert
        result.IpRoles.Should().BeEquivalentTo(requested);
        result.GrantedRoles.Should().BeEmpty();
        result.LocationPhrases.Should().BeEquivalentTo("in Requested");
    }

    [Fact]
    public async Task Evaluate_LocationPhrases_DistinctAndExcludeNull()
    {
        // Arrange
        const int customer = 9005;
        const string first = "http://dlcs.test/9005/ip:first";
        const string second = "http://dlcs.test/9005/ip:second";
        const string noPlace = "http://dlcs.test/9005/ip:no-place";
        await AddRole(customer, first, IpConfig("10.0.0.0/24", "in the Library"));
        await AddRole(customer, second, IpConfig("10.0.1.0/24", "in the Library"));
        await AddRole(customer, noPlace, IpConfig("10.0.2.0/24"));
        var sut = GetSut("192.168.0.1");

        // Act
        var result = await sut.Evaluate(customer, [first, second, noPlace]);

        // Assert
        result.IpRoles.Should().HaveCount(3);
        result.LocationPhrases.Should().BeEquivalentTo("in the Library");
    }

    [Fact]
    public async Task Evaluate_NoRolesGranted_IfClientIpUnknown()
    {
        // Arrange
        const int customer = 9006;
        const string role = "http://dlcs.test/9006/ip:reading-room";
        await AddRole(customer, role, IpConfig("10.0.0.0/24"));
        var sut = GetSut(null);

        // Act
        var result = await sut.Evaluate(customer, [role]);

        // Assert
        result.IpRoles.Should().BeEquivalentTo(role);
        result.GrantedRoles.Should().BeEmpty();
    }

    [Fact]
    public async Task Evaluate_NotIpRole_IfIpPrefix_ButNotIpRestrictionProvider()
    {
        // Arrange
        const int customer = 9007;
        const string role = "http://dlcs.test/9007/ip:clickthrough";
        await AddRole(customer, role, ClickthroughConfig());
        var sut = GetSut("10.0.0.5");

        // Act
        var result = await sut.Evaluate(customer, [role]);

        // Assert
        result.Should().BeSameAs(IpRoleEvaluation.Empty);
        AssertWarningLogged(1);
    }

    [Fact]
    public async Task Evaluate_IpRole_IfNoIpPrefix_ButIpRestrictionProvider()
    {
        // Arrange
        const int customer = 9008;
        const string role = "http://dlcs.test/9008/on-site";
        await AddRole(customer, role, IpConfig("10.0.0.0/24"));
        var sut = GetSut("10.0.0.5");

        // Act
        var result = await sut.Evaluate(customer, [role]);

        // Assert
        result.IpRoles.Should().BeEquivalentTo(role);
        result.GrantedRoles.Should().BeEquivalentTo(role);
        AssertWarningLogged(1);
    }

    [Theory]
    [InlineData(DefaultHost, "192.168.0.5", true)]
    [InlineData(DefaultHost, "10.0.0.5", false)]
    [InlineData("other.example", "10.0.0.5", true)]
    [InlineData("other.example", "192.168.0.5", false)]
    public async Task Evaluate_UsesHostConfiguration_FallingBackToDefault(string host, string clientIp,
        bool expectGranted)
    {
        // Arrange
        // Records are cached after the first case, the configuration is the same for each so this doesn't matter
        const int customer = 9009;
        const string role = "http://dlcs.test/9009/ip:host-specific";
        var configuration = IpConfig("10.0.0.0/24");
        configuration[DefaultHost] = new IpRestrictionConfiguration
        {
            Config = RoleProviderType.IpRestriction, Ranges = ["192.168.0.0/16"]
        };
        await AddRole(customer, role, configuration);
        var sut = GetSut(clientIp, host);

        // Act
        var result = await sut.Evaluate(customer, [role]);

        // Assert
        result.GrantedRoles.Contains(role).Should().Be(expectGranted);
    }

    [Fact]
    public async Task Evaluate_Empty_IfNoConfigurationForHost_AndNoDefault()
    {
        // Arrange
        const int customer = 9010;
        const string role = "http://dlcs.test/9010/ip:no-default";
        var configuration = new RoleProviderConfiguration
        {
            ["other.example"] = new IpRestrictionConfiguration
            {
                Config = RoleProviderType.IpRestriction, Ranges = ["10.0.0.0/24"]
            }
        };
        await AddRole(customer, role, configuration);
        var sut = GetSut("10.0.0.5");

        // Act
        var result = await sut.Evaluate(customer, [role]);

        // Assert
        result.Should().BeSameAs(IpRoleEvaluation.Empty);
    }

    [Fact]
    public async Task Evaluate_Empty_IfAccessServiceHasNoRoleProvider()
    {
        // Arrange
        const int customer = 9011;
        const string role = "http://dlcs.test/9011/no-provider";
        await AddRole(customer, role, null);
        var sut = GetSut("10.0.0.5");

        // Act
        var result = await sut.Evaluate(customer, [role]);

        // Assert
        result.Should().BeSameAs(IpRoleEvaluation.Empty);
        AssertWarningLogged(0);
    }

    private IpRoleEvaluator GetSut(string? clientIp, string host = DefaultHost)
    {
        var clientIpProvider = A.Fake<IClientIpProvider>();
        A.CallTo(() => clientIpProvider.GetClientIp())
            .Returns(clientIp == null ? null : IPAddress.Parse(clientIp));

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        var contextAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => contextAccessor.HttpContext).Returns(context);

        return new IpRoleEvaluator(dbContext, clientIpProvider, contextAccessor, logger);
    }

    private void AssertWarningLogged(int times)
        => A.CallTo(logger)
            .Where(call => call.Method.Name == nameof(ILogger.Log) &&
                           call.GetArgument<LogLevel>(0) == LogLevel.Warning)
            .MustHaveHappened(times, Times.Exactly);

    private static RoleProviderConfiguration IpConfig(string range, string? locationPhrase = null)
        => new()
        {
            [RoleProviderConfiguration.DefaultKey] = new IpRestrictionConfiguration
            {
                Config = RoleProviderType.IpRestriction, Ranges = [range], LocationPhrase = locationPhrase
            }
        };

    private static RoleProviderConfiguration ClickthroughConfig()
        => new()
        {
            [RoleProviderConfiguration.DefaultKey] = new ClickthroughConfiguration
            {
                Config = RoleProviderType.Clickthrough
            }
        };

    private async Task AddRole(int customer, string roleId, RoleProviderConfiguration? configuration)
    {
        var accessService = new AccessService
        {
            Id = Guid.NewGuid(),
            Customer = customer,
            Name = $"service-{Guid.NewGuid():N}",
            Profile = configuration?.Values.Any(c => c is IpRestrictionConfiguration) == true
                ? AccessServiceProfiles.External
                : AccessServiceProfiles.Active,
            RoleProvider = configuration == null
                ? null
                : new RoleProvider { Id = Guid.NewGuid(), Customer = customer, Configuration = configuration },
        };
        await dbContext.AccessServices.AddAsync(accessService);
        await dbContext.Roles.AddAsync(new Role
        {
            Id = roleId, Customer = customer, Name = roleId.Split('/')[^1], AccessServiceId = accessService.Id
        });
        await dbContext.SaveChangesAsync();
    }
}
