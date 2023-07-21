using IIIFAuth2.API.Models.Domain;

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