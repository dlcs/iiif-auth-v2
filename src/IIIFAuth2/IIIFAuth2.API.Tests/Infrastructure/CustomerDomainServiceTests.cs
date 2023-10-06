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
public class CustomerDomainServiceTests
{
    private readonly AuthServicesContext dbContext;
    private const string CurrentHost = "dlcs.test.example";
    private const string CookieHost = "alternative.example";
    private const int CustomerId = 984356;
    private readonly CustomerDomainService sut;

    public CustomerDomainServiceTests(DatabaseFixture dbFixture)
    {
        dbContext = dbFixture.CreateNewAuthServiceContext();
        dbFixture.CleanUp();
        dbContext.CustomerCookieDomains.Add(new CustomerCookieDomain
        {
            Customer = CustomerId, Domains = new List<string> { CookieHost }
        });
        dbContext.SaveChanges();

        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Host = new HostString(CurrentHost);
        request.Scheme = "https";
        var contextAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => contextAccessor.HttpContext).Returns(context);

        sut = new CustomerDomainService(contextAccessor, dbContext, new NullLogger<CustomerDomainService>());
    }
    
    public static IEnumerable<object[]> OriginExpectations => new List<object[]>
    {
        new object[] { $"https://{CurrentHost}", true, "Origin matches host" },
        new object[] { $"https://subdomain.{CurrentHost}", true, "Origin subdomain of host" },
        new object[] { $"https://{CookieHost}", true, "Origin matches cookie domain" },
        new object[] { $"https://subdomain.{CookieHost}", true, "Origin subdomain of cookie domain" },
        new object[] { "https://different.example", false, "Non-matching origin" },
        new object[] { "https://test.example", false, "Origin is apex of host" },
    };

    [Theory]
    [MemberData(nameof(OriginExpectations))]
    public async Task OriginForControlledDomain_Correct(string origin, bool expected, string reason)
    {
        // Arrange
        var originUri = new Uri(origin);

        // Act
        var result = await sut.OriginForControlledDomain(CustomerId, originUri);

        // Assert
        result.Should().Be(expected, reason);
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
    public async Task GetCustomerCookieDomains_ReturnsEmptyCollection_IfNoRecordForCustomer()
    {
        // Act
        var result = await sut.GetCustomerCookieDomains(29292);

        // Assert
        result.Should().BeEmpty();
    }
    
    [Fact]
    public async Task GetCustomerCookieDomains_ReturnsCookieDomains()
    {
        // Arrange
        const int customerId = 2929288;
        var domains = new List<string> { "subdomain.foo.bar" };
        await dbContext.CustomerCookieDomains.AddAsync(new CustomerCookieDomain
        {
            Customer = customerId, Domains = domains
        });
        await dbContext.SaveChangesAsync();
        
        // Act
        var result = await sut.GetCustomerCookieDomains(customerId);

        // Assert
        result.Should().BeEquivalentTo(domains);
    }
}