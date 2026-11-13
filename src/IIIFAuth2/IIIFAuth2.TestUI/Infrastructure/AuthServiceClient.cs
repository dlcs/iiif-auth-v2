using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IIIFAuth2.TestUI.Infrastructure;

/// <summary>
/// Client for iiif-auth-v2, making the same requests as Orchestrator's IIIFAuth2Client. Raw responses are returned so
/// callers can decide how to handle them.
/// </summary>
/// <remarks>
/// probe_internal and verifyaccess are POSTed, simulating the proposed Orchestrator changes for appointment roles.
/// Roles are sent as entries (string[][]), simulating the proposed Orchestrator changes for role combinations
/// </remarks>
public class AuthServiceClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Task<HttpResponseMessage> GetServices(Deliverable deliverable, CancellationToken cancellationToken)
        => httpClient.GetAsync($"services/{deliverable.Id}?roles={GetRolesString(deliverable)}", cancellationToken);

    /// <summary>
    /// POST to probe_internal, passing access token as Bearer token (if present) and client IP as x-forwarded-for (if
    /// present). Body is roles plus asset metadata, if any role is an appointment role
    /// </summary>
    public Task<HttpResponseMessage> ProbeInternal(Deliverable deliverable, string? accessToken, string? forwardedFor,
        CancellationToken cancellationToken)
    {
        var request = CreateAccessCheckRequest($"probe_internal/{deliverable.Id}", deliverable, forwardedFor);
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        return httpClient.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// POST to verifyaccess, passing the Cookie header from the incoming request (Orchestrator uses header
    /// propagation) and client IP as x-forwarded-for (if present). Body as <see cref="ProbeInternal"/>
    /// </summary>
    public Task<HttpResponseMessage> VerifyAccess(Deliverable deliverable, string? cookieHeader, string? forwardedFor,
        CancellationToken cancellationToken)
    {
        var request = CreateAccessCheckRequest($"verifyaccess/{deliverable.Id}", deliverable, forwardedFor);
        if (!string.IsNullOrEmpty(cookieHeader)) request.Headers.Add("Cookie", cookieHeader);
        return httpClient.SendAsync(request, cancellationToken);
    }

    private static HttpRequestMessage CreateAccessCheckRequest(string path, Deliverable deliverable,
        string? forwardedFor)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(GetBody(deliverable), options: JsonOptions)
        };
        if (!string.IsNullOrEmpty(forwardedFor)) request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        return request;
    }

    /// <summary>
    /// Asset metadata only sent if there are appointment roles, as Orchestrator would only load it then. Scopes are
    /// sent as "manifests", as current Protagonist names them, to exercise iiif-auth-v2 fallback
    /// </summary>
    private static object GetBody(Deliverable deliverable)
    {
        var asset = deliverable.Asset;
        return new
        {
            roles = deliverable.Entries,
            asset = deliverable.HasAppointmentRole
                ? new
                {
                    manifests = asset.Scopes, asset.Tags, asset.String1, asset.String2, asset.String3, asset.Number1,
                    asset.Number2, asset.Number3,
                }
                : null
        };
    }

    /// <summary>"," separates entries, encoded "+" (%2B) separates roles in an entry</summary>
    private static string GetRolesString(Deliverable deliverable)
        => string.Join(",", deliverable.Entries.Select(e => string.Join("%2B", e.Select(Uri.EscapeDataString))));
}
