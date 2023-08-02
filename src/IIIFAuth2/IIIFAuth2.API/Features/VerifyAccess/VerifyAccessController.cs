using IIIFAuth2.API.Features.VerifyAccess.Requests;
using IIIFAuth2.API.Infrastructure.Web;
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
    /// Generate a status-code response by validating cookie associated with request  
    /// </summary>
    /// <param name="assetId">Id of DLCS asset to check access for</param>
    /// <param name="roles">Comma delimited list of roles that asset has</param>
    [HttpGet]
    [Route("{**assetId}")]
    public async Task<IActionResult> VerifyAccess(
        [FromRoute] string assetId,
        [FromQuery] string roles,
        CancellationToken cancellationToken)
        => await HandleRequest(async () =>
        {
            var testAccessRequest = new TestAccessRequest(assetId, roles);
            var statusCode = await Mediator.Send(testAccessRequest, cancellationToken);
            return StatusCode((int)statusCode);
        });
}