using System.Diagnostics.CodeAnalysis;
// ReSharper disable PossibleMultipleEnumeration

namespace IIIFAuth2.API.Utils;

public static class GuardX
{
    /// <summary>
    /// Throw <see cref="ArgumentNullException"/> if provided object is null.
    /// </summary>
    /// <param name="argument">Argument to check.</param>
    /// <param name="argName">Name of argument.</param>
    /// <typeparam name="T">Type of argument to check.</typeparam>
    /// <returns>Passed argument, if not null.</returns>
    /// <exception cref="ArgumentNullException">Thrown if provided argument is null.</exception>
    [return: NotNull]
    public static T ThrowIfNull<T>(this T argument, string argName)
    {
        if (argument == null)
        {
            throw new ArgumentNullException(argName);
        }

        return argument;
    }
    
    /// <summary>
    /// Throw <see cref="ArgumentNullException"/> if provided collection is null or empty.
    /// </summary>
    /// <param name="argument">Argument to check.</param>
    /// <param name="argName">Name of argument.</param>
    /// <typeparam name="T">Type of argument to check.</typeparam>
    /// <returns>Passed argument, if not null or empty.</returns>
    /// <exception cref="ArgumentNullException">Thrown if provided argument is null.</exception>
    public static IEnumerable<T> ThrowIfNullOrEmpty<T>(this IEnumerable<T>? argument, string argName)
    {
        if (argument.IsNullOrEmpty())
        {
            throw new ArgumentNullException(argName);
        }

        return argument;
    }
}