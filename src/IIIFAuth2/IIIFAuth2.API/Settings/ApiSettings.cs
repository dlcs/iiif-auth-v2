namespace IIIFAuth2.API.Settings;

public class ApiSettings
{
    public string? PathBase { get; set; }

    /// <summary>
    /// The base URI of DLCS Orchestrator
    /// </summary>
    /// <remarks>Used to generate Probe request paths</remarks>
    public Uri OrchestratorRoot { get; set; } = null!;
}