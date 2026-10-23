using System.Net;
using System.Net.Http.Headers;
using FakeItEasy;
using IIIFAuth2.Management.Infrastructure.Dlcs;
using IIIFAuth2.Management.Tests.TestingInfrastructure;
using IIIFAuth2.Tests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace IIIFAuth2.Management.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class AuthenticationTests : IClassFixture<ManagementWebApplicationFactory>
{
    private readonly HttpClient httpClient;
    private const string ValidCredentials = "dmFsaWQ6Y3JlZHM=";

    public AuthenticationTests(ManagementWebApplicationFactory factory, DatabaseFixture dbFixture)
    {
        var dlcsApiClient = A.Fake<IDlcsApiClient>();
        A.CallTo(() => dlcsApiClient.IsRequestAuthenticated(DatabaseFixture.Customer, A<CancellationToken>._))
            .Returns(true);

        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithTestServices(services => services.AddSingleton(dlcsApiClient))
            .CreateClient();
    }

    [Fact]
    public async Task Health_Returns200_WithoutCredentials()
    {
        var response = await httpClient.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ping_Returns401_IfNoCredentials()
    {
        var response = await httpClient.GetAsync($"/customers/{DatabaseFixture.Customer}/ping");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().ContainSingle(h => h.Scheme == "Basic");
    }

    [Fact]
    public async Task Ping_Returns401_IfDlcsRejectsCredentials()
    {
        // Fake DLCS client only accepts credentials for DatabaseFixture.Customer
        var request = new HttpRequestMessage(HttpMethod.Get, "/customers/100/ping");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", ValidCredentials);

        var response = await httpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ping_Returns200_IfValidCredentials()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/customers/{DatabaseFixture.Customer}/ping");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", ValidCredentials);

        var response = await httpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
