using IIIFAuth2.API.Features.Auth.Requests;
using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Utils;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IIIFAuth2.API.Features.Auth;

/// <summary>
/// Controller for IIIF Authorization Flow 2.0 endpoints
/// </summary>
public class AuthController : AuthBaseController
{
    public AuthController(IMediator mediator) : base(mediator)
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
        if (IsSignificantGestureRequired(origin))
        {
            var getSignificantGestureModel = new GetSignificantGestureModel(customerId, accessServiceName);
            var significantGestureModel = await Mediator.Send(getSignificantGestureModel, cancellationToken);

            if (significantGestureModel == null)
            {
                return NotFound($"AccessService {accessServiceName} not found");
            }
            
            return View("SignificantGesture", significantGestureModel);
        }

        throw new NotImplementedException();
    }
    
    /// <summary>
    /// Handle post back for page that is required to users to perform a 'significant gesture' on DLCS domain.
    /// This is required for us to issue a cookie to user. 
    /// </summary>
    [HttpPost]
    [Route("{customerId}/{accessServiceName}")]
    public Task<IActionResult> SignificantGesture(
        [FromRoute] int customerId,
        [FromRoute] string accessServiceName,
        [FromQuery] string origin,
        CancellationToken cancellationToken)
    {
        // StartRoleProviderLogic();
        // Window.Close()
        // return HandleRoleProvision()
        throw new NotImplementedException();
    }

    private Task<IActionResult> HandleRoleProvision(
        [FromRoute] int customerId,
        [FromRoute] string accessServiceName,
        [FromQuery] string origin,
        CancellationToken cancellationToken)
    {
        // StartRoleProviderLogic();
        // Window.Close()
        throw new NotImplementedException();
    }

    private bool IsSignificantGestureRequired(Uri origin)
        => !Request.IsSameOrigin(origin);
}