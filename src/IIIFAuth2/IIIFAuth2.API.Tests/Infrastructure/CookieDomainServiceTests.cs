using FakeItEasy;
using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Auth;
using IIIFAuth2.API.Tests.TestingInfrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace IIIFAuth2.API.Tests.Infrastructure;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class CookieDomainServiceTests
{
    private const string CurrentHost = "test.example";
    private readonly AuthServicesContext dbContext;
    private readonly CustomerDomainService sut;

    public CookieDomainServiceTests(DatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        dbFixture.CleanUp();
        
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Host = new HostString(CurrentHost);
        request.Scheme = "https";
        var contextAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => contextAccessor.HttpContext).Returns(context);

        sut = new CustomerDomainService(contextAccessor, dbContext, new NullLogger<CustomerDomainService>());
    }

    [Fact]
    public async Task OriginForControlledDomain_True_IfOriginMatchesHost()
    {
        // Arrange
        var origin = new Uri($"Https://{CurrentHost}");

        // Act
        var result = await sut.OriginForControlledDomain(123, origin);
        
        // Assert
        result.Should().BeTrue();
    }
    
    [Fact]
    public async Task OriginForControlledDomain_True_IfMatchesCookieDomain()
    {
        // Arrange
        const int customerId = 123;
        await dbContext.CustomerCookieDomains.AddAsync(new CustomerCookieDomain
        {
            Customer = customerId, Domains = new List<string> { "alpha.bar", "foo.bar" }
        });
        await dbContext.SaveChangesAsync();
        var origin = new Uri("https://foo.bar");

        // Act
        var result = await sut.OriginForControlledDomain(customerId, origin);
        
        // Assert
        result.Should().BeTrue();
    }
    
    [Fact]
    public async Task OriginForControlledDomain_True_IfOriginSubdomainOfCookieDomain()
    {
        // Arrange
        const int customerId = 1234;
        await dbContext.CustomerCookieDomains.AddAsync(new CustomerCookieDomain
        {
            Customer = customerId, Domains = new List<string> { "foo.bar" }
        });
        await dbContext.SaveChangesAsync();
        var origin = new Uri("https://subdomain.foo.bar");

        // Act
        var result = await sut.OriginForControlledDomain(customerId, origin);
        
        // Assert
        result.Should().BeTrue();
    }
    
    [Fact]
    public async Task OriginForControlledDomain_False_IfOriginNotHostAndNoCustomerCookieDomains()
    {
        // Arrange
        var origin = new Uri("https://foo.bar");

        // Act
        var result = await sut.OriginForControlledDomain(999, origin);
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public async Task OriginForControlledDomain_False_IfOriginNotHostAndCustomerCookieDomainsNotSubdomain()
    {
        // Arrange
        const int customerId = 12345;
        await dbContext.CustomerCookieDomains.AddAsync(new CustomerCookieDomain
        {
            Customer = customerId, Domains = new List<string> { "subdomain.foo.bar" }
        });
        await dbContext.SaveChangesAsync();
        var origin = new Uri("https://foo.different");

        // Act
        var result = await sut.OriginForControlledDomain(customerId, origin);
        
        // Assert
        result.Should().BeFalse();
    }
}