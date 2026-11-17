using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;

namespace IIIFAuth2.Management.Infrastructure.Hydra;

/// <summary>
/// A collection of Hydra resources, see https://www.hydra-cg.com/spec/latest/core/#collections
/// </summary>
public class HydraCollection<T>() : JsonLdBase("Collection")
{
    [JsonPropertyName("totalItems")]
    public int TotalItems { get; set; }

    [JsonPropertyName("pageSize")]
    public int? PageSize { get; set; }

    [JsonPropertyName("member")]
    public T[] Members { get; set; } = [];

    [JsonPropertyName("view")]
    public PartialCollectionView? View { get; set; }
}

public class PartialCollectionView() : JsonLdBase("PartialCollectionView")
{
    [JsonPropertyName("first")]
    public string? First { get; set; }

    [JsonPropertyName("previous")]
    public string? Previous { get; set; }

    [JsonPropertyName("next")]
    public string? Next { get; set; }

    [JsonPropertyName("last")]
    public string? Last { get; set; }

    // Not part of the Hydra specification, carried over from Protagonist
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    /// <summary>
    /// Add a "View" to collection if there is more than 1 page. Links are based on collection.Id, with
    /// <paramref name="furtherParameters"/> (e.g. search filters) appended to each link
    /// </summary>
    public static void AddPaging<T>(HydraCollection<T> collection, int page, int pageSize,
        IEnumerable<KeyValuePair<string, string?>>? furtherParameters = null)
    {
        if (collection.TotalItems <= collection.Members.Length) return;

        var totalPages = (collection.TotalItems + pageSize - 1) / pageSize;
        var baseUrl = collection.Id!.Split('?')[0];
        var further = furtherParameters?.ToList() ?? [];
        
        collection.View = new PartialCollectionView
        {
            Id = Link(page),
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            First = page > 1 ? Link(1) : null,
            Previous = page > 1 ? Link(page - 1) : null,
            Next = page < totalPages ? Link(page + 1) : null,
            Last = page < totalPages ? Link(totalPages) : null,
        };
        return;
        
        string Link(int p) => QueryHelpers.AddQueryString(baseUrl,
            new KeyValuePair<string, string?>[] { new("page", p.ToString()), new("pageSize", pageSize.ToString()) }
                .Concat(further));
    }
}
