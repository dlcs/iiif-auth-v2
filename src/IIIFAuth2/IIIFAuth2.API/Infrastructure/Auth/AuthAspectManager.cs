using System.Net.Http.Headers;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Settings;
using IIIFAuth2.API.Utils;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.API.Infrastructure.Auth;

/// <summary>
/// A collection of helper utils for dealing with auth cookies.
/// </summary>
/// <remarks>This is based on the original implementation for iiif auth 1.0 in Protagonist</remarks>
public class AuthAspectManager
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly AuthSettings authSettings;
    private const string CookiePrefix = "id=";
    
    public AuthAspectManager(
        IHttpContextAccessor httpContextAccessor,
        IOptions<AuthSettings> authSettings
        )
    {
        this.httpContextAccessor = httpContextAccessor;
        this.authSettings = authSettings.Value;
    }
    
    /// <summary>
    /// Get the Id of auth cookie for customer
    /// </summary>
    public string GetAuthCookieKey(string cookieNameFormat, int customer)
        => string.Format(cookieNameFormat, customer);

    /// <summary>
    /// Get the cookieValue from CookieId
    /// </summary>
    public string GetCookieValueForId(string cookieId)
        => $"{CookiePrefix}{cookieId}";
    
    /// <summary>
    /// Get the CookieId from cookieValue
    /// </summary>
    public string? GetCookieIdFromValue(string cookieValue)
        => cookieValue.StartsWith(CookiePrefix) ? cookieValue[3..] : null;
    
    /// <summary>
    /// Get Cookie for specified customer
    /// </summary>
    public string? GetCookieValueForCustomer(int customer)
    {
        var httpContext = GetContext();
        var cookieKey = GetAuthCookieKey(authSettings.CookieNameFormat, customer);
        return httpContext.Request.Cookies.TryGetValue(cookieKey, out var cookieValue)
            ? cookieValue
            : null;
    }
    
    /// <summary>
    /// Add cookie to current Response object, using details from specified <see cref="SessionUser"/>
    /// </summary>
    public void IssueCookie(SessionUser sessionUser)
    {
        var httpContext = GetContext();
        var domains = GetCookieDomainList(httpContext);

        var cookieValue = GetCookieValueForId(sessionUser.CookieId);
        var cookieId = GetAuthCookieKey(authSettings.CookieNameFormat, sessionUser.Customer);

        foreach (var domain in domains)
        {
            httpContext.Response.Cookies.Append(cookieId, cookieValue,
                new CookieOptions
                {
                    Domain = domain,
                    Expires = DateTimeOffset.UtcNow.AddSeconds(authSettings.SessionTtl),
                    SameSite = SameSiteMode.None,
                    Secure = true
                });
        }
    }

    /// <summary>
    /// Get the Id of provided access-token from Bearer token header
    /// </summary>
    public string? GetAccessToken()
    {
        const string bearerTokenScheme = "bearer";

        var requestHeaders = GetContext().Request.Headers;
        return AuthenticationHeaderValue.TryParse(requestHeaders.Authorization, out var parsed) &&
               parsed.Scheme.Equals(bearerTokenScheme, StringComparison.InvariantCultureIgnoreCase)
            ? parsed.Parameter
            : null;
    }

    private HttpContext GetContext() =>
        httpContextAccessor.HttpContext.ThrowIfNull(nameof(httpContextAccessor.HttpContext));

    private IEnumerable<string> GetCookieDomainList(HttpContext httpContext)
    {
        var domains = authSettings.CookieDomains;
        return authSettings.UseCurrentDomainForCookie
            ? domains.Union(httpContext.Request.Host.Host.AsList())
            : domains;
    }
}