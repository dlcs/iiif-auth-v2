using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using IIIFAuth2.TestUI.Infrastructure;

namespace IIIFAuth2.TestUI.FakeOrchestrator;

/// <summary>
/// Endpoints that simulate Orchestrator's handling of access-controlled resources, making the same downstream calls to
/// iiif-auth-v2 that Orchestrator would.
/// </summary>
public static class FakeOrchestratorEndpoints
{
    public const string CorsPolicy = "FakeOrchestrator";
    private const string AuthContext = "http://iiif.io/api/auth/2/context.json";
    private const string Auth2CookieNameFormat = "dlcs-auth2-{0}";

    public static void MapFakeOrchestrator(this WebApplication app)
    {
        // Same path as Orchestrator, so iiif-auth-v2 "OrchestratorRoot" can be set to this app
        app.MapGet("/auth/v2/probe/{**deliverableId}", ProbeService).RequireCors(CorsPolicy);
        app.MapGet("/fake-image/{**deliverableId}", ContentResource);
        app.MapGet("/manifest/{**deliverableId}", Manifest).RequireCors(CorsPolicy);
    }

    /// <summary>
    /// Simulates Orchestrator ProbeService - see ProbeServiceSupport.HandleProbeRequest in Orchestrator.
    /// This includes proposed IpRestriction changes: the deliverable is looked up before checking for a bearer token,
    /// as probe_internal is called without a token if there are any IP roles. Client IP is sent as x-forwarded-for
    /// if there are any IP roles
    /// </summary>
    private static async Task<IResult> ProbeService(string deliverableId, HttpRequest request,
        DeliverableStore deliverableStore, AuthServiceClient authServiceClient, SimulatedClientIp simulatedClientIp,
        ILogger<Deliverable> logger, CancellationToken cancellationToken)
    {
        if (!deliverableStore.TryGet(deliverableId, out var deliverable))
        {
            logger.LogInformation("Probe for unknown {DeliverableId}", deliverableId);
            return Results.NotFound();
        }

        if (deliverable.Roles.Count == 0)
        {
            logger.LogInformation("Probe for {DeliverableId} with no roles", deliverableId);
            return ProbeResult(200);
        }

        var accessToken = GetBearerToken(request);
        if (string.IsNullOrWhiteSpace(accessToken) && !deliverable.HasIpRole)
        {
            logger.LogInformation("Probe for {DeliverableId} has no bearer token", deliverableId);
            return ProbeResult(401, "Missing credentials", "Authorising credentials not found");
        }

        try
        {
            using var response = await authServiceClient.ProbeInternal(deliverable, accessToken,
                GetForwardedFor(deliverable, request, simulatedClientIp), cancellationToken);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return Results.Text(body, "application/json");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting probe_internal for {DeliverableId}", deliverableId);
            return ProbeResult(500, "Unexpected Error", "Unexpected Error");
        }
    }

    /// <summary>
    /// Simulates Orchestrator handling a request for an access-controlled image/file, using cookie to verify access.
    /// Returns an SVG image summarising result.
    /// As Orchestrator AssetAccessValidator, verifyaccess is only called if there is an auth2 cookie for customer or,
    /// per proposed IpRestriction changes, there are any IP roles. Client IP is sent as x-forwarded-for if there are
    /// any IP roles
    /// </summary>
    private static async Task<IResult> ContentResource(string deliverableId, HttpRequest request,
        DeliverableStore deliverableStore, AuthServiceClient authServiceClient, SimulatedClientIp simulatedClientIp,
        ILogger<Deliverable> logger, CancellationToken cancellationToken)
    {
        if (!deliverableStore.TryGet(deliverableId, out var deliverable))
        {
            return Svg(HttpStatusCode.NotFound, deliverableId, "Not registered in Test UI");
        }

        if (deliverable.Roles.Count == 0)
        {
            return Svg(HttpStatusCode.OK, deliverableId, "Open - no roles");
        }

        if (!HasAuth2Cookie(deliverable, request) && !deliverable.HasIpRole)
        {
            return Svg(HttpStatusCode.Unauthorized, deliverableId, "Access denied (no auth2 cookie, no IP roles)");
        }

        try
        {
            using var response = await authServiceClient.VerifyAccess(deliverable, request.Headers.Cookie.ToString(),
                GetForwardedFor(deliverable, request, simulatedClientIp), cancellationToken);
            return response.IsSuccessStatusCode
                ? Svg(HttpStatusCode.OK, deliverableId, $"Access granted (verifyaccess {(int)response.StatusCode})")
                : Svg(HttpStatusCode.Unauthorized, deliverableId,
                    $"Access denied (verifyaccess {(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error verifying access for {DeliverableId}", deliverableId);
            return Svg(HttpStatusCode.Unauthorized, deliverableId, "Access denied (verifyaccess error)");
        }
    }

    /// <summary>
    /// Generate a single-canvas IIIF Presentation 3 manifest for deliverable, with auth services from iiif-auth-v2
    /// "services" endpoint (as Orchestrator does)
    /// </summary>
    private static async Task<IResult> Manifest(string deliverableId, HttpRequest request,
        DeliverableStore deliverableStore, AuthServiceClient authServiceClient, ILogger<Deliverable> logger,
        CancellationToken cancellationToken)
    {
        if (!deliverableStore.TryGet(deliverableId, out var deliverable)) return Results.NotFound();

        var baseUrl = $"{request.Scheme}://{request.Host}";
        var manifestId = $"{baseUrl}/manifest/{deliverable.Id}";
        var canvasId = $"{manifestId}/canvas/0";
        var contentResource = new JsonObject
        {
            ["id"] = $"{baseUrl}/fake-image/{deliverable.Id}",
            ["type"] = "Image",
            ["format"] = "image/svg+xml",
            ["width"] = 800,
            ["height"] = 400,
        };

        var probeService = deliverable.Roles.Count == 0
            ? null
            : await GetProbeService(deliverable, authServiceClient, logger, cancellationToken);
        if (probeService != null) contentResource["service"] = new JsonArray(probeService);

        var manifest = new JsonObject
        {
            ["@context"] = new JsonArray(AuthContext, "http://iiif.io/api/presentation/3/context.json"),
            ["id"] = manifestId,
            ["type"] = "Manifest",
            ["label"] = LanguageMap($"Test manifest for {deliverable.Id}"),
            ["items"] = new JsonArray(new JsonObject
            {
                ["id"] = canvasId,
                ["type"] = "Canvas",
                ["width"] = 800,
                ["height"] = 400,
                ["items"] = new JsonArray(new JsonObject
                {
                    ["id"] = $"{canvasId}/page",
                    ["type"] = "AnnotationPage",
                    ["items"] = new JsonArray(new JsonObject
                    {
                        ["id"] = $"{canvasId}/page/painting",
                        ["type"] = "Annotation",
                        ["motivation"] = "painting",
                        ["target"] = canvasId,
                        ["body"] = contentResource,
                    }),
                }),
            }),
        };

        return Results.Text(manifest.ToJsonString(), "application/json");
    }

    private static async Task<JsonNode?> GetProbeService(Deliverable deliverable,
        AuthServiceClient authServiceClient, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            // As Orchestrator, a failure here results in no services being rendered
            using var response = await authServiceClient.GetServices(deliverable, cancellationToken);
            response.EnsureSuccessStatusCode();
            var probeService = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            probeService?.AsObject().Remove("@context");
            return probeService;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting services for {DeliverableId}", deliverable.Id);
            return null;
        }
    }

    private static string? GetForwardedFor(Deliverable deliverable, HttpRequest request,
        SimulatedClientIp simulatedClientIp)
        => deliverable.HasIpRole ? simulatedClientIp.GetClientIp(request) : null;

    private static bool HasAuth2Cookie(Deliverable deliverable, HttpRequest request)
        // Unparseable customer - pass through to iiif-auth-v2 to test handling
        => deliverable.Customer is not { } customer ||
           request.Cookies.ContainsKey(string.Format(Auth2CookieNameFormat, customer));

    private static string? GetBearerToken(HttpRequest request)
        => AuthenticationHeaderValue.TryParse(request.Headers.Authorization, out var parsed) &&
           parsed.Scheme.Equals("bearer", StringComparison.OrdinalIgnoreCase)
            ? parsed.Parameter
            : null;

    private static IResult ProbeResult(int status, string? heading = null, string? note = null)
    {
        var probeResult = new JsonObject
        {
            ["@context"] = AuthContext,
            ["type"] = "AuthProbeResult2",
            ["status"] = status,
        };
        if (heading != null) probeResult["heading"] = LanguageMap(heading);
        if (note != null) probeResult["note"] = LanguageMap(note);

        // Probe service always returns 200, status of resource is in body
        return Results.Text(probeResult.ToJsonString(), "application/json");
    }

    private static JsonObject LanguageMap(string value) => new() { ["en"] = new JsonArray(value) };

    private static IResult Svg(HttpStatusCode statusCode, string deliverableId, string message)
    {
        var colour = statusCode == HttpStatusCode.OK ? "#1b7f3b" : "#b42318";
        var svg = $"""
                   <svg xmlns="http://www.w3.org/2000/svg" width="800" height="400" viewBox="0 0 800 400">
                     <rect width="800" height="400" fill="{colour}"/>
                     <text x="400" y="170" font-family="sans-serif" font-size="56" fill="#fff" text-anchor="middle">{(int)statusCode} {statusCode}</text>
                     <text x="400" y="240" font-family="sans-serif" font-size="28" fill="#fff" text-anchor="middle">{WebUtility.HtmlEncode(deliverableId)}</text>
                     <text x="400" y="290" font-family="sans-serif" font-size="24" fill="#fff" text-anchor="middle">{WebUtility.HtmlEncode(message)}</text>
                   </svg>
                   """;
        return Results.Text(svg, "image/svg+xml", statusCode: (int)statusCode);
    }
}
