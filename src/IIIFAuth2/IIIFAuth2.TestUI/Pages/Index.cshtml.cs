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

    /// <summary>Comma separated</summary>
    [BindProperty] public string? Scopes { get; set; }

    /// <summary>Comma separated</summary>
    [BindProperty] public string? Tags { get; set; }

    [BindProperty] public string? String1 { get; set; }
    [BindProperty] public string? String2 { get; set; }
    [BindProperty] public string? String3 { get; set; }
    [BindProperty] public int? Number1 { get; set; }
    [BindProperty] public int? Number2 { get; set; }
    [BindProperty] public int? Number3 { get; set; }

    public RoleLookupResult KnownRoles { get; private set; } = new([], null);

    public IReadOnlyCollection<Deliverable> Deliverables => deliverableStore.All();

    public Uri AuthServiceRoot => settings.Value.AuthServiceRoot;

    public async Task OnGetAsync(string? id, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(id) && deliverableStore.TryGet(id, out var existing))
        {
            DeliverableId = existing.Id;
            SelectedRoles = existing.Roles.ToList();
            var asset = existing.Asset;
            Scopes = asset.Scopes == null ? null : string.Join(", ", asset.Scopes);
            Tags = asset.Tags == null ? null : string.Join(", ", asset.Tags);
            (String1, String2, String3) = (asset.String1, asset.String2, asset.String3);
            (Number1, Number2, Number3) = (asset.Number1, asset.Number2, asset.Number3);
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

        var asset = new DeliverableAsset(SplitList(Scopes), SplitList(Tags), NullIfEmpty(String1),
            NullIfEmpty(String2), NullIfEmpty(String3), Number1, Number2, Number3);
        var deliverable = deliverableStore.Register(DeliverableId, roles, asset);
        return RedirectToPage("Flow", new { id = deliverable.Id });
    }

    private static string[]? SplitList(string? value)
    {
        var values = (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return values.Length == 0 ? null : values;
    }

    // Not trimmed, so whitespace can be tested
    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    public IActionResult OnPostDelete(string id)
    {
        deliverableStore.Remove(id);
        return RedirectToPage();
    }
}
