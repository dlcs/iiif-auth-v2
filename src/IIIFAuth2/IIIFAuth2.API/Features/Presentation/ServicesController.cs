using Microsoft.AspNetCore.Mvc;

namespace IIIFAuth2.API.Features.Presentation;

/// <summary>
/// Controller for IIIF presentation resources
/// </summary>
[ApiController]
[Route("[controller]")]
public class ServicesController : Controller
{
    [HttpGet]
    [Route("{assetId}")]
    public async Task<IActionResult> GetServicesDescription(
        [FromRoute] string assetId,
        [FromQuery] string roles)
    {
        return Ok();
    }
}