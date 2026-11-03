using IIIFAuth2.API.Infrastructure.Auth.Models;
using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Settings;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Core.Models.Domain;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Appointments;

/// <summary>
/// Handles RoleProvisioning for appointment configurations
/// </summary>
public class AppointmentProviderHandler
{
    private readonly IUrlPathProvider urlPathProvider;
    private readonly ApiSettings apiSettings;

    public AppointmentProviderHandler(IUrlPathProvider urlPathProvider, IOptions<ApiSettings> apiOptions)
    {
        this.urlPathProvider = urlPathProvider;
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

    private AppointmentKeyModel GetAppointmentKeyModel(Uri requestOrigin, AccessService accessService,
        AppointmentConfiguration configuration, string? error = null)
        => new(
            urlPathProvider.GetAccessServicePath(accessService),
            configuration.GestureTitle ?? apiSettings.DefaultSignificantGestureTitle,
            configuration.GestureMessage ?? apiSettings.DefaultSignificantGestureMessage,
            requestOrigin.ToString(),
            error);
}
