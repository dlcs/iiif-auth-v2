﻿#nullable disable
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace IIIFAuth2.API.Data.Converters;

/// <summary>
/// Conversion logic for string[] (on model) -> string (in db)
/// </summary>
public class StringListConverter : ValueConverter<List<string>, string>
{
    public StringListConverter()
        :base(v => string.Join(",", v),
            v => v.Split(",", StringSplitOptions.RemoveEmptyEntries).ToList())
    {
    }
}

/// <summary>
/// Comparison logic for LanguageMap values. Used by EF internals for determining when a field has changed 
/// </summary>
public class StringListComparer : ValueComparer<List<string>>
{
    public StringListComparer()
        : base(
            (c1, c2) => c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList()
        )
    {
    }
}