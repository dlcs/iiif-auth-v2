using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Settings;
using IIIFAuth2.API.Utils;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.API.Infrastructure.Auth;

/// <summary>
/// A collection of helper utils for dealing with auth cookies.
/// </summary>
/// <remarks>This is based on the original implementation for iiif auth 1.0 in Protagonist</remarks>
public class AuthCookieManager
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly AuthSettings authSettings;
    private const string CookiePrefix = "id=";
    
    public AuthCookieManager(
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
    /// Add cookie to current Response object, using details from specified <see cref="SessionUser"/>
    /// </summary>
    public void IssueCookie(SessionUser sessionUser)
    {
        var httpContext = httpContextAccessor.HttpContext.ThrowIfNull(nameof(httpContextAccessor.HttpContext));
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

    private IEnumerable<string> GetCookieDomainList(HttpContext httpContext)
    {
        var domains = authSettings.CookieDomains;
        return authSettings.UseCurrentDomainForCookie
            ? domains.Union(httpContext.Request.Host.Host.AsList())
            : domains;
    }
}