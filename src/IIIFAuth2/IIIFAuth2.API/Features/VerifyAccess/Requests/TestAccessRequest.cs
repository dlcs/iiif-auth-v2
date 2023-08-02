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
    public AssetId AssetId { get; }
    
    public IReadOnlyCollection<string> Roles { get; }

    public TestAccessRequest(string assetId, string roles)
    {
        AssetId = AssetId.FromString(assetId);
        Roles = roles.Split(",", StringSplitOptions.RemoveEmptyEntries);
    }
}

public class GetAccessTestResultHandler : IRequestHandler<TestAccessRequest, HttpStatusCode>
{
    private readonly SessionManagementService sessionManagementService;

    public GetAccessTestResultHandler(SessionManagementService sessionManagementService)
    {
        this.sessionManagementService = sessionManagementService;
    }

    public async Task<HttpStatusCode> Handle(TestAccessRequest request, CancellationToken cancellationToken)
    {
        var tryGetSessionResponse =
            await sessionManagementService.TryGetSessionUserForCookie(request.AssetId.Customer, null,
                cancellationToken);

        var statusCode = AccessStatusCodeHelpers.GetStatusCode(tryGetSessionResponse, request.Roles);
        return statusCode;
    }
}