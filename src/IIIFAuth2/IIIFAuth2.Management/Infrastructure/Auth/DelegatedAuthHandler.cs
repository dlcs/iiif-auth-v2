using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.Management.Infrastructure.Auth;

/// <summary>
/// AuthenticationHandler that hands off calls to implementation of <see cref="IAuthenticator"/> for auth logic
/// </summary>
public class DelegatedAuthHandler(
    IAuthenticator delegatedAuthenticator,
    IOptionsMonitor<DelegatedAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<DelegatedAuthenticationOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var result = await delegatedAuthenticator.ValidateRequest(Request, Context.RequestAborted);
        return result switch
        {
            AuthResult.NoCredentials => AuthenticateResult.NoResult(),
            AuthResult.Failed => AuthenticateResult.Fail("Invalid credentials"),
            AuthResult.Success => AuthenticateResult.Success(GetAuthenticatedTicket()),
            _ => AuthenticateResult.Fail("Unknown auth result")
        };
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = $"Basic realm=\"{Options.Realm}\"";
        return base.HandleChallengeAsync(properties);
    }

    private AuthenticationTicket GetAuthenticatedTicket()
    {
        var identity = new ClaimsIdentity(Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return ticket;
    }
}

/// <summary>
/// Options for use with <see cref="DelegatedAuthHandler"/>
/// </summary>
public class DelegatedAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// Get or set the Realm for use in auth challenges.
    /// </summary>
    public string Realm { get; set; } = null!;
}

/// <summary>
/// Contains constants for use with basic auth.
/// </summary>
public static class BasicAuthenticationDefaults
{
    public const string AuthenticationScheme = "Basic";
}
