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
    Uri GetAccessTokenServicePath(int customerId);

    /// <summary>
    /// Get the relative Uri for posting back  
    /// </summary>
    /// <param name="customerId"></param>
    /// <returns></returns>
    Uri GetGesturePostbackRelativePath(int customerId);
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
        var path = $"/auth/v2/access/{accessService.Customer}/{accessService.Name}";
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
        var path = $"/auth/v2/access/{accessService.Customer}/{accessService.Name}/logout";
        var builder = new UriBuilder(baseUrl)
        {
            Path = path
        };

        return builder.Uri;
    }
    
    /// <inheritdoc />
    public Uri GetAccessTokenServicePath(int customerId)
    {
        var baseUrl = GetCurrentBaseUrl();
        var path = $"/auth/v2/access/{customerId}/token";
        var builder = new UriBuilder(baseUrl)
        {
            Path = path
        };

        return builder.Uri;
    }

    /// <inheritdoc />
    public Uri GetGesturePostbackRelativePath(int customerId)
    {
        var request = httpContextAccessor.SafeHttpContext().Request;
        var host = request.Host.Value;

        var template = GetPopulatedTemplate(host, customerId);
        return new Uri(template, UriKind.Relative);
    }
    
    private string GetPopulatedTemplate(string host, int customerId)
    {
        var template = GetTemplate(host);
        return template.Replace("{customerId}", customerId.ToString());
    }
    
    private string GetTemplate(string host)
    {
        const string defaultPathTemplate = "/access/{customerId}/gesture";
        const string defaultKey = "Default";

        var pathTemplates = apiSettings.Auth.GesturePathTemplateForDomain;
        
        if (pathTemplates.TryGetValue(host, out var hostTemplate)) return hostTemplate;
        if (pathTemplates.TryGetValue(defaultKey, out var pathTemplate)) return pathTemplate;
        return defaultPathTemplate;
    }

    private string GetCurrentBaseUrl() =>
        httpContextAccessor.SafeHttpContext().Request.GetDisplayUrl(null, false);
}
