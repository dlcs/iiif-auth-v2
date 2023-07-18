using System.Net;
using IIIF;
using IIIF.Auth.V2;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Tests.Infrastructure;

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
            Id = "todo",
            Service = new List<IService>
            {
                new AuthAccessService2
                {
                    Id = "todo",
                    Profile = "active",
                    Service = new List<IService>
                    {
                        new AuthAccessTokenService2 { Id = "todo", },
                        new AuthLogoutService2
                        {
                            Id = "todo",
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
    public async Task GetServicesDescription_ReturnsServicesDescription_SingleService_IfMultipleRoles_OneNotFound()
    {
        // Arrange
        var path = $"services/99/2/asset?roles={DatabaseFixture.ClickthroughRoleUri},role-not-found";

        var expected = new AuthProbeService2
        {
            Id = "todo",
            Service = new List<IService>
            {
                new AuthAccessService2
                {
                    Id = "todo",
                    Profile = "active",
                    Service = new List<IService>
                    {
                        new AuthAccessTokenService2 { Id = "todo", },
                        new AuthLogoutService2
                        {
                            Id = "todo",
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
}