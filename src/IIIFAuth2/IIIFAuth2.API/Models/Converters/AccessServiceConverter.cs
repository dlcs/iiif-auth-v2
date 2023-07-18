using IIIF;
using IIIF.Auth.V2;
using IIIF.Presentation.V3.Strings;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Models.Domain;

namespace IIIFAuth2.API.Models.Converters;

public static class AccessServiceConverter
{
    public static AuthProbeService2 ToProbeService(this ICollection<AccessService> accessServices, IUrlPathProvider pathProvider, AssetId assetId)
    {
        var probeService = new AuthProbeService2
        {
            Id = pathProvider.GetOrchestratorProbeServicePath(assetId).ToString(),
            Service = new List<IService>(accessServices.Count),
        };

        foreach (var accessService in accessServices)
        {
            var svc = accessService.ToIIIFModel(pathProvider);
            probeService.Service.Add(svc);
        }

        return probeService;
    }
    
    /// <summary>
    /// Converts database entity <see cref="AccessService"/> to <see cref="AuthAccessService2"/>
    /// </summary>
    public static AuthAccessService2 ToIIIFModel(this AccessService accessService, IUrlPathProvider pathProvider)
    {
        var authAccessService = new AuthAccessService2
        {
            Id = pathProvider.GetAccessServicePath(accessService).ToString(),
            Profile = accessService.Profile,
            Label = accessService.Label,
            Heading = accessService.Heading,
            Note = accessService.Note,
            ConfirmLabel = accessService.ConfirmLabel,
            Service = new List<IService>
            {
                GenerateTokenService(accessService, pathProvider),
                GenerateLogoutService(accessService, pathProvider),
            },
        };
        return authAccessService;
    }

    private static IService GenerateTokenService(AccessService accessService, IUrlPathProvider pathProvider)
    {
        var authTokenService = new AuthAccessTokenService2
        {
            Id = pathProvider.GetAccessTokenServicePath(accessService.Customer).ToString(),
            ErrorHeading = accessService.AccessTokenErrorHeading,
            ErrorNote = accessService.AccessTokenErrorNote,
        };
        return authTokenService;
    }
    
    private static IService GenerateLogoutService(AccessService accessService, IUrlPathProvider pathProvider)
    {
        var logoutService = new AuthLogoutService2
        {
            Id = pathProvider.GetAccessServiceLogoutPath(accessService).ToString(),
            Label = accessService.LogoutLabel ?? new LanguageMap("en", $"Logout of {accessService.Name}")
        };
        return logoutService;
    }
}