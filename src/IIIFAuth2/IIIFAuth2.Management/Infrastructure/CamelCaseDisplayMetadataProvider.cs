using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace IIIFAuth2.Management.Infrastructure;

/// <summary>
/// Default property display names to camelCase, so validation messages use the same names as JSON bodies (e.g.
/// "The name field is required.")
/// </summary>
public class CamelCaseDisplayMetadataProvider : IDisplayMetadataProvider
{
    public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
    {
        if (context.Key.MetadataKind != ModelMetadataKind.Property || context.DisplayMetadata.DisplayName != null)
            return;

        var name = JsonNamingPolicy.CamelCase.ConvertName(context.Key.Name!);
        context.DisplayMetadata.DisplayName = () => name;
    }
}
