using IIIFAuth2.API.Infrastructure.Auth.Models;
using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Settings;
using IIIFAuth2.Core.Data;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Core.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Appointments;

/// <summary>
/// Handles RoleProvisioning for appointment configurations
/// </summary>
public class AppointmentProviderHandler
{
    public const string UnknownKeyError = "Appointment key not recognised";
    public const string EndedError = "This appointment has ended";

    private readonly AuthServicesContext dbContext;
    private readonly SessionManagementService sessionManagementService;
    private readonly IUrlPathProvider urlPathProvider;
    private readonly ApiSettings apiSettings;
    private readonly ILogger<AppointmentProviderHandler> logger;

    public AppointmentProviderHandler(
        AuthServicesContext dbContext,
        SessionManagementService sessionManagementService,
        IUrlPathProvider urlPathProvider,
        IOptions<ApiSettings> apiOptions,
        ILogger<AppointmentProviderHandler> logger)
    {
        this.dbContext = dbContext;
        this.sessionManagementService = sessionManagementService;
        this.urlPathProvider = urlPathProvider;
        this.logger = logger;
        apiSettings = apiOptions.Value;
    }

    /// <summary>
    /// Handle initial request to access service, this renders a form for the user to enter their appointment key
    /// </summary>
    public HandleRoleProvisionResponse InitiateRequest(Uri requestOrigin, AccessService accessService,
        IProviderConfiguration providerConfiguration)
    {
        var configuration = providerConfiguration.SafelyGetTypedConfig<AppointmentConfiguration>();
        return HandleRoleProvisionResponse.AppointmentKey(
            GetAppointmentKeyModel(requestOrigin, accessService, configuration));
    }

    /// <summary>
    /// Handle appointment key posted from form. If the key is for an appointment that hasn't ended it is added to the
    /// user's session (or a new session is created) and a cookie is issued. Otherwise the form is re-rendered with an
    /// error.
    /// </summary>
    /// <remarks>
    /// Appointments that are not yet started or not active are accepted, the probe service explains why access is
    /// denied for these
    /// </remarks>
    public async Task<HandleRoleProvisionResponse> CompleteRequest(int customerId, string? appointmentKey,
        Uri requestOrigin, AccessService accessService, IProviderConfiguration providerConfiguration,
        CancellationToken cancellationToken = default)
    {
        var configuration = providerConfiguration.SafelyGetTypedConfig<AppointmentConfiguration>();
        
        var appointment = await GetAppointment(customerId, accessService, appointmentKey, cancellationToken);
        if (appointment == null)
        {
            logger.LogInformation("Unknown appointment key entered for {CustomerId}:{AccessServiceName}", customerId,
                accessService.Name);
            return HandleRoleProvisionResponse.AppointmentKey(
                GetAppointmentKeyModel(requestOrigin, accessService, configuration, UnknownKeyError));
        }

        if (appointment.End <= DateTime.UtcNow)
        {
            logger.LogInformation("Ended appointment {AppointmentId} key entered", appointment.Id);
            return HandleRoleProvisionResponse.AppointmentKey(
                GetAppointmentKeyModel(requestOrigin, accessService, configuration, EndedError));
        }

        await sessionManagementService.AddAppointmentToSession(customerId, appointment.Id, requestOrigin.ToString(),
            cancellationToken);
        return HandleRoleProvisionResponse.Handled();
    }

    private async Task<Appointment?> GetAppointment(int customerId, AccessService accessService,
        string? appointmentKey, CancellationToken cancellationToken)
    {
        // No appointment caching, just lookup by token. Expectation is the user will enter their token correctly
        var token = appointmentKey?.Trim();
        var appointment = string.IsNullOrEmpty(token)
            ? null
            : await dbContext.Appointments.AsNoTracking().SingleOrDefaultAsync(a =>
                    a.Customer == customerId && a.Token == token && a.RoleProviderId == accessService.RoleProviderId,
                cancellationToken);
        return appointment;
    }

    private AppointmentKeyModel GetAppointmentKeyModel(Uri requestOrigin, AccessService accessService,
        AppointmentConfiguration configuration, string? error = null)
        => new(
            urlPathProvider.GetAccessServicePath(accessService),
            configuration.GestureTitle ?? apiSettings.DefaultSignificantGestureTitle,
            configuration.GestureMessage ?? apiSettings.DefaultSignificantGestureMessage,
            requestOrigin.ToString(),
            error);
}
