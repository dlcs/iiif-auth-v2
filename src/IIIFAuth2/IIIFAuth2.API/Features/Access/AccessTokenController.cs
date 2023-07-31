using IIIF;
using IIIF.Auth.V2;
using IIIFAuth2.API.Features.Access.Requests;
using IIIFAuth2.API.Infrastructure.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IIIFAuth2.API.Features.Access;

/// <summary>
/// Controller for IIIF Authorization Flow 2.0 AccessTokenService
/// </summary>
/// <remarks>We don't use [ApiController] here as we always want to return a 200/HTML page with details of any errors</remarks>
public class AccessTokenController : Controller
{
    private readonly IMediator mediator;
    private readonly ILogger<AccessTokenController> logger;

    public AccessTokenController(IMediator mediator, ILogger<AccessTokenController> logger)
    {
        this.mediator = mediator;
        this.logger = logger;
    }
    
    /// <summary>
    /// Access Token Service, client exchanges authorising aspect for Token
    /// as detailed in https://iiif.io/api/auth/2.0/#access-token-service
    /// </summary>
    [HttpGet]
    [Route("access/{customerId}/token")]
    public async Task<IActionResult> AccessTokenService(
        [FromRoute] int customerId,
        [FromQuery] string messageId,
        [FromQuery] Uri? origin,
        CancellationToken cancellationToken)
    {
        
        ViewResult GenerateErrorResult(string profile, string heading, string note)
        {
            var accessTokenError = AuthServiceBuilder.CreateAuthAccessTokenError2(profile, messageId, heading, note);
            return View("AccessTokenResponse", (accessTokenError as JsonLdBase, origin));
        }
        
        try
        {
            if (string.IsNullOrEmpty(messageId))
            {
                return GenerateErrorResult(AuthAccessTokenError2.InvalidRequest, "Invalid Request",
                    "Required messageId query parameter not provided");
            }
            
            if (origin == null)
            {
                return GenerateErrorResult(AuthAccessTokenError2.InvalidRequest, "Invalid Request",
                    "Required origin query parameter not provided or invalid");
            }
            
            var initiate = new HandleAccessTokenRequest(customerId, origin, messageId);
            var accessTokenResponse = await mediator.Send(initiate, cancellationToken);

            return View("AccessTokenResponse", (accessTokenResponse, origin));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error handling AccessTokenRequest");
            return GenerateErrorResult(AuthAccessTokenError2.Unavailable, "Unexpected error",
                "Unexpected error processing request");
        }
    }
}