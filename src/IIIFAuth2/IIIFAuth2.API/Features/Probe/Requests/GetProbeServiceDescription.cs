using System.Net;
using IIIF.Auth.V2;
using IIIF.Presentation.V3.Strings;
using IIIFAuth2.API.Infrastructure.Auth;
using IIIFAuth2.API.Infrastructure.Auth.Models;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Settings;
using MediatR;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.API.Features.Probe.Requests;

/// <summary>
/// Get a probe service response for specified roles
/// </summary>
public class GetProbeServiceDescription : IRequest<AuthProbeResult2>
{
    public DeliverableId DeliverableId { get; }

    public IReadOnlyCollection<string> Roles { get; }

    public GetProbeServiceDescription(string deliverableId, string roles)
    {
        DeliverableId = DeliverableId.FromString(deliverableId);
        Roles = roles.Split(",", StringSplitOptions.RemoveEmptyEntries);
    }
}

public class GetServicesDescriptionHandler : IRequestHandler<GetProbeServiceDescription, AuthProbeResult2>
{
    private readonly SessionManagementService sessionManagementService;
    private readonly IpRoleEvaluator ipRoleEvaluator;
    private readonly ApiSettings apiSettings;

    public GetServicesDescriptionHandler(SessionManagementService sessionManagementService,
        IpRoleEvaluator ipRoleEvaluator, IOptions<ApiSettings> apiOptions)
    {
        this.sessionManagementService = sessionManagementService;
        this.ipRoleEvaluator = ipRoleEvaluator;
        apiSettings = apiOptions.Value;
    }

    public async Task<AuthProbeResult2> Handle(GetProbeServiceDescription request, CancellationToken cancellationToken)
    {
        // IP roles are evaluated first as they don't require a session
        var ipRoleEvaluation = await ipRoleEvaluator.Evaluate(request.DeliverableId.Customer, request.Roles);
        var ipStatusCode = AccessStatusCodeHelpers.GetIpStatusCode(ipRoleEvaluation, request.Roles);
        if (ipStatusCode.HasValue) return BuildIpProbeResultResponse(ipStatusCode.Value, ipRoleEvaluation);

        var findSessionResponse =
            await sessionManagementService.TryGetSessionUserForAccessToken(request.DeliverableId.Customer, cancellationToken);

        var authProbeResult = BuildProbeResultResponse(findSessionResponse, request.Roles);
        return authProbeResult;
    }

    private static AuthProbeResult2 BuildProbeResultResponse(TryGetSessionResponse tryGetSessionResponse, IReadOnlyCollection<string> roles)
    {
        var statusCode = AccessStatusCodeHelpers.GetStatusCode(tryGetSessionResponse, roles);
        var probeService = new AuthProbeResult2
        { 
            Status = (int)statusCode, 
        };

        if (statusCode == HttpStatusCode.OK) return probeService;
        
        var (heading, note) = GetProperties(tryGetSessionResponse.Status, statusCode);
        if (heading != null) probeService.Heading = new LanguageMap("en", heading);
        if (note != null) probeService.Note = new LanguageMap("en", note);

        return probeService;
    }

    private AuthProbeResult2 BuildIpProbeResultResponse(HttpStatusCode statusCode,
        IpRoleEvaluation ipRoleEvaluation)
    {
        var probeService = new AuthProbeResult2 { Status = (int)statusCode };
        if (statusCode == HttpStatusCode.OK) return probeService;

        var note = apiSettings.UseGenericLocationWording || ipRoleEvaluation.LocationPhrases.Count == 0
            ? "Only available from permitted locations"
            : $"Only available {string.Join(" or ", ipRoleEvaluation.LocationPhrases)}";
        probeService.Heading = new LanguageMap("en", "Not available at your location");
        probeService.Note = new LanguageMap("en", note);
        return probeService;
    }

    private static (string? Heading, string? Note) GetProperties(GetSessionStatus getSessionStatus, HttpStatusCode statusCode)
    {
        if (statusCode == HttpStatusCode.Forbidden) return ("Forbidden", "Session does not have required roles");

        return getSessionStatus switch
        {
            GetSessionStatus.MissingSession => ("Invalid credentials", "Authorising credentials invalid"),
            GetSessionStatus.MissingCredentials => ("Missing credentials", "Authorising credentials not found"),
            GetSessionStatus.ExpiredSession => ("Expired session", "Session has expired"),
            _ => ("Unknown Error", "Unable to fulfil request")
        };
    }
}