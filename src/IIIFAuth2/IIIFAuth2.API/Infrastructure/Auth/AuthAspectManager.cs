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
    private delegate void DomainCookieHandler(HttpContext httpContext, string cookieId, string domain);
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ICustomerDomainProvider customerDomainProvider;
    private readonly AuthSettings authSettings;
    private const string CookiePrefix = "id=";
    
    public AuthAspectManager(
        IHttpContextAccessor httpContextAccessor,
        ICustomerDomainProvider customerDomainProvider,
        IOptions<AuthSettings> authSettings)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.customerDomainProvider = customerDomainProvider;
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
        var httpContext = httpContextAccessor.SafeHttpContext();
        var cookieKey = GetAuthCookieKey(authSettings.CookieNameFormat, customer);
        return httpContext.Request.Cookies.TryGetValue(cookieKey, out var cookieValue)
            ? cookieValue
            : null;
    }

    /// <summary>
    /// Add cookie to current Response object, using details from specified <see cref="SessionUser"/>
    /// </summary>
    public Task IssueCookie(SessionUser sessionUser)
        => HandleCookieDomain(sessionUser.Customer,
            (httpContext, cookieId, domain) =>
            {
                var cookieValue = GetCookieValueForId(sessionUser.CookieId);
                httpContext.Response.Cookies.Append(cookieId, cookieValue,
                    new CookieOptions
                    {
                        Domain = domain,
                        Expires = DateTimeOffset.UtcNow.AddSeconds(authSettings.SessionTtl),
                        SameSite = SameSiteMode.None,
                        Secure = true
                    });
            });

    /// <summary>
    /// Remove cookie for customer from current Response object 
    /// </summary>
    public Task RemoveCookieFromResponse(int customerId)
        => HandleCookieDomain(customerId,
            (httpContext, cookieId, domain) =>
                httpContext.Response.Cookies.Delete(cookieId, new CookieOptions { Domain = domain }));

    /// <summary>
    /// Get the Id of provided access-token from Bearer token header
    /// </summary>
    public string? GetAccessToken()
    {
        const string bearerTokenScheme = "bearer";

        var requestHeaders = httpContextAccessor.SafeHttpContext().Request.Headers;
        return AuthenticationHeaderValue.TryParse(requestHeaders.Authorization, out var parsed) &&
               parsed.Scheme.Equals(bearerTokenScheme, StringComparison.InvariantCultureIgnoreCase)
            ? parsed.Parameter
            : null;
    }
    
    private async Task HandleCookieDomain(int customerId, DomainCookieHandler domainCookieHandler)
    {
        var httpContext = httpContextAccessor.SafeHttpContext();
        var domains = await GetCookieDomainList(customerId, httpContext);

        var cookieId = GetAuthCookieKey(authSettings.CookieNameFormat, customerId);

        foreach (var domain in domains)
        {
            domainCookieHandler(httpContext, cookieId, domain);
        }
    }

    private async Task<IEnumerable<string>> GetCookieDomainList(int customerId, HttpContext httpContext)
    {
        var customDomains = await customerDomainProvider.GetCustomerCookieDomains(customerId);
        return customDomains.Union(httpContext.Request.Host.Host.AsList()).Distinct();
    }
}