namespace IIIFAuth2.TestUI.Infrastructure;

public class TestUISettings
{
    /// <summary>
    /// Base URI of iiif-auth-v2, including any PathBase. Equivalent of Orchestrator's Auth__Auth2ServiceRoot
    /// </summary>
    public Uri AuthServiceRoot { get; set; } = new("https://localhost:7049/auth/v2/");
}
