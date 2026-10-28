using IIIFAuth2.TestUI.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.TestUI.Pages;

public class IndexModel(DeliverableStore deliverableStore, RoleLookup roleLookup, IOptions<TestUISettings> settings)
    : PageModel
{
    [BindProperty] public string DeliverableId { get; set; } = "";

    [BindProperty] public List<string> SelectedRoles { get; set; } = [];

    [BindProperty] public string? AdditionalRoles { get; set; }

    public RoleLookupResult KnownRoles { get; private set; } = new([], null);

    public IReadOnlyCollection<Deliverable> Deliverables => deliverableStore.All();

    public Uri AuthServiceRoot => settings.Value.AuthServiceRoot;

    public async Task OnGetAsync(string? id, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(id) && deliverableStore.TryGet(id, out var existing))
        {
            DeliverableId = existing.Id;
            SelectedRoles = existing.Roles.ToList();
        }

        KnownRoles = await roleLookup.GetRoles(cancellationToken);

        // Show any roles not known to the db in free-text box
        AdditionalRoles = string.Join(Environment.NewLine,
            SelectedRoles.Where(r => KnownRoles.Roles.All(k => k.Id != r)));
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(DeliverableId))
        {
            ModelState.AddModelError(nameof(DeliverableId), "Deliverable Id is required");
            KnownRoles = await roleLookup.GetRoles(cancellationToken);
            return Page();
        }

        var additional = (AdditionalRoles ?? "").Split(['\n', '\r', ','],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var roles = SelectedRoles.Concat(additional).Distinct().ToList();

        var deliverable = deliverableStore.Register(DeliverableId, roles);
        return RedirectToPage("Flow", new { id = deliverable.Id });
    }

    public IActionResult OnPostDelete(string id)
    {
        deliverableStore.Remove(id);
        return RedirectToPage();
    }
}
