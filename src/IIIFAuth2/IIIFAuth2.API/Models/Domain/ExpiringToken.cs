namespace IIIFAuth2.API.Models.Domain;

/// <summary>
/// Helpers for generating and validating tokens 
/// </summary>
public static class ExpiringToken
{
    /// <summary>
    /// Generate a new token, containing random part and a timestamp
    /// </summary>
    /// <param name="timestamp">Optional, current Utc time</param>
    /// <returns>Random string</returns>
    public static string GenerateNewToken(DateTime? timestamp = null)
    {
        if (timestamp.HasValue && timestamp.Value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException($"Timestamp must be Utc kind, {timestamp.Value.Kind} provided",
                nameof(timestamp));
        }
        
        var datePart = BitConverter.GetBytes((timestamp ?? DateTime.UtcNow).ToBinary());
        var randomPart = Guid.NewGuid().ToByteArray();
        var token = Convert.ToBase64String(randomPart.Concat(datePart).ToArray());
        return token;
    }

    /// <summary>
    /// Validate whether provided token has expired.
    /// Note that this only validates the timestamp, not whether this token has been used
    /// </summary>
    /// <param name="token">Token to validate</param>
    /// <param name="validForSecs">How long specified token is valid for (default 300s)</param>
    /// <returns>True if token has not expired, else False</returns>
    public static bool HasExpired(string token, int validForSecs = 300)
    {
        try
        {
            var bytes = Convert.FromBase64String(token);
            // Guid byte array is 16-element array so start at 16 to get dateTime part
            var datePart = DateTime.FromBinary(BitConverter.ToInt64(bytes, 16));
            return datePart < DateTime.UtcNow.AddSeconds(-validForSecs);
        }
        catch (FormatException)
        {
            // If we can't parse it, reject it
            return true;
        }
    }
}