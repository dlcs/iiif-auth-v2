using IIIFAuth2.API.Data;
using IIIFAuth2.API.Utils;

namespace IIIFAuth2.API.Infrastructure.Auth;

public interface ICustomerDomainProvider
{
    /// <summary>
    /// Get a list of any custom domains configured for customer
    /// </summary>
    Task<IReadOnlyCollection<string>> GetCustomerCookieDomains(int customerId);
}

public interface ICustomerDomainChecker
{
    /// <summary>
    /// Check if the specified Origin is for the current domain, or it is on a domain that the DLCS can issue cookies to
    /// </summary>
    Task<bool> OriginForControlledDomain(int customerId, Uri origin);
}

/// <summary>
/// Service to help dealing with customer domains
/// </summary>
public class CustomerDomainService : ICustomerDomainProvider, ICustomerDomainChecker
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
    /// Check if the specified Origin is for the current domain, or it is on a domain that the DLCS can issue cookies to
    /// </summary>
    public async Task<bool> OriginForControlledDomain(int customerId, Uri origin)
    {
        try
        {
            var currentRequest = httpContextAccessor.SafeHttpContext().Request;
            if (OriginMatchesHost(origin, currentRequest)) return true;
            if (OriginSubdomainOfHost(origin, currentRequest)) return true;

            return await IsOriginSubdomainOfCookieDomain(customerId, origin);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if Origin is for ControlledDomain");
            return true;
        }
    }

    /// <summary>
    /// Get a list of any custom domains configured for customer
    /// </summary>
    public async Task<IReadOnlyCollection<string>> GetCustomerCookieDomains(int customerId)
    {
        var customerCookieDomain =
            (await dbContext.CustomerCookieDomains.GetCachedCustomerRecords(customerId, CacheKeys.CookieDomains))
            .SingleOrDefault();

        return customerCookieDomain?.Domains ?? new List<string>(0);
    }
    
    private bool OriginMatchesHost(Uri origin, HttpRequest currentRequest)
    {
        var originMatchesHost = currentRequest.IsSameOrigin(origin);

        logger.LogTrace("Test Origin {RequestOrigin} with Host {Scheme}://{Host} result: {OriginMatchesHost}", origin,
            currentRequest.Scheme, currentRequest.Host, originMatchesHost);
        return originMatchesHost;
    }

    private bool OriginSubdomainOfHost(Uri origin, HttpRequest currentRequest)
    {
        var originHost = origin.Host;
        var currentHost = currentRequest.Host.Host;
        if (originHost.Contains(currentHost, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogTrace("'{OriginHost}' is subdomain of current Host {CurrentHost}", originHost, currentHost);
            return true;
        }

        return false;
    }

    private async Task<bool> IsOriginSubdomainOfCookieDomain(int customerId, Uri origin)
    {
        var customerCookieDomains = await GetCustomerCookieDomains(customerId);

        if (customerCookieDomains.IsNullOrEmpty())
        {
            logger.LogTrace("No CustomerCookieDomains found for customer {CustomerId}", customerId);
            return false;
        }

        var originHost = origin.Host;
        foreach (var domain in customerCookieDomains)
        {
            if (originHost.Contains(domain, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogTrace("'{CookieDomain}' is subdomain of origin {OriginHost} for Customer {CustomerId}",
                    domain, originHost, customerId);
                return true;
            }
        }

        return false;
    }
}
