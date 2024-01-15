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
}