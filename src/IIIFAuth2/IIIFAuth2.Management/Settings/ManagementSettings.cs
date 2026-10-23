namespace IIIFAuth2.Management.Settings;

public class ManagementSettings
{
    public DlcsSettings Dlcs { get; set; } = new();

    /// <summary>
    /// How long, in secs, to cache successful auth checks against DLCS API
    /// </summary>
    public int AuthCacheTtl { get; set; } = 60;
}

public class DlcsSettings
{
    /// <summary>
    /// The base URI of DLCS API, used to validate incoming credentials
    /// </summary>
    public Uri ApiRoot { get; set; } = null!;

    /// <summary>
    /// Default timeout, in ms, for requests to DLCS API
    /// </summary>
    public int ApiDefaultTimeoutMs { get; set; } = 30000;
}
