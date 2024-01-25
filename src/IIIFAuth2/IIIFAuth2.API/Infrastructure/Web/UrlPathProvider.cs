using System.Text.RegularExpressions;
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
    /// Get the path oauth2 identity provider will callback after login
    /// </summary>
    Uri GetAccessServiceOAuthCallbackPath(AccessService accessService);

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
    private readonly Regex duplicateSlashRegex = new("(/)+", RegexOptions.Compiled);

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
    public Uri GetAccessServiceOAuthCallbackPath(AccessService accessService)
    {
        const string defaultPathTemplate = "/access/{customerId}/{accessService}/oauth2/callback";
        
        var template = GetTemplate(apiSettings.Auth.OAuthCallbackPathTemplateForDomain, defaultPathTemplate);
        var populatedTemplate = template
            .Replace("{customerId}", accessService.Customer.ToString())
            .Replace("{accessService}", accessService.Name);
        
        var baseUrl = GetCurrentBaseUrl();
        var builder = new UriBuilder(baseUrl)
        {
            Path = populatedTemplate
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
        const string defaultPathTemplate = "/access/{customerId}/gesture";
        
        var template = GetTemplate(apiSettings.Auth.GesturePathTemplateForDomain, defaultPathTemplate);
        var populatedTemplate = template.Replace("{customerId}", customerId.ToString());
        return new Uri(populatedTemplate, UriKind.Relative);
    }

    private string GetTemplate(Dictionary<string, string> pathTemplates, string defaultPathTemplate)
    {
        const string defaultKey = "Default";
        var request = httpContextAccessor.SafeHttpContext().Request;
        var host = request.Host.Value;

        if (pathTemplates.TryGetValue(host, out var hostTemplate)) return hostTemplate;
        if (pathTemplates.TryGetValue(defaultKey, out var pathTemplate)) return pathTemplate;
        if (apiSettings.PathBase.IsNullOrEmpty()) return defaultPathTemplate;

        // Replace any duplicate slashes after joining path elements
        var candidate = $"{apiSettings.PathBase}/{defaultPathTemplate}";
        return duplicateSlashRegex.Replace(candidate, "$1");
    }

    private string GetCurrentBaseUrl() =>
        httpContextAccessor.SafeHttpContext().Request.GetDisplayUrl(null, false);
}
