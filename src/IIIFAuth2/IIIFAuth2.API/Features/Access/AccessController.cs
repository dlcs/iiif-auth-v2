using IIIFAuth2.API.Features.Auth.Requests;
using IIIFAuth2.API.Infrastructure.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace IIIFAuth2.API.Features.Access;

/// <summary>
/// Controller for IIIF Authorization Flow 2.0 endpoints
/// </summary>
[ApiController]
[Route("[controller]")]
public class AccessController : AuthBaseController
{
    public AccessController(IMediator mediator) : base(mediator)
    {
    }

    /// <summary>
    /// Handle Client interaction with Access Service as detailed in
    /// https://iiif.io/api/auth/2.0/#interaction-with-access-services
    /// </summary>
    [HttpGet]
    [Route("{customerId}/{accessServiceName}")]
    public async Task<IActionResult> AccessService(
        [FromRoute] int customerId,
        [FromRoute] string accessServiceName,
        [FromQuery] Uri origin,
        CancellationToken cancellationToken)
    {
        var initiate = new InitiateRoleProvisionRequest(customerId, accessServiceName, origin);
        var provisionRoleResponse = await Mediator.Send(initiate, cancellationToken);

        if (provisionRoleResponse == null)
        {
            return NotFound($"AccessService {accessServiceName} not found");
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
    }

    /// <summary>
    /// Handle post back for page that is required to users to perform a 'significant gesture' on DLCS domain.
    /// This is required for us to issue a cookie to user. 
    /// </summary>
    [HttpPost]
    [Route("gesture")]
    public Task<IActionResult> SignificantGesture(
        [FromForm] string singleUseToken,
        CancellationToken cancellationToken)
    {
        // StartRoleProviderLogic();
        // Window.Close()
        // return HandleRoleProvision()
        throw new NotImplementedException();
    }
}