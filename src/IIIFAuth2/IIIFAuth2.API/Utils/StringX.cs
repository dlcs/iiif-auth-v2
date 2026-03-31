namespace IIIFAuth2.API.Utils;

public static class StringX
{
    /// <summary>
    /// Ensure that strings ends with provided value, adding it if not.
    /// </summary>
    /// <param name="str">String to check</param>
    /// <param name="endsWith">Value to ensure string ends with</param>
    /// <returns>Provided string if it already ends with value, or provided string with value appended</returns>
    public static string EnsureEndsWith(this string str, string endsWith)
        => str.EndsWith(endsWith) ? str : $"{str}{endsWith}";


    /// <summary>
    /// Splits string containing separated values into IEnumerable{T}, using specified separator.
    /// </summary>
    /// <param name="str">String to split</param>
    /// <param name="separator">String to split by.</param>
    /// <returns>String split, or empty list.</returns>
    public static IEnumerable<string> SplitSeparatedString(this string? str, string separator)
        => str?.Trim().Split(separator, StringSplitOptions.RemoveEmptyEntries) ?? Enumerable.Empty<string>();

}