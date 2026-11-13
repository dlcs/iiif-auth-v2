using System.Net;
using IIIFAuth2.API.Infrastructure.Auth;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.Core.Models.Domain;
using MediatR;

namespace IIIFAuth2.API.Features.VerifyAccess.Requests;

/// <summary>
/// Get an HttpStatusCode value indicating whether the current request has access to specified roles
/// </summary>
public class TestAccessRequest : IRequest<HttpStatusCode>
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

    public TestAccessRequest(string deliverableId, IReadOnlyList<RoleEntry> roles, AssetMetadata? asset)
    {
        DeliverableId = DeliverableId.FromString(deliverableId);
        Roles = roles;
        Asset = asset;
    }
}

public class GetAccessTestResultHandler : IRequestHandler<TestAccessRequest, HttpStatusCode>
{
    private readonly SessionManagementService sessionManagementService;
    private readonly AccessEvaluator accessEvaluator;

    public GetAccessTestResultHandler(SessionManagementService sessionManagementService,
        AccessEvaluator accessEvaluator)
    {
        this.sessionManagementService = sessionManagementService;
        this.accessEvaluator = accessEvaluator;
    }

    public async Task<HttpStatusCode> Handle(TestAccessRequest request, CancellationToken cancellationToken)
    {
        var decision = await accessEvaluator.Evaluate(request.DeliverableId, request.Roles, request.Asset,
            ct => sessionManagementService.TryGetSessionUserForCookie(request.DeliverableId.Customer, null, ct),
            cancellationToken);
        return decision.StatusCode;
    }
}
