using IIIFAuth2.API.Features.Access.Requests;
using IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning;
using IIIFAuth2.API.Infrastructure.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IIIFAuth2.API.Features.Access;

/// <summary>
/// Controller for IIIF Authorization Flow 2.0 endpoints
/// </summary>
[ApiController]
[Route("[controller]")]
public class AccessController : AuthBaseController
{
    public AccessController(IMediator mediator, ILogger<AccessController> logger) : base(mediator, logger)
    {
    }

    /// <summary>
    /// Handle Client interaction with Access Service as detailed in
    /// https://iiif.io/api/auth/2.0/#interaction-with-access-services
    /// </summary>
    [HttpGet]
    [Route("{customerId}/{accessServiceName}")]
    public Task<IActionResult> AccessService(
        [FromRoute] int customerId,
        [FromRoute] string accessServiceName,
        [FromQuery] Uri origin,
        CancellationToken cancellationToken)
    {
        var initiate = new InitiateRoleProvisionRequest(customerId, accessServiceName, origin);
        return RoleProvisionResponseConverter(initiate, accessServiceName, cancellationToken);
    }

    /// <summary>
    /// Handle post back for page that is required to users to perform a 'significant gesture' on DLCS domain.
    /// This is required for us to issue a cookie to user. 
    /// </summary>
    [HttpPost]
    [Route("{customerId}/gesture")]
    public async Task<IActionResult> SignificantGesture(
        [FromForm] string singleUseToken,
        CancellationToken cancellationToken)
    {
        return await HandleRequest(async () =>
        {
            var initiate = new HandleRoleProvisionToken(singleUseToken);
            var errorMessage = await Mediator.Send(initiate, cancellationToken);

            return View("CloseWindow", errorMessage);
        });
    }

    /// <summary>
    /// Callback handler after user has authenticated with 3rd party oauth provider
    /// </summary>
    [HttpGet]
    [Route("{customerId}/{accessServiceName}/oauth2/callback")]
    public Task<IActionResult> Oauth2Callback(
            [FromRoute] int customerId,
            [FromRoute] string accessServiceName,
            [FromQuery] string code,
            [FromQuery(Name = "state")] string roleProvisionToken,
            CancellationToken cancellationToken)
    {
        var oauth2Callback = new HandleOAuth2Callback(customerId, accessServiceName, code, roleProvisionToken);
        return RoleProvisionResponseConverter(oauth2Callback, accessServiceName, cancellationToken);
    }

    /// <summary>
    /// IIIF Authorization logout service 
    /// https://iiif.io/api/auth/2.0/#logout-service
    /// </summary>
    [HttpGet]
    [Route("{customerId}/{accessServiceName}/logout")]
    public async Task<IActionResult> AccessService(
        [FromRoute] int customerId,
        [FromRoute] string accessServiceName,
        CancellationToken cancellationToken)
    {
        return await HandleRequest(async () =>
        {
            var logout = new HandleLogoutRequest(customerId, accessServiceName);
            await Mediator.Send(logout, cancellationToken);

            return NoContent();
        });
    }

    private async Task<IActionResult> RoleProvisionResponseConverter(IRequest<HandleRoleProvisionResponse?> request,
        string accessServiceName, CancellationToken cancellationToken)
    {
        return await HandleRequest(async () =>
        {
            var provisionRoleResponse = await Mediator.Send(request, cancellationToken);

            if (provisionRoleResponse == null)
            {
                return NotFound($"AccessService {accessServiceName} not found");
            }

            if (provisionRoleResponse.RequiresRedirect)
            {
                return Redirect(provisionRoleResponse.RedirectUri!.ToString());
            }

            if (provisionRoleResponse.SignificantGestureRequired)
            {
                return View("SignificantGesture", provisionRoleResponse.SignificantGestureModel);
            }

            if (provisionRoleResponse.RoleProvisionHandled)
            {
                return View("CloseWindow");
            }

            return StatusCode(500, "Unexpected error encountered");
        });
    }
}