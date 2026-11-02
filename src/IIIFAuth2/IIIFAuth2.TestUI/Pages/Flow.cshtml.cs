using IIIFAuth2.TestUI.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IIIFAuth2.TestUI.Pages;

public class FlowModel(DeliverableStore deliverableStore, CallLog callLog, SimulatedClientIp simulatedClientIp)
    : PageModel
{
    public Deliverable? Deliverable { get; private set; }

    /// <summary>
    /// Client IP sent to iiif-auth-v2 as x-forwarded-for when deliverable has IP roles. Null = request IP
    /// </summary>
    public string? SimulatedClientIp => simulatedClientIp.Value;

    public string? RequestClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>
    /// Only downstream calls after this are shown in the flow log
    /// </summary>
    public long LastCallId { get; private set; }

    public void OnGet(string id)
    {
        Deliverable = deliverableStore.TryGet(id, out var deliverable) ? deliverable : null;
        LastCallId = callLog.LatestId;
    }

    public IActionResult OnPostClientIp(string id, string? clientIp)
    {
        // Not validated, allowing invalid values to be sent to test handling
        simulatedClientIp.Value = clientIp;
        return RedirectToPage("Flow", new { id });
    }
}
