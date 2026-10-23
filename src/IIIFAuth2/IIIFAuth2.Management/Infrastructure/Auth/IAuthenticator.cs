namespace IIIFAuth2.Management.Infrastructure.Auth;

/// <summary>
/// Validate that provided request contains valid auth credentials
/// </summary>
public interface IAuthenticator
{
    Task<AuthResult> ValidateRequest(HttpRequest request, CancellationToken cancellationToken = default);
}

public enum AuthResult
{
    Success,
    Failed,
    NoCredentials,
}
