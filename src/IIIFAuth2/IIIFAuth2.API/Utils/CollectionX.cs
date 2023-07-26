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
    
    /// <summary>
    /// Return a List{T} containing single item.
    /// </summary>
    /// <param name="item">Item to add to list</param>
    /// <typeparam name="T">Type of item</typeparam>
    /// <returns>List of one item</returns>
    public static List<T> AsList<T>(this T item)
        => new() { item };
}