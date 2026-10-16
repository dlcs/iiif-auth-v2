using IIIFAuth2.TestUI.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IIIFAuth2.TestUI.Pages;

public class FlowModel(DeliverableStore deliverableStore, CallLog callLog) : PageModel
{
    public Deliverable? Deliverable { get; private set; }

    /// <summary>
    /// Only downstream calls after this are shown in the flow log
    /// </summary>
    public long LastCallId { get; private set; }

    public void OnGet(string id)
    {
        Deliverable = deliverableStore.TryGet(id, out var deliverable) ? deliverable : null;
        LastCallId = callLog.LatestId;
    }
}
