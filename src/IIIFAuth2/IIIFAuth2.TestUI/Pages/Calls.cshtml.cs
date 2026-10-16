using IIIFAuth2.TestUI.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IIIFAuth2.TestUI.Pages;

public class CallsModel(CallLog callLog) : PageModel
{
    public IReadOnlyList<CallLogEntry> Entries { get; private set; } = [];

    public void OnGet() => Entries = callLog.GetAfter(0).Reverse().ToList();

    public IActionResult OnPostClear()
    {
        callLog.Clear();
        return RedirectToPage();
    }
}
