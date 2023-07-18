using IIIF;
using IIIF.Auth.V2;
using IIIF.Presentation.V3.Strings;
using IIIFAuth2.API.Data.Entities;

namespace IIIFAuth2.API.Models.Converters;

public static class AccessServiceConverter
{
    public static AuthProbeService2 ToProbeService(this ICollection<AccessService> accessServices)
    {
        var probeService = new AuthProbeService2
        {
            Id = "todo", // TODO
            Service = new List<IService>(accessServices.Count),
        };

        foreach (var accessService in accessServices)
        {
            var svc = accessService.ToIIIFModel();
            probeService.Service.Add(svc);
        }

        return probeService;
    }
    
    /// <summary>
    /// Converts database entity <see cref="AccessService"/> to <see cref="AuthAccessService2"/>
    /// </summary>
    public static AuthAccessService2 ToIIIFModel(this AccessService accessService)
    {
        var authAccessService = new AuthAccessService2
        {
            Id = "todo", // TODO
            Profile = accessService.Profile,
            Label = accessService.Label,
            Heading = accessService.Heading,
            Note = accessService.Note,
            ConfirmLabel = accessService.ConfirmLabel,
            Service = new List<IService>
            {
                GenerateTokenService(accessService),
                GenerateLogoutService(accessService),
            },
        };
        return authAccessService;
    }

    private static IService GenerateTokenService(AccessService accessService)
    {
        var authTokenService = new AuthAccessTokenService2
        {
            Id = "todo", // TODO
            ErrorHeading = accessService.AccessTokenErrorHeading,
            ErrorNote = accessService.AccessTokenErrorNote,
        };
        return authTokenService;
    }
    
    private static IService GenerateLogoutService(AccessService accessService)
    {
        var logoutService = new AuthLogoutService2
        {
            Id = "todo", // TODO
            Label = accessService.LogoutLabel ?? new LanguageMap("en", $"Logout of {accessService.Name}")
        };
        return logoutService;
    }
}