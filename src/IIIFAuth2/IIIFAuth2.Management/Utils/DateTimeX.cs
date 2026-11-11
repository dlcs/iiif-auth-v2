namespace IIIFAuth2.Management.Utils;

public static class DateTimeX
{
    /// <summary>
    /// Convert to UTC, as Npgsql requires for timestamptz. A value without offset is assumed to be UTC
    /// </summary>
    public static DateTime ToUtc(this DateTime value)
        => value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
}
