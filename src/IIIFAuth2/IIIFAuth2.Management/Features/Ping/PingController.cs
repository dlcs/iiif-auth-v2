using Microsoft.AspNetCore.Mvc;

namespace IIIFAuth2.Management.Features.Ping;

/// <summary>
/// Minimal authenticated endpoint, verifies that provided credentials are valid for customer
/// </summary>
[ApiController]
[Route("customers/{customerId:int}/ping")]
public class PingController : ControllerBase
{
    [HttpGet]
    public IActionResult Ping(int customerId) => Ok(new { customerId });
}
