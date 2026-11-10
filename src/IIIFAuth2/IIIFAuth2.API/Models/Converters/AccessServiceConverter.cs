using IIIF;
using IIIF.Auth.V2;
using IIIF.Presentation.V3.Strings;
using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Core.Models.Domain;

namespace IIIFAuth2.API.Models.Converters;

public static class AccessServiceConverter
{
    public static AuthProbeService2 ToProbeService(this ICollection<AccessService> accessServices,
        IUrlPathProvider pathProvider, DeliverableId deliverableId, bool useGenericLocationWording)
    {
        var probeService = new AuthProbeService2
        {
            Id = pathProvider.GetOrchestratorProbeServicePath(deliverableId).ToString(),
            Service = new List<IService>(accessServices.Count),
        };

        foreach (var accessService in accessServices)
        {
            var svc = accessService.ToIIIFModel(pathProvider, useGenericLocationWording);
            probeService.Service.Add(svc);
        }

        return probeService;
    }
    
    /// <summary>
    /// Converts database entity <see cref="AccessService"/> to <see cref="AuthAccessService2"/>
    /// </summary>
    public static AuthAccessService2 ToIIIFModel(this AccessService accessService, IUrlPathProvider pathProvider,
        bool useGenericLocationWording)
    {
        if (accessService.Profile == AccessServiceProfiles.External)
            return accessService.ToExternalIIIFModel(pathProvider, useGenericLocationWording);

        var authAccessService = new AuthAccessService2
        {
            Id = pathProvider.GetAccessServicePath(accessService).ToString(),
            Profile = accessService.Profile,
            Label = accessService.Label,
            Heading = accessService.Heading,
            Note = accessService.Note,
            ConfirmLabel = accessService.ConfirmLabel,
            Service =
            [
                GenerateTokenService(accessService, pathProvider),
                GenerateLogoutService(accessService, pathProvider)
            ],
        };
        return authAccessService;
    }

    /// <summary>
    /// Converts database entity <see cref="AccessService"/> with "external" profile to <see cref="AuthAccessService2"/>.
    /// As there is no session to end it has no logout service.
    /// </summary>
    /// <remarks>
    /// The spec says the id SHOULD NOT be present for external services and that clients MUST ignore it. It is
    /// output so the service can be referenced, e.g. declared once in a Manifest's services and referenced by id. If
    /// a client does open it, the access service returns a "not interactive" error and the window closes.
    /// </remarks>
    private static AuthAccessService2 ToExternalIIIFModel(this AccessService accessService,
        IUrlPathProvider pathProvider, bool useGenericLocationWording)
    {
        var locationPhrase = GetLocationPhrase(accessService, useGenericLocationWording);
        var authAccessService = new AuthAccessService2
        {
            Id = pathProvider.GetAccessServicePath(accessService).ToString(),
            Profile = AccessServiceProfiles.External,
            Label = accessService.Label ?? new LanguageMap("en",
                locationPhrase == null ? "Available at permitted locations" : $"Available {locationPhrase}"),
            Heading = accessService.Heading,
            Note = accessService.Note,
            Service =
            [
                GenerateExternalTokenService(accessService, pathProvider, locationPhrase)
            ],
        };
        return authAccessService;
    }

    /// <summary>
    /// Get locationPhrase of IpRestriction role-provider, or null if not set or generic wording is to be used.
    /// </summary>
    /// <remarks>
    /// Wording only, so uses default configuration rather than host-specific
    /// </remarks>
    private static string? GetLocationPhrase(AccessService accessService, bool useGenericLocationWording)
    {
        if (useGenericLocationWording) return null;

        IProviderConfiguration? configuration = null;
        accessService.RoleProvider?.Configuration.TryGetValue(RoleProviderConfiguration.DefaultKey, out configuration);
        var locationPhrase = (configuration as IpRestrictionConfiguration)?.LocationPhrase;
        return string.IsNullOrWhiteSpace(locationPhrase) ? null : locationPhrase;
    }

    /// <summary>
    /// Generate token service for "external" access service. The token service is used immediately by clients for
    /// external services, which will fail if the client isn't at a permitted location (no session), so if not
    /// configured the error heading and note default to location wording, matching probe wording.
    /// </summary>
    private static AuthAccessTokenService2 GenerateExternalTokenService(AccessService accessService,
        IUrlPathProvider pathProvider, string? locationPhrase)
    {
        var authTokenService = GenerateTokenService(accessService, pathProvider);
        if (authTokenService.ErrorHeading != null) return authTokenService;

        // errorHeading must be present if errorNote is, so heading is defaulted even if a note is configured
        authTokenService.ErrorHeading = new LanguageMap("en", "Not available at your location");
        authTokenService.ErrorNote ??= new LanguageMap("en",
            locationPhrase == null ? "Only available from permitted locations" : $"Only available {locationPhrase}");
        return authTokenService;
    }

    private static AuthAccessTokenService2 GenerateTokenService(AccessService accessService,
        IUrlPathProvider pathProvider)
    {
        var authTokenService = new AuthAccessTokenService2
        {
            Id = pathProvider.GetAccessTokenServicePath(accessService.Customer).ToString(),
            ErrorHeading = accessService.AccessTokenErrorHeading,
            ErrorNote = accessService.AccessTokenErrorNote,
        };
        return authTokenService;
    }

    private static AuthLogoutService2 GenerateLogoutService(AccessService accessService, IUrlPathProvider pathProvider)
    {
        var logoutService = new AuthLogoutService2
        {
            Id = pathProvider.GetAccessServiceLogoutPath(accessService).ToString(),
            Label = accessService.LogoutLabel ?? new LanguageMap("en", $"Logout of {accessService.Name}")
        };
        return logoutService;
    }
}