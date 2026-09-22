using System.Net;
using IIIF.Auth.V2;
using IIIF.Presentation.V3.Strings;
using IIIFAuth2.API.Features.Probe.Requests;
using IIIFAuth2.API.Infrastructure.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IIIFAuth2.API.Features.Probe;

/// <summary>
/// Controller for downstream/internal-only probe-service. This is used by other services to generate
/// probe-service-response objects w/ status-code.
/// </summary>
/// <remarks>We don't use [ApiController] here as we always want to return a ProbeServiceResponse</remarks>
public class ProbeController : AuthBaseController
{
    public ProbeController(IMediator mediator, ILogger<ProbeController> logger) : base(mediator, logger) 
    {
    }

    /// <summary>
    /// Generate a IIIF Probe Service Response by validating Bearer token
    /// </summary>
    /// <param name="assetId">Id of DLCS asset to get probe service result for</param>
    /// <param name="roles">Comma delimited list of roles that asset has</param>
    [HttpGet]
    [Route("probe_internal/{**assetId}")]
    public Task<IActionResult> ProbeService(
        [FromRoute] string assetId,
        [FromQuery] string roles,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Handling probe service request for {AssetId}", assetId);
        return HandleProbeRequest(assetId, roles, cancellationToken);
    }

    /// <summary>
    /// Generate a IIIF Probe Service Response for an Adjunct by validating Bearer token. Adjuncts inherit
    /// the roles of their parent Asset, so access is validated using the same customer/roles logic.
    /// </summary>
    /// <param name="customer">Id of DLCS customer that owns the parent asset</param>
    /// <param name="space">Id of DLCS space that owns the parent asset</param>
    /// <param name="asset">Id of DLCS asset the adjunct belongs to</param>
    /// <param name="adjunctId">Id of the adjunct to get probe service result for</param>
    /// <param name="roles">Comma delimited list of roles that adjunct's parent asset has</param>
    [HttpGet]
    [Route("probe_internal/{customer}/{space}/{asset}/adjuncts/{adjunctId}")]
    public Task<IActionResult> AdjunctProbeService(
        [FromRoute] string customer,
        [FromRoute] string space,
        [FromRoute] string asset,
        [FromRoute] string adjunctId,
        [FromQuery] string roles,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Handling probe service request for adjunct {AdjunctId} on asset {Customer}/{Space}/{Asset}",
            adjunctId, customer, space, asset);
        return HandleProbeRequest($"{customer}/{space}/{asset}", roles, cancellationToken);
    }

    private async Task<IActionResult> HandleProbeRequest(string assetId, string roles,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(roles))
            {
                return GenerateErrorResult(HttpStatusCode.BadRequest, "Required roles query parameter missing");
            }

            var probeServiceRequest = new GetProbeServiceDescription(assetId, roles);
            var probeServiceResult = await Mediator.Send(probeServiceRequest, cancellationToken);
            return IIIFContent(probeServiceResult);
        }
        catch (FormatException fmtEx)
        {
            Logger.LogDebug(fmtEx, "Format exception processing probe service request");
            return GenerateErrorResult(HttpStatusCode.BadRequest, "Provided AssetId is invalid format");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error handling probe service request");
            return GenerateErrorResult(HttpStatusCode.InternalServerError, "Unexpected error");
        }
    }

    private ContentResult GenerateErrorResult(HttpStatusCode statusCode, string note)
    {
        var heading = statusCode == HttpStatusCode.BadRequest ? "Bad Request" : "Unexpected Error";
        var probeResult = new AuthProbeResult2
        { 
            Status = (int)statusCode,
            Heading = new LanguageMap("en", heading),
            Note = new LanguageMap("en", note),
        };
        
        return IIIFContent(probeResult);
    }
}