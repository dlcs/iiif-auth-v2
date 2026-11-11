namespace IIIFAuth2.Management.Settings;

public class ManagementSettings
{
    public DlcsSettings Dlcs { get; set; } = new();

    /// <summary>
    /// Optional path prefix the app is served under, without leading slash
    /// </summary>
    public string? PathBase { get; set; } = "auth";

    /// <summary>
    /// How long, in secs, to cache successful auth checks against DLCS API
    /// </summary>
    public int AuthCacheTtl { get; set; } = 60;

    /// <summary>
    /// Page size used for paged collections if pageSize isn't specified
    /// </summary>
    public int DefaultPageSize { get; set; } = 50;

    /// <summary>
    /// Maximum page size for paged collections, a larger requested pageSize is capped to this
    /// </summary>
    public int MaxPageSize { get; set; } = 200;
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
