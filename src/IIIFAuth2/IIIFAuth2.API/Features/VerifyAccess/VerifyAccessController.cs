using IIIFAuth2.API.Features.VerifyAccess.Requests;
using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Models.Api;
using IIIFAuth2.Core.Models.Domain;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IIIFAuth2.API.Features.VerifyAccess;

/// <summary>
/// Controller for downstream/internal-only use by DLCS to validate whether request has access to specified resource.
/// </summary>
[Route("[controller]")]
[ApiController]
public class VerifyAccessController : AuthBaseController
{
    public VerifyAccessController(IMediator mediator, ILogger<VerifyAccessController> logger) : base(mediator, logger)
    {
    }

    /// <summary>
    /// Generate a status-code response by validating cookie associated with request. Used for both Assets
    /// ({customer}/{space}/{asset}) and Adjuncts ({customer}/{space}/{asset}/{adjunctId})
    /// </summary>
    /// <param name="deliverableId">Id of DLCS deliverable to check access for</param>
    /// <param name="roles">Comma delimited list of roles that asset has</param>
    /// <remarks>Retained for compatibility, POST allows asset metadata to be provided</remarks>
    [HttpGet]
    [Route("{**deliverableId}")]
    public Task<IActionResult> VerifyAccess(
        [FromRoute] string deliverableId,
        [FromQuery] string roles,
        CancellationToken cancellationToken)
        => SendTestAccessRequest(() => new TestAccessRequest(deliverableId, roles), cancellationToken);

    /// <summary>
    /// Generate a status-code response by validating cookie associated with request, using roles and optional asset
    /// metadata from the request body. Used for both Assets and Adjuncts.
    /// </summary>
    /// <param name="deliverableId">Id of DLCS deliverable to check access for</param>
    /// <param name="body">Roles that asset has, and optional asset metadata for evaluating appointment roles</param>
    [HttpPost]
    [Route("{**deliverableId}")]
    public Task<IActionResult> VerifyAccessPost(
        [FromRoute] string deliverableId,
        [FromBody] AccessCheckRequest body,
        CancellationToken cancellationToken)
    {
        var roles = body.Roles?.Select(RoleEntry.Parse).Where(e => e.Roles.Count > 0).ToList();
        if (roles is not { Count: > 0 }) return Task.FromResult<IActionResult>(BadRequest("Required roles missing"));

        var asset = body.Asset?.ToAssetMetadata();
        return SendTestAccessRequest(() => new TestAccessRequest(deliverableId, roles, asset), cancellationToken);
    }

    /// <remarks>Request is created in HandleRequest so invalid deliverableIds result in 400</remarks>
    private Task<IActionResult> SendTestAccessRequest(Func<TestAccessRequest> createRequest,
        CancellationToken cancellationToken)
        => HandleRequest(async () =>
        {
            var statusCode = await Mediator.Send(createRequest(), cancellationToken);
            return StatusCode((int)statusCode);
        });
}
