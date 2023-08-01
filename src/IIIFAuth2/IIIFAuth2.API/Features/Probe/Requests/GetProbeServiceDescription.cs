using System.Net;
using IIIF.Auth.V2;
using IIIF.Presentation.V3.Strings;
using IIIFAuth2.API.Infrastructure.Auth;
using IIIFAuth2.API.Infrastructure.Auth.Models;
using IIIFAuth2.API.Models.Domain;
using MediatR;

namespace IIIFAuth2.API.Features.Probe.Requests;

/// <summary>
/// Get a probe service response for specified roles
/// </summary>
public class GetProbeServiceDescription : IRequest<AuthProbeResult2>
{
    public AssetId AssetId { get; }
    
    public IReadOnlyCollection<string> Roles { get; }

    public GetProbeServiceDescription(string assetId, string roles)
    {
        AssetId = AssetId.FromString(assetId);
        Roles = roles.Split(",", StringSplitOptions.RemoveEmptyEntries);
    }
}

public class GetServicesDescriptionHandler : IRequestHandler<GetProbeServiceDescription, AuthProbeResult2>
{
    private readonly SessionManagementService sessionManagementService;

    public GetServicesDescriptionHandler(SessionManagementService sessionManagementService)
    {
        this.sessionManagementService = sessionManagementService;
    }

    public async Task<AuthProbeResult2> Handle(GetProbeServiceDescription request, CancellationToken cancellationToken)
    {
        var findSessionResponse =
            await sessionManagementService.TryGetSessionUserForAccessToken(request.AssetId.Customer, cancellationToken);

        var authProbeResult = BuildProbeResultResponse(findSessionResponse, request.Roles);
        return authProbeResult;
    }

    private static AuthProbeResult2 BuildProbeResultResponse(TryGetSessionResponse tryGetSessionResponse, IReadOnlyCollection<string> roles)
    {
        var statusCode = GetStatusCode(tryGetSessionResponse, roles);
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

    private static HttpStatusCode GetStatusCode(TryGetSessionResponse getSessionResponse, IReadOnlyCollection<string> roles)
    {
        if (!getSessionResponse.IsSuccessWithSession()) return HttpStatusCode.Unauthorized;
        
        return getSessionResponse.CanUserAccessAtLeastOneRole(roles) ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
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