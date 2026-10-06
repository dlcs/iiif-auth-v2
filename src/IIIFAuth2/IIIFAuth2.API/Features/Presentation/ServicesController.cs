using IIIFAuth2.API.Features.Presentation.Requests;
using IIIFAuth2.API.Infrastructure.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IIIFAuth2.API.Features.Presentation;

/// <summary>
/// Controller for IIIF presentation resources
/// </summary>
[ApiController]
[Route("[controller]")]
public class ServicesController : AuthBaseController
{
    public ServicesController(IMediator mediator, ILogger<ServicesController> logger) : base(mediator, logger)
    {
    }
    
    /// <summary>
    /// Generate a IIIF Services Description for auth services for given DeliverableId and Role.
    /// No check is done to validate that the specified resource has the given role - this is an outside concern
    /// </summary>
    /// <param name="deliverableId">Id of deliverable to generate service description for</param>
    /// <param name="roles">Comma delimited list of roles that asset has</param>
    /// <returns>IIIF Service Description for specified deliverable</returns>
    [HttpGet]
    [Route("{**deliverableId}")]
    public Task<IActionResult> GetServicesDescription(
        [FromRoute] string deliverableId,
        [FromQuery] string roles,
        CancellationToken cancellationToken)
    {
        return HandleRequest(() => new GetServicesDescription(deliverableId, roles),
            errorTitle: "Error getting IIIF services",
            cancellationToken: cancellationToken);
    }
}
