namespace IIIFAuth2.API.Settings;

public class ApiSettings
{
    public string? PathBase { get; set; }

    /// <summary>
    /// The base URI of DLCS Orchestrator
    /// </summary>
    /// <remarks>Used to generate Probe request paths</remarks>
    public Uri OrchestratorRoot { get; set; } = null!;

    public AuthSettings Auth { get; set; } = new();

    /// <summary>
    /// Fallback title for Significant Gesture view, if none specified by RoleProvider
    /// </summary>
    public string DefaultSignificantGestureTitle = "Click to continue";

    /// <summary>
    /// Fallback message for Significant Gesture view, if none specified by RoleProvider
    /// </summary>
    public string DefaultSignificantGestureMessage = "Confirm authentication with DLCS";
}

public class AuthSettings
{
    /// <summary>
    /// Format of authToken, used to generate token id.
    /// {0} is replaced with customer id
    /// </summary>
    public string CookieNameFormat { get; set; } = "dlcs-auth2-{0}";

    /// <summary>
    /// Default TTL for sessions + cookies in secs
    /// </summary>
    public int SessionTtl { get; set; } = 600;

    /// <summary>
    /// UserSession expiry not refreshed if LastChecked within this number of secs
    /// </summary>
    /// <remarks>This avoids constant churn in db</remarks>
    public int RefreshThreshold { get; set; } = 120;

    /// <summary>
    /// TTL, in secs, for how long to cache jwks
    /// </summary>
    public int JwksTtl { get; set; } = 600;

    /// <summary>
    /// Dictionary that allows control of domain-specific significant gesture paths. Default value is
    /// /access/{customerId}/gesture.
    /// Replacement values: {customerId}
    /// </summary>
    public Dictionary<string, string> GesturePathTemplateForDomain { get; set; } = new();
    
    /// <summary>
    /// Dictionary that allows control of domain-specific oauth callback paths. Default value is
    /// /access/{customerId}/{accessService}/oauth2/callback.
    /// Replacement values: {customerId} and {accessService}
    /// </summary>
    public Dictionary<string, string> OAuthCallbackPathTemplateForDomain { get; set; } = new();
}