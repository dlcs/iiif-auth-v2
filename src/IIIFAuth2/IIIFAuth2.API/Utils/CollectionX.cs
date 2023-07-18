using System.Diagnostics.CodeAnalysis;

namespace IIIFAuth2.API.Utils;

public static class CollectionX
{
    /// <summary>
    /// Check if IList is null or empty
    /// </summary>
    /// <returns>true if null or empty, else false</returns>
    public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this ICollection<T>? collection)
        => collection == null || collection.Count == 0;
}