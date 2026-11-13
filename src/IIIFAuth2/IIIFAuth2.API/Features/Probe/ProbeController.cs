using System.Net;
using IIIF.Auth.V2;
using IIIF.Presentation.V3.Strings;
using IIIFAuth2.API.Features.Probe.Requests;
using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Models.Api;
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
    /// Generate a IIIF Probe Service Response by validating Bearer token. Used for both Assets
    /// ({customer}/{space}/{asset}) and Adjuncts ({customer}/{space}/{asset}/{adjunctId})
    /// </summary>
    /// <param name="deliverableId">Id of DLCS deliverable to get probe service result for</param>
    /// <param name="roles">Comma delimited list of roles that asset has</param>
    /// <remarks>Retained for compatibility, POST allows asset metadata to be provided</remarks>
    [HttpGet]
    [Route("probe_internal/{**deliverableId}")]
    public Task<IActionResult> ProbeService(
        [FromRoute] string deliverableId,
        [FromQuery] string roles,
        CancellationToken cancellationToken)
        => HandleProbeRequest(() => string.IsNullOrEmpty(roles)
                ? null
                : new GetProbeServiceDescription(deliverableId, roles),
            "Required roles query parameter missing", cancellationToken);

    /// <summary>
    /// Generate a IIIF Probe Service Response by validating Bearer token, using roles and optional asset metadata
    /// from the request body. Used for both Assets and Adjuncts.
    /// </summary>
    /// <param name="deliverableId">Id of DLCS deliverable to get probe service result for</param>
    /// <param name="body">Roles that asset has, and optional asset metadata for evaluating appointment roles</param>
    [HttpPost]
    [Route("probe_internal/{**deliverableId}")]
    public Task<IActionResult> ProbeServicePost(
        [FromRoute] string deliverableId,
        [FromBody] AccessCheckRequest? body,
        CancellationToken cancellationToken)
        => HandleProbeRequest(() =>
            {
                var roles = body?.Roles?.Where(r => !string.IsNullOrWhiteSpace(r)).ToArray();
                return roles is not { Length: > 0 }
                    ? null
                    : new GetProbeServiceDescription(deliverableId, roles,
                        body!.Asset?.ToAssetMetadata());
            },
            "Required roles missing from request body, or body invalid", cancellationToken);

    private async Task<IActionResult> HandleProbeRequest(Func<GetProbeServiceDescription?> createRequest,
        string invalidRequestNote, CancellationToken cancellationToken)
    {
        try
        {
            var probeServiceRequest = createRequest();
            if (probeServiceRequest == null)
            {
                return GenerateErrorResult(HttpStatusCode.BadRequest, invalidRequestNote);
            }

            var probeServiceResult = await Mediator.Send(probeServiceRequest, cancellationToken);
            return IIIFContent(probeServiceResult);
        }
        catch (FormatException fmtEx)
        {
            Logger.LogDebug(fmtEx, "Format exception processing probe service request");
            return GenerateErrorResult(HttpStatusCode.BadRequest, "Provided DeliverableId is invalid format");
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
