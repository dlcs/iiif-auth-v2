using IIIFAuth2.API.Data;
using IIIFAuth2.API.Utils;

namespace IIIFAuth2.API.Infrastructure.Auth;

/// <summary>
/// Service to help dealing with customer domains
/// </summary>
public class CustomerDomainService
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly AuthServicesContext dbContext;
    private readonly ILogger<CustomerDomainService> logger;

    public CustomerDomainService(
        IHttpContextAccessor httpContextAccessor,
        AuthServicesContext dbContext,
        ILogger<CustomerDomainService> logger)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.dbContext = dbContext;
        this.logger = logger;
    }
    
    /// <summary>
    /// Check if the specified Origin is for the current domain or Origin is on a domain that the DLCS can issue cookies
    /// too 
    /// </summary>
    public async Task<bool> OriginForControlledDomain(int customerId, Uri origin)
    {
        try
        {
            var originMatchesHost = OriginMatchesHost(origin);
            if (originMatchesHost) return true;

            return await IsOriginSubdomainOfCookieDomain(customerId, origin);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if Origin is for ControlledDomain");
            return true;
        }
    }

    private async Task<List<string>> GetCustomerCookieDomains(int customerId)
    {
        var customerCookieDomain =
            (await dbContext.CustomerCookieDomains.GetCachedCustomerRecords(customerId, CacheKeys.CookieDomains))
            .SingleOrDefault();

        return customerCookieDomain?.Domains ?? new List<string>(0);
    }
    
    private bool OriginMatchesHost(Uri origin)
    {
        var currentRequest = httpContextAccessor
            .HttpContext.ThrowIfNull(nameof(httpContextAccessor.HttpContext))
            .Request;
        
        var originMatchesHost = currentRequest.IsSameOrigin(origin);

        logger.LogTrace("Test Origin {RequestOrigin} with Host {Scheme}://{Host} result: {OriginMatchesHost}", origin,
            currentRequest.Scheme, currentRequest.Host, originMatchesHost);
        return originMatchesHost;
    }

    private async Task<bool> IsOriginSubdomainOfCookieDomain(int customerId, Uri origin)
    {
        var customerCookieDomains = await GetCustomerCookieDomains(customerId);

        if (customerCookieDomains.IsNullOrEmpty())
        {
            logger.LogTrace("No CustomerCookieDomains found for customer {CustomerId}", customerId);
            return false;
        }

        // Use Host.Host, rather than Host.Value, as we don't check port
        var currentHost = origin.Host;
        foreach (var domain in customerCookieDomains)
        {
            if (currentHost.Contains(domain, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogTrace("'{CookieDomain}' is subdomain of current Host {CurrentHost} for Customer {CustomerId}",
                    domain, currentHost, customerId);
                return true;
            }
        }

        return false;
    }
}