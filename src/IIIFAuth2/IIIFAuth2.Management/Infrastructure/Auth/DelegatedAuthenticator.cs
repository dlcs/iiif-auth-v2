using System.Collections.Concurrent;
using System.Net.Http.Headers;
using IIIFAuth2.Management.Infrastructure.Dlcs;
using IIIFAuth2.Management.Settings;
using LazyCache;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.Management.Infrastructure.Auth;

/// <summary>
/// Validate that provided request contains valid auth credentials by proxying to downstream DLCS instance.
/// Any auth header provided here will be proxied to DLCS API - a 200 response = auth successful.
/// </summary>
/// <remarks>Based on DelegatedAuthenticator from iiif-presentation</remarks>
public class DelegatedAuthenticator(
    IDlcsApiClient dlcsApiClient,
    IOptions<ManagementSettings> managementSettings,
    IAppCache appCache,
    ILogger<DelegatedAuthenticator> logger) : IAuthenticator
{
    public async Task<AuthResult> ValidateRequest(HttpRequest request, CancellationToken cancellationToken = default)
    {
        var headerValue = request.TryGetValidAuthHeader();

        if (headerValue == null)
        {
            // Missing or invalid Authorization header
            return AuthResult.NoCredentials;
        }

        var customerId = request.GetCustomerId(logger);
        if (!customerId.HasValue) return AuthResult.NoCredentials;

        return await IsValidUser(headerValue, customerId.Value, cancellationToken)
            ? AuthResult.Success
            : AuthResult.Failed;
    }

    private async Task<bool> IsValidUser(AuthenticationHeaderValue authenticationHeaderValue, int customerId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"auth:{customerId}:{authenticationHeaderValue.Scheme}";
        var authParameter = authenticationHeaderValue.Parameter!;

        var list = await appCache.GetAsync<ConcurrentBag<string>>(cacheKey);
        if (list != null && list.Contains(authParameter)) return true;

        var isValid = await dlcsApiClient.IsRequestAuthenticated(customerId, cancellationToken);
        if (!isValid) return false;

        list ??= [];
        list.Add(authParameter);
        appCache.Add(cacheKey, list, DateTimeOffset.UtcNow.AddSeconds(managementSettings.Value.AuthCacheTtl));
        return true;
    }
}
