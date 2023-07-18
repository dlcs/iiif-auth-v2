using IIIF.Serialisation;
using IIIFAuth2.API.Features.Presentation.Requests;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IIIFAuth2.API.Features.Presentation;

/// <summary>
/// Controller for IIIF presentation resources
/// </summary>
[ApiController]
[Route("[controller]")]
public class ServicesController : Controller
{
    private readonly IMediator mediator;

    public ServicesController(IMediator mediator)
    {
        this.mediator = mediator;
    }
    
    /// <summary>
    /// Generate a IIIF Services Description for auth services for given AssetId and Role.
    /// No check is done to validate that the specified resource has the given role - this is an outside concern
    /// </summary>
    /// <param name="assetId">Id of DLCS asset to generate service description for</param>
    /// <param name="roles">Comma delimited list of roles that asset has</param>
    /// <returns>IIIF Service Description for specified asset</returns>
    [HttpGet]
    [Route("{**assetId}")]
    public async Task<IActionResult> GetServicesDescription(
        [FromRoute] string assetId,
        [FromQuery] string roles,
        CancellationToken cancellationToken)
    {
        var request = new GetServicesDescription(assetId, roles);
        var response = await mediator.Send(request, cancellationToken);
        return Ok(response.DescriptionResource!.AsJson());
    }
}
