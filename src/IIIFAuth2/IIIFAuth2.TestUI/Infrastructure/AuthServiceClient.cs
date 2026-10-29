using System.Net.Http.Headers;

namespace IIIFAuth2.TestUI.Infrastructure;

/// <summary>
/// Client for iiif-auth-v2, making the same requests as Orchestrator's IIIFAuth2Client. Raw responses are returned so
/// callers can decide how to handle them.
/// </summary>
public class AuthServiceClient(HttpClient httpClient)
{
    public Task<HttpResponseMessage> GetServices(Deliverable deliverable, CancellationToken cancellationToken)
        => httpClient.GetAsync($"services/{deliverable.Id}?roles={GetRolesString(deliverable)}", cancellationToken);

    /// <summary>
    /// Call probe_internal, passing access token as Bearer token (if present) and client IP as x-forwarded-for (if
    /// present)
    /// </summary>
    public Task<HttpResponseMessage> ProbeInternal(Deliverable deliverable, string? accessToken, string? forwardedFor,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"probe_internal/{deliverable.Id}?roles={GetRolesString(deliverable)}");
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        AddForwardedFor(request, forwardedFor);
        return httpClient.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Call verifyaccess, passing the Cookie header from the incoming request (Orchestrator uses header propagation)
    /// and client IP as x-forwarded-for (if present)
    /// </summary>
    public Task<HttpResponseMessage> VerifyAccess(Deliverable deliverable, string? cookieHeader, string? forwardedFor,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"verifyaccess/{deliverable.Id}?roles={GetRolesString(deliverable)}");
        if (!string.IsNullOrEmpty(cookieHeader)) request.Headers.Add("Cookie", cookieHeader);
        AddForwardedFor(request, forwardedFor);
        return httpClient.SendAsync(request, cancellationToken);
    }

    private static void AddForwardedFor(HttpRequestMessage request, string? forwardedFor)
    {
        if (!string.IsNullOrEmpty(forwardedFor)) request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
    }

    private static string GetRolesString(Deliverable deliverable)
        => string.Join(",", deliverable.Roles.Select(Uri.EscapeDataString));
}
