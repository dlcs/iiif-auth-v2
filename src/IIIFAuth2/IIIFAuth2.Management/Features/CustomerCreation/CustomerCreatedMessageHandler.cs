using System.Text.Json;
using IIIF.Presentation.V3.Strings;
using IIIFAuth2.Core.Data;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Core.Models.Domain;
using IIIFAuth2.Management.Infrastructure.Sqs;
using Microsoft.EntityFrameworkCore;

namespace IIIFAuth2.Management.Features.CustomerCreation;

/// <summary>
/// Handler for customer created messages. Creates a default clickthrough and appointment role, access-service and
/// role-provider for new customer.
/// </summary>
public class CustomerCreatedMessageHandler(
    AuthServicesContext dbContext,
    ILogger<CustomerCreatedMessageHandler> logger)
    : MessageHandlerBase<CustomerCreatedMessage>(logger)
{
    private const string ClickthroughName = "clickthrough";
    private const string AppointmentName = "appointment";
    private const string AppointmentRoleName = "appointment:token-based";
    private const string RoleRoot = "https://api.dlcs.io";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    protected override CustomerCreatedMessage DeserializeMessage(QueueMessage message) =>
        JsonSerializer.Deserialize<CustomerCreatedMessage>(message.Body, JsonSerializerOptions)
        ?? throw new InvalidOperationException($"Unable to deserialize {nameof(CustomerCreatedMessage)}");

    protected override async Task<bool> HandleMessage(CustomerCreatedMessage customerCreatedMessage,
        QueueMessage rawMessage, CancellationToken cancellationToken)
    {
        var customerId = customerCreatedMessage.Id;
        logger.LogInformation("Received customer created notification for customer {CustomerId} '{CustomerName}'",
            customerId, customerCreatedMessage.Name);

        await EnsureAccessService(customerId, ClickthroughName,
            new ClickthroughConfiguration { Config = RoleProviderType.Clickthrough },
            new AccessService
            {
                Name = ClickthroughName,
                Label = new LanguageMap("en", "Confirm access"),
                Heading = new LanguageMap("en", "Confirm access"),
                Note = new LanguageMap("en", "<span>Placeholder content - update before use</span>"),
                ConfirmLabel = new LanguageMap("en", "Accept and Open"),
                LogoutLabel = new LanguageMap("en", "Log out of session"),
                AccessTokenErrorHeading = new LanguageMap("en", "Unable to show content"),
                AccessTokenErrorNote = new LanguageMap("en",
                    "<span>Something went wrong confirming access. Please refresh the page and try again.</span>"),
            }, cancellationToken);

        await EnsureAccessService(customerId, AppointmentRoleName,
            new AppointmentConfiguration
            {
                Config = RoleProviderType.Appointment,
                GestureTitle = "Use your appointment key",
                GestureMessage = "Enter the key from your appointment confirmation",
            },
            new AccessService
            {
                Name = AppointmentName,
                Label = new LanguageMap("en", "Appointment key"),
                Heading = new LanguageMap("en", "Viewing by appointment"),
                Note = new LanguageMap("en", "Enter the key from your appointment confirmation."),
                ConfirmLabel = new LanguageMap("en", "Enter key"),
                LogoutLabel = new LanguageMap("en", "End appointment session"),
            }, cancellationToken);

        // Single save so a failed message creates nothing, and is safe to retry
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Add an active <paramref name="accessService"/>, with role-provider and a single role, unless customer already
    /// has an access-service with the same name
    /// </summary>
    private async Task EnsureAccessService(int customerId, string roleName, IProviderConfiguration configuration,
        AccessService accessService, CancellationToken cancellationToken)
    {
        if (await dbContext.AccessServices.AnyAsync(a => a.Customer == customerId && a.Name == accessService.Name,
                cancellationToken))
        {
            logger.LogInformation("Customer {CustomerId} already has {AccessServiceName} access-service, no-op",
                customerId, accessService.Name);
            return;
        }

        var roleProvider = new RoleProvider
        {
            Id = Guid.NewGuid(),
            Customer = customerId,
            Configuration = new RoleProviderConfiguration { [RoleProviderConfiguration.DefaultKey] = configuration }
        };

        accessService.Id = Guid.NewGuid();
        accessService.Customer = customerId;
        accessService.RoleProviderId = roleProvider.Id;
        accessService.Profile = AccessServiceProfiles.Active;

        var role = new Role
        {
            Id = $"{RoleRoot}/customers/{customerId}/roles/{roleName}",
            Customer = customerId,
            AccessServiceId = accessService.Id,
            Name = roleName,
        };

        logger.LogInformation(
            "Creating {AccessServiceName} access-service {AccessServiceId} and role {RoleId} for customer {CustomerId}",
            accessService.Name, accessService.Id, role.Id, customerId);

        dbContext.RoleProviders.Add(roleProvider);
        dbContext.AccessServices.Add(accessService);
        dbContext.Roles.Add(role);
    }
}
