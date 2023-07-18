using System.Net;
using IIIFAuth2.API.Data;
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
}