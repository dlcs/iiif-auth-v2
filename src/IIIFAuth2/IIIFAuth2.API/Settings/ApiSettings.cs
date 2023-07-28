namespace IIIFAuth2.API.Settings;

public class ApiSettings
{
    public string? PathBase { get; set; }

    /// <summary>
    /// The base URI of DLCS Orchestrator
    /// </summary>
    /// <remarks>Used to generate Probe request paths</remarks>
    public Uri OrchestratorRoot { get; set; } = null!;

    /// <summary>
    /// Fallback title for Significant Gesture view, if none specified by RoleProvider
    /// </summary>
    public string DefaultSignificantGestureTitle = "Click to continue";

    /// <summary>
    /// Fallback message for Significant Gesture view, if none specified by RoleProvider
    /// </summary>
    public string DefaultSignificantGestureMessage = "You will now be redirected to DLCS to login";
}

public class AuthSettings
{
    /// <summary>
    /// Format of authToken, used to generate token id.
    /// {0} is replaced with customer id
    /// </summary>
    public string CookieNameFormat { get; set; } = "dlcs-auth2-{0}";

    /// <summary>
    /// Default TTL for sessions + cookies
    /// </summary>
    public int SessionTtl { get; set; } = 600;

    /// <summary>
    /// A list of domains to set on auth cookie.
    /// </summary>
    public List<string> CookieDomains { get; set; } = new();

    /// <summary>
    /// If true the current domain is automatically added to auth token domains.
    /// </summary>
    public bool UseCurrentDomainForCookie { get; set; } = true;
}