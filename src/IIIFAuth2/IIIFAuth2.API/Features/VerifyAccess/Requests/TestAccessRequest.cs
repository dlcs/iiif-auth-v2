using System.Net;
using IIIFAuth2.API.Infrastructure.Auth;
using IIIFAuth2.API.Models.Domain;
using MediatR;

namespace IIIFAuth2.API.Features.VerifyAccess.Requests;

/// <summary>
/// Get an HttpStatusCode value indicating whether the current request has access to specified roles
/// </summary>
public class TestAccessRequest : IRequest<HttpStatusCode>
{
    public DeliverableId DeliverableId { get; }

    public IReadOnlyCollection<string> Roles { get; }

    public TestAccessRequest(string deliverableId, string roles)
    {
        DeliverableId = DeliverableId.FromString(deliverableId);
        Roles = roles.Split(",", StringSplitOptions.RemoveEmptyEntries);
    }
}

public class GetAccessTestResultHandler : IRequestHandler<TestAccessRequest, HttpStatusCode>
{
    private readonly SessionManagementService sessionManagementService;
    private readonly IpRoleEvaluator ipRoleEvaluator;

    public GetAccessTestResultHandler(SessionManagementService sessionManagementService,
        IpRoleEvaluator ipRoleEvaluator)
    {
        this.sessionManagementService = sessionManagementService;
        this.ipRoleEvaluator = ipRoleEvaluator;
    }

    public async Task<HttpStatusCode> Handle(TestAccessRequest request, CancellationToken cancellationToken)
    {
        // IP roles are evaluated first as they don't require a session
        var ipRoleEvaluation = await ipRoleEvaluator.Evaluate(request.DeliverableId.Customer, request.Roles);
        var ipStatusCode = AccessStatusCodeHelpers.GetIpStatusCode(ipRoleEvaluation, request.Roles);
        if (ipStatusCode.HasValue) return ipStatusCode.Value;

        // Next try cookie based
        var tryGetSessionResponse =
            await sessionManagementService.TryGetSessionUserForCookie(request.DeliverableId.Customer, null,
                cancellationToken);

        var statusCode = AccessStatusCodeHelpers.GetStatusCode(tryGetSessionResponse, request.Roles);
        return statusCode;
    }
}