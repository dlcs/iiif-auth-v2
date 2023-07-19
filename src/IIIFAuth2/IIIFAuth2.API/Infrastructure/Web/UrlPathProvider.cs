using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Settings;
using IIIFAuth2.API.Utils;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.API.Infrastructure.Web;

public interface IUrlPathProvider
{
    /// <summary>
    /// Get the path for Orchestrator probe service - this will be the publicly called endpoint
    /// </summary>
    Uri GetOrchestratorProbeServicePath(AssetId assetId);

    /// <summary>
    /// Get the path for AccessService
    /// </summary>
    Uri GetAccessServicePath(AccessService accessService);

    /// <summary>
    /// Get the path for AccessService logout
    /// </summary>
    Uri GetAccessServiceLogoutPath(AccessService accessService);

    /// <summary>
    /// Get the path for AccessService
    /// </summary>
    Uri GetAccessTokenServicePath(int customer);
}

public class UrlPathProvider : IUrlPathProvider
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ApiSettings apiSettings;

    public UrlPathProvider(IHttpContextAccessor httpContextAccessor, IOptions<ApiSettings> apiOptions)
    {
        apiSettings = apiOptions.Value;
        this.httpContextAccessor = httpContextAccessor;
    }
    
    /// <inheritdoc />
    public Uri GetOrchestratorProbeServicePath(AssetId assetId)
    {
        var orchestratorUrl = apiSettings.OrchestratorRoot;
        var path = $"/auth/v2/probe/{assetId}";
        var builder = new UriBuilder(orchestratorUrl)
        {
            Path = path
        };

        return builder.Uri;
    }

    /// <inheritdoc />
    public Uri GetAccessServicePath(AccessService accessService)
    {
        var baseUrl = GetCurrentBaseUrl();
        var path = $"/auth/v2/{accessService.Customer}/{accessService.Name}";
        var builder = new UriBuilder(baseUrl)
        {
            Path = path
        };

        return builder.Uri;
    }
    
    /// <inheritdoc />
    public Uri GetAccessServiceLogoutPath(AccessService accessService)
    {
        var baseUrl = GetCurrentBaseUrl();
        var path = $"/auth/v2/{accessService.Customer}/{accessService.Name}/logout";
        var builder = new UriBuilder(baseUrl)
        {
            Path = path
        };

        return builder.Uri;
    }
    
    /// <inheritdoc />
    public Uri GetAccessTokenServicePath(int customer)
    {
        var baseUrl = GetCurrentBaseUrl();
        var path = $"/auth/v2/{customer}/token";
        var builder = new UriBuilder(baseUrl)
        {
            Path = path
        };

        return builder.Uri;
    }

    private string GetCurrentBaseUrl() =>
        httpContextAccessor.HttpContext?.Request.GetDisplayUrl(null, false) ?? string.Empty;
}
