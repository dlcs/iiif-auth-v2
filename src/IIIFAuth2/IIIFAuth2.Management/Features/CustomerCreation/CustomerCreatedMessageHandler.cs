using System.Text.Json;
using IIIF.Presentation.V3.Strings;
using IIIFAuth2.Core.Data;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Core.Models.Domain;
using IIIFAuth2.Management.Infrastructure.Sqs;
using Microsoft.EntityFrameworkCore;

namespace IIIFAuth2.Management.Features.CustomerCreation;

/// <summary>
/// Handler for customer created messages. Creates a default clickthrough role, access-service and role-provider for
/// new customer.
/// </summary>
public class CustomerCreatedMessageHandler(
    AuthServicesContext dbContext,
    ILogger<CustomerCreatedMessageHandler> logger)
    : MessageHandlerBase<CustomerCreatedMessage>(logger)
{
    private const string ClickthroughName = "clickthrough";
    private const string RoleRoot = "https://api.dlcs.io";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    protected override CustomerCreatedMessage DeserializeMessage(QueueMessage message) =>
        JsonSerializer.Deserialize<CustomerCreatedMessage>(message.Body, JsonSerializerOptions)
        ?? throw new InvalidOperationException($"Unable to deserialize {nameof(CustomerCreatedMessage)}");

    protected override async Task<bool> HandleMessage(CustomerCreatedMessage customerCreatedMessage,
        QueueMessage rawMessage, CancellationToken cancellationToken)
    {
        logger.LogInformation("Received customer created notification for customer {CustomerId} '{CustomerName}'",
            customerCreatedMessage.Id, customerCreatedMessage.Name);

        await EnsureClickthroughAccessService(customerCreatedMessage.Id, cancellationToken);
        return true;
    }

    private async Task EnsureClickthroughAccessService(int customerId, CancellationToken cancellationToken)
    {
        if (await dbContext.AccessServices.AnyAsync(a => a.Customer == customerId && a.Name == ClickthroughName,
                cancellationToken))
        {
            logger.LogInformation("Customer {CustomerId} already has clickthrough access-service, no-op", customerId);
            return;
        }

        var roleProvider = new RoleProvider
        {
            Id = Guid.NewGuid(),
            Customer = customerId,
            Configuration = new RoleProviderConfiguration
            {
                [RoleProviderConfiguration.DefaultKey] = new ClickthroughConfiguration
                {
                    Config = RoleProviderType.Clickthrough
                }
            }
        };

        var accessService = new AccessService
        {
            Id = Guid.NewGuid(),
            Customer = customerId,
            RoleProviderId = roleProvider.Id,
            Name = ClickthroughName,
            Profile = "active",
            Label = new LanguageMap("en", "Confirm access"),
            Heading = new LanguageMap("en", "Confirm access"),
            Note = new LanguageMap("en", "<span>Placeholder content - update before use</span>"),
            ConfirmLabel = new LanguageMap("en", "Accept and Open"),
            LogoutLabel = new LanguageMap("en", "Log out of session"),
            AccessTokenErrorHeading = new LanguageMap("en", "Unable to show content"),
            AccessTokenErrorNote = new LanguageMap("en",
                "<span>Something went wrong confirming access. Please refresh the page and try again.</span>"),
        };

        var role = new Role
        {
            Id = GetRoleId(customerId),
            Customer = customerId,
            AccessServiceId = accessService.Id,
            Name = ClickthroughName,
        };

        logger.LogInformation("Creating clickthrough access-service {AccessServiceId} and role {RoleId} for customer {CustomerId}",
            accessService.Id, role.Id, customerId);

        dbContext.RoleProviders.Add(roleProvider);
        dbContext.AccessServices.Add(accessService);
        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string GetRoleId(int customerId)
        => $"{RoleRoot}/customers/{customerId}/roles/{ClickthroughName}";
}
