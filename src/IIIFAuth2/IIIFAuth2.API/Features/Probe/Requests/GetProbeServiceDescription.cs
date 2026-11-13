using IIIF.Auth.V2;
using IIIF.Presentation.V3.Strings;
using IIIFAuth2.API.Infrastructure.Auth;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.Core.Models.Domain;
using MediatR;

namespace IIIFAuth2.API.Features.Probe.Requests;

/// <summary>
/// Get a probe service response for specified roles
/// </summary>
public class GetProbeServiceDescription : IRequest<AuthProbeResult2>
{
    public DeliverableId DeliverableId { get; }

    /// <summary>
    /// Role entries, any one of which grants access if all of its roles are met
    /// </summary>
    public IReadOnlyList<RoleEntry> Roles { get; }

    /// <summary>
    /// Optional asset metadata, used to evaluate appointment asset patterns
    /// </summary>
    public AssetMetadata? Asset { get; }

    public GetProbeServiceDescription(string deliverableId, string roles)
        : this(deliverableId, RoleEntry.ParseList(roles), null)
    {
    }

    public GetProbeServiceDescription(string deliverableId, IReadOnlyList<RoleEntry> roles, AssetMetadata? asset)
    {
        DeliverableId = DeliverableId.FromString(deliverableId);
        Roles = roles;
        Asset = asset;
    }
}

public class GetServicesDescriptionHandler : IRequestHandler<GetProbeServiceDescription, AuthProbeResult2>
{
    private readonly SessionManagementService sessionManagementService;
    private readonly AccessEvaluator accessEvaluator;

    public GetServicesDescriptionHandler(SessionManagementService sessionManagementService,
        AccessEvaluator accessEvaluator)
    {
        this.sessionManagementService = sessionManagementService;
        this.accessEvaluator = accessEvaluator;
    }

    public async Task<AuthProbeResult2> Handle(GetProbeServiceDescription request, CancellationToken cancellationToken)
    {
        var decision = await accessEvaluator.Evaluate(request.DeliverableId, request.Roles, request.Asset,
            ct => sessionManagementService.TryGetSessionUserForAccessToken(request.DeliverableId.Customer, ct),
            cancellationToken);

        var probeResult = new AuthProbeResult2 { Status = (int)decision.StatusCode };
        if (decision.Heading != null) probeResult.Heading = new LanguageMap("en", decision.Heading);
        if (decision.Note != null) probeResult.Note = new LanguageMap("en", decision.Note);
        return probeResult;
    }
}
