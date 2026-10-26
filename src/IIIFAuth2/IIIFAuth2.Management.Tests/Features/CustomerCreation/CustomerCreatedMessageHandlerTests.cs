using IIIF.Presentation.V3.Strings;
using IIIFAuth2.Core.Data;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Core.Models.Domain;
using IIIFAuth2.Management.Features.CustomerCreation;
using IIIFAuth2.Management.Infrastructure.Sqs;
using IIIFAuth2.Management.Tests.TestingInfrastructure;
using IIIFAuth2.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IIIFAuth2.Management.Tests.Features.CustomerCreation;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class CustomerCreatedMessageHandlerTests
{
    private readonly DatabaseFixture dbFixture;
    private readonly AuthServicesContext dbContext;
    private readonly CustomerCreatedMessageHandler sut;

    public CustomerCreatedMessageHandlerTests(DatabaseFixture dbFixture)
    {
        this.dbFixture = dbFixture;
        dbContext = dbFixture.DbContext;
        sut = new CustomerCreatedMessageHandler(dbFixture.CreateNewAuthServiceContext(),
            new NullLogger<CustomerCreatedMessageHandler>());

        dbFixture.CleanUp();
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("null")]
    public async Task HandleMessage_False_IfMessageInvalid(string body)
    {
        var message = GetMessage(body);

        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeFalse();

        await ShouldOnlyContainSeededAccessServices();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"name\":\"test\"}")]
    public async Task HandleMessage_False_IfIdMissing(string body)
    {
        var message = GetMessage(body);

        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeFalse();

        await ShouldOnlyContainSeededAccessServices();
        (await dbContext.Roles.AnyAsync(r => r.Customer == 0)).Should().BeFalse();
    }

    [Fact]
    public async Task HandleMessage_True_AndCreatesClickthrough_IfNotExists()
    {
        // Arrange
        const int customerId = -100;
        var message = GetMessage(customerId);

        // Act
        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();

        // Assert
        var accessService = await dbContext.AccessServices
            .AsNoTracking()
            .Include(a => a.RoleProvider)
            .SingleAsync(a => a.Customer == customerId);
        accessService.Name.Should().Be("clickthrough");
        accessService.Profile.Should().Be("active");
        accessService.Label.Should().BeEquivalentTo(new LanguageMap("en", "Confirm access"));
        accessService.Heading.Should().BeEquivalentTo(new LanguageMap("en", "Confirm access"));
        accessService.Note.Should().BeEquivalentTo(new LanguageMap("en",
            "<span>Placeholder content - update before use</span>"));
        accessService.ConfirmLabel.Should().BeEquivalentTo(new LanguageMap("en", "Accept and Open"));
        accessService.LogoutLabel.Should().BeEquivalentTo(new LanguageMap("en", "Log out of session"));
        accessService.AccessTokenErrorHeading.Should().BeEquivalentTo(new LanguageMap("en", "Unable to show content"));
        accessService.AccessTokenErrorNote.Should().BeEquivalentTo(new LanguageMap("en",
            "<span>Something went wrong confirming access. Please refresh the page and try again.</span>"));

        var roleProvider = accessService.RoleProvider!;
        roleProvider.Customer.Should().Be(customerId);
        roleProvider.Configuration.Should().ContainSingle()
            .Which.Key.Should().Be(RoleProviderConfiguration.DefaultKey);
        roleProvider.Configuration.GetConfiguration("any-host").Should().BeOfType<ClickthroughConfiguration>()
            .Which.Config.Should().Be(RoleProviderType.Clickthrough);

        var role = await dbContext.Roles.AsNoTracking().SingleAsync(r => r.Customer == customerId);
        role.Id.Should().Be($"https://api.dlcs.io/customers/{customerId}/roles/clickthrough");
        role.Name.Should().Be("clickthrough");
        role.AccessServiceId.Should().Be(accessService.Id);
    }

    [Fact]
    public async Task HandleMessage_True_AndCreatesClickthrough_IfPropertyNamesPascalCase()
    {
        const int customerId = -101;
        var message = GetMessage($"{{\"Name\":\"test\",\"Id\":{customerId}}}");

        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();

        (await dbContext.AccessServices.CountAsync(a => a.Customer == customerId)).Should().Be(1);
    }

    [Fact]
    public async Task HandleMessage_True_AndCreatesClickthrough_IfNameMissing()
    {
        const int customerId = -102;
        var message = GetMessage($"{{\"id\":{customerId}}}");

        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();

        (await dbContext.AccessServices.CountAsync(a => a.Customer == customerId)).Should().Be(1);
    }

    [Fact]
    public async Task HandleMessage_True_AndNoOp_IfMessageReceivedTwice()
    {
        // Arrange
        const int customerId = -103;
        var message = GetMessage(customerId);
        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();

        // Act
        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();

        // Assert
        await ShouldHaveClickthroughRecordCounts(customerId, 1);
    }

    [Fact]
    public async Task HandleMessage_True_AndNoOp_IfCustomerAlreadyHasClickthrough()
    {
        // Arrange - DatabaseFixture seeds a "clickthrough" access-service for customer
        const int customerId = DatabaseFixture.Customer;
        var accessServicesBefore = await dbContext.AccessServices.CountAsync(a => a.Customer == customerId);
        var roleProvidersBefore = await dbContext.RoleProviders.CountAsync(r => r.Customer == customerId);
        var rolesBefore = await dbContext.Roles.CountAsync(r => r.Customer == customerId);

        // Act
        (await sut.HandleMessage(GetMessage(customerId), CancellationToken.None)).Should().BeTrue();

        // Assert
        (await dbContext.AccessServices.CountAsync(a => a.Customer == customerId)).Should().Be(accessServicesBefore);
        (await dbContext.RoleProviders.CountAsync(r => r.Customer == customerId)).Should().Be(roleProvidersBefore);
        (await dbContext.Roles.CountAsync(r => r.Customer == customerId)).Should().Be(rolesBefore);
        (await dbContext.Roles.AnyAsync(r => r.Id == $"https://api.dlcs.io/customers/{customerId}/roles/clickthrough"))
            .Should().BeFalse("existing clickthrough has different role id, no new role should be created");
    }

    [Fact]
    public async Task HandleMessage_True_AndCreatesClickthrough_IfOtherCustomerHasClickthrough()
    {
        // Arrange
        const int otherCustomerId = -104;
        const int customerId = -105;
        (await sut.HandleMessage(GetMessage(otherCustomerId), CancellationToken.None)).Should().BeTrue();

        // Act
        (await sut.HandleMessage(GetMessage(customerId), CancellationToken.None)).Should().BeTrue();

        // Assert
        await ShouldHaveClickthroughRecordCounts(customerId, 1);
        await ShouldHaveClickthroughRecordCounts(otherCustomerId, 1);
    }

    [Fact]
    public async Task HandleMessage_True_AndCreatesClickthrough_IfCustomerHasNonClickthroughAccessService()
    {
        // Arrange
        const int customerId = -106;
        dbContext.AccessServices.Add(new AccessService
        {
            Id = Guid.NewGuid(), Customer = customerId, Name = "oidc", Profile = "active"
        });
        await dbContext.SaveChangesAsync();

        // Act
        (await sut.HandleMessage(GetMessage(customerId), CancellationToken.None)).Should().BeTrue();

        // Assert
        (await dbContext.AccessServices.Where(a => a.Customer == customerId).Select(a => a.Name).ToListAsync())
            .Should().BeEquivalentTo("oidc", "clickthrough");
        (await dbContext.Roles.CountAsync(r => r.Customer == customerId)).Should().Be(1);
    }

    private async Task ShouldOnlyContainSeededAccessServices()
        => (await dbContext.AccessServices.Select(a => a.Id).ToListAsync())
            .Should().BeEquivalentTo([dbFixture.ClickthroughAccessId, dbFixture.OidcAccessId]);

    private async Task ShouldHaveClickthroughRecordCounts(int customerId, int expected)
    {
        (await dbContext.AccessServices.CountAsync(a => a.Customer == customerId)).Should().Be(expected);
        (await dbContext.RoleProviders.CountAsync(r => r.Customer == customerId)).Should().Be(expected);
        (await dbContext.Roles.CountAsync(r => r.Customer == customerId)).Should().Be(expected);
    }

    private static QueueMessage GetMessage(int customerId)
        => GetMessage($"{{\"name\":\"test\",\"id\":{customerId}}}");

    private static QueueMessage GetMessage(string body) => new(body, new Dictionary<string, string>(), "foo");
}
