namespace IIIFAuth2.Core.Models.Domain;

/// <summary>
/// Supported IIIF Auth 2 access service profiles
/// </summary>
/// <remarks>See https://iiif.io/api/auth/2.0/#access-service</remarks>
public static class AccessServiceProfiles
{
    public const string Active = "active";
    public const string Kiosk = "kiosk";
    public const string External = "external";
}
