#nullable disable
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace IIIFAuth2.API.Data.Converters;

/// <summary>
/// Conversion logic for string[] (on model) -> string (in db)
/// </summary>
public class StringArrayConverter : ValueConverter<string[], string>
{
    public StringArrayConverter()
        :base(v => string.Join(",", v),
            v => v.Split(",", StringSplitOptions.RemoveEmptyEntries).ToArray())
    {
    }
}

/// <summary>
/// Comparison logic for LanguageMap values. Used by EF internals for determining when a field has changed 
/// </summary>
public class StringArrayComparer : ValueComparer<string[]>
{
    public StringArrayComparer()
        : base(
            (c1, c2) => c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToArray()
        )
    {
    }
}