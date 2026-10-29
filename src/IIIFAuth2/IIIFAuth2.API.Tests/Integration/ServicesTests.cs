using System.Net;
using IIIF;
using IIIF.Auth.V2;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using IIIFAuth2.API.Tests.Infrastructure;
using IIIFAuth2.API.Tests.TestingInfrastructure;
using IIIFAuth2.Core.Data;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Core.Models.Domain;
using IIIFAuth2.Tests.Common;

namespace IIIFAuth2.API.Tests.Integration;

/// <summary>
/// Tests of presentation requests
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class ServicesTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly HttpClient httpClient;
    private readonly AuthServicesContext dbContext;

    public ServicesTests(AuthWebApplicationFactory factory, DatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .CreateClient();

        dbFixture.CleanUp();
    }

    [Fact]
    public async Task GetServicesDescription_Returns400_IfNoRolesProvided()
    {
        // Arrange
        const string path = "services/12345";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task GetServicesDescription_Returns400_IfAssetIdInvalid()
    {
        // Arrange
        const string path = "services/12345?roles=hello";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task GetServicesDescription_Returns404_IfRoleExists_ButAssetIsDifferentCustomer()
    {
        // Arrange
        var path = $"services/1/2/asset?roles={DatabaseFixture.ClickthroughRoleUri}";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetServicesDescription_Returns404_IfRoleNotFound()
    {
        // Arrange
        const string path = "services/99/2/asset?roles=does-not-exist";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetServicesDescription_Returns404_IfRoleFound_ButAccessServiceNotFound()
    {
        // Arrange
        await dbContext.Roles.AddAsync(new Role
        {
            AccessServiceId = Guid.NewGuid(), Customer = DatabaseFixture.Customer, Id = "no-access-service",
            Name = "test"
        });
        await dbContext.SaveChangesAsync();

        const string path = "services/99/2/asset?roles=no-access-service";
            
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetServicesDescription_ReturnsServicesDescription_IfRoleAndAccessServiceFound()
    {
        // Arrange
        var path = $"services/99/2/asset?roles={DatabaseFixture.ClickthroughRoleUri}";

        var expected = new AuthProbeService2
        {
            Id = "https://orchestrator.testing/auth/v2/probe/99/2/asset",
            Service = new List<IService>
            {
                new AuthAccessService2
                {
                    Id = "http://localhost/auth/v2/access/99/clickthrough",
                    Profile = "active",
                    Service = new List<IService>
                    {
                        new AuthAccessTokenService2 { Id = "http://localhost/auth/v2/access/99/token", },
                        new AuthLogoutService2
                        {
                            Id = "http://localhost/auth/v2/99/clickthrough/logout",
                            Label = new LanguageMap("en", $"Logout of {DatabaseFixture.ClickthroughService}")
                        }
                    }
                }
            }
        };
        
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var probeService = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeService2>();
        probeService.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetServicesDescription_ReturnsServicesDescription_ForAdjunct_IfRoleAndAccessServiceFound()
    {
        // Arrange
        var path = $"services/99/2/asset/adjunct-1?roles={DatabaseFixture.ClickthroughRoleUri}";

        var expected = new AuthProbeService2
        {
            Id = "https://orchestrator.testing/auth/v2/probe/99/2/asset/adjunct-1",
            Service = new List<IService>
            {
                new AuthAccessService2
                {
                    Id = "http://localhost/auth/v2/access/99/clickthrough",
                    Profile = "active",
                    Service = new List<IService>
                    {
                        new AuthAccessTokenService2 { Id = "http://localhost/auth/v2/access/99/token", },
                        new AuthLogoutService2
                        {
                            Id = "http://localhost/auth/v2/99/clickthrough/logout",
                            Label = new LanguageMap("en", $"Logout of {DatabaseFixture.ClickthroughService}")
                        }
                    }
                }
            }
        };

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var probeService = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeService2>();
        probeService.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetServicesDescription_Returns400_IfTooManyPathSegments()
    {
        // Arrange
        var path = $"services/99/2/asset/adjunct-1/extra?roles={DatabaseFixture.ClickthroughRoleUri}";

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetServicesDescription_ReturnsServicesDescription_SingleService_IfMultipleRoles_OneNotFound()
    {
        // Arrange
        var path = $"services/99/2/asset?roles={DatabaseFixture.ClickthroughRoleUri},role-not-found";

        var expected = new AuthProbeService2
        {
            Id = "https://orchestrator.testing/auth/v2/probe/99/2/asset",
            Service = new List<IService>
            {
                new AuthAccessService2
                {
                    Id = "http://localhost/auth/v2/access/99/clickthrough",
                    Profile = "active",
                    Service = new List<IService>
                    {
                        new AuthAccessTokenService2 { Id = "http://localhost/auth/v2/access/99/token", },
                        new AuthLogoutService2
                        {
                            Id = "http://localhost/auth/v2/99/clickthrough/logout",
                            Label = new LanguageMap("en", $"Logout of {DatabaseFixture.ClickthroughService}")
                        }
                    }
                }
            }
        };
        
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var probeService = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeService2>();
        probeService.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetServicesDescription_ReturnsExternalService_IfIpRole()
    {
        // Arrange
        var path = $"services/99/2/asset?roles={Uri.EscapeDataString(DatabaseFixture.IpRestrictionRoleUri)}";

        var expected = new AuthProbeService2
        {
            Id = "https://orchestrator.testing/auth/v2/probe/99/2/asset",
            Service = new List<IService>
            {
                new AuthAccessService2
                {
                    Profile = "external",
                    Label = new LanguageMap("en", "Available in the Research Centre"),
                    Service = new List<IService>
                    {
                        new AuthAccessTokenService2 { Id = "http://localhost/auth/v2/access/99/token", },
                    }
                }
            }
        };

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var probeService = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeService2>();
        probeService.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetServicesDescription_ReturnsExternalServiceFirst_IfIpAndClickthroughRoles()
    {
        // Arrange
        var path =
            $"services/99/2/asset?roles={DatabaseFixture.ClickthroughRoleUri},{Uri.EscapeDataString(DatabaseFixture.IpRestrictionRoleUri)}";

        var expected = new AuthProbeService2
        {
            Id = "https://orchestrator.testing/auth/v2/probe/99/2/asset",
            Service = new List<IService>
            {
                new AuthAccessService2
                {
                    Profile = "external",
                    Label = new LanguageMap("en", "Available in the Research Centre"),
                    Service = new List<IService>
                    {
                        new AuthAccessTokenService2 { Id = "http://localhost/auth/v2/access/99/token", },
                    }
                },
                new AuthAccessService2
                {
                    Id = "http://localhost/auth/v2/access/99/clickthrough",
                    Profile = "active",
                    Service = new List<IService>
                    {
                        new AuthAccessTokenService2 { Id = "http://localhost/auth/v2/access/99/token", },
                        new AuthLogoutService2
                        {
                            Id = "http://localhost/auth/v2/99/clickthrough/logout",
                            Label = new LanguageMap("en", $"Logout of {DatabaseFixture.ClickthroughService}")
                        }
                    }
                }
            }
        };

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var probeService = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeService2>();
        probeService.Should().BeEquivalentTo(expected, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task GetServicesDescription_ExternalService_UsesGenericLabel_IfNoLabelOrPlaceName()
    {
        // Arrange
        // Customer specific to this test as customer records are cached
        const int customer = 98;
        const string role = "http://dlcs.test/98/ip:no-place-name";
        await AddIpRestriction(customer, role, placeName: null, label: null);

        var path = $"services/{customer}/2/asset?roles={Uri.EscapeDataString(role)}";

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var probeService = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeService2>();
        var accessService = probeService.Service.Should().ContainSingle().Which.As<AuthAccessService2>();
        accessService.Label["en"].Should().ContainSingle(s => s == "Available at permitted locations");
    }

    [Fact]
    public async Task GetServicesDescription_ExternalService_UsesAccessServiceLabel_IfSet()
    {
        // Arrange
        // Customer specific to this test as customer records are cached
        const int customer = 97;
        const string role = "http://dlcs.test/97/ip:with-label";
        await AddIpRestriction(customer, role, placeName: "the Library",
            label: new LanguageMap("en", "On site access"));

        var path = $"services/{customer}/2/asset?roles={Uri.EscapeDataString(role)}";

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var probeService = (await response.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeService2>();
        var accessService = probeService.Service.Should().ContainSingle().Which.As<AuthAccessService2>();
        accessService.Label["en"].Should().ContainSingle(s => s == "On site access");
    }

    private async Task AddIpRestriction(int customer, string role, string? placeName, LanguageMap? label)
    {
        var roleProvider = new RoleProvider
        {
            Customer = customer,
            Configuration = new RoleProviderConfiguration
            {
                [RoleProviderConfiguration.DefaultKey] = new IpRestrictionConfiguration
                {
                    Config = RoleProviderType.IpRestriction, Ranges = ["10.0.0.0/24"], PlaceName = placeName
                }
            }
        };
        var accessService = new AccessService
        {
            Id = Guid.NewGuid(), Customer = customer, Name = "on-site", Profile = AccessServiceProfiles.External,
            Label = label, RoleProvider = roleProvider
        };
        await dbContext.AccessServices.AddAsync(accessService);
        await dbContext.Roles.AddAsync(new Role
        {
            Id = role, Customer = customer, Name = "ip-role", AccessServiceId = accessService.Id
        });
        await dbContext.SaveChangesAsync();
    }
}
