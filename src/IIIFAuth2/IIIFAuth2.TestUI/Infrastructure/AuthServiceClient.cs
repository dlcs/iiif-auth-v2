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

    public Task<HttpResponseMessage> ProbeInternal(Deliverable deliverable, string accessToken,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"probe_internal/{deliverable.Id}?roles={GetRolesString(deliverable)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return httpClient.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Call verifyaccess, passing the Cookie header from the incoming request (Orchestrator uses header propagation)
    /// </summary>
    public Task<HttpResponseMessage> VerifyAccess(Deliverable deliverable, string? cookieHeader,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"verifyaccess/{deliverable.Id}?roles={GetRolesString(deliverable)}");
        if (!string.IsNullOrEmpty(cookieHeader)) request.Headers.Add("Cookie", cookieHeader);
        return httpClient.SendAsync(request, cancellationToken);
    }

    private static string GetRolesString(Deliverable deliverable)
        => string.Join(",", deliverable.Roles.Select(Uri.EscapeDataString));
}
