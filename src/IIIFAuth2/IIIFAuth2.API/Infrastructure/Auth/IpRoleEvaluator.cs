using IIIFAuth2.API.Infrastructure.Auth.Models;
using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.Core.Data;
using IIIFAuth2.Core.Models.Domain;

namespace IIIFAuth2.API.Infrastructure.Auth;

/// <summary>
/// Evaluates requested roles that are granted by IpRestriction role-providers against the current client IP address
/// </summary>
public class IpRoleEvaluator(
    AuthServicesContext dbContext,
    IClientIpProvider clientIpProvider,
    IHttpContextAccessor httpContextAccessor,
    ILogger<IpRoleEvaluator> logger)
{
    /// <summary>
    /// Determine which of the specified roles are IP roles, and which of those are granted to the client IP address
    /// </summary>
    /// <remarks>
    /// A role is an IP role if its access-service's role-provider is an IpRestriction provider. The "ip:" prefix of
    /// the role is not used to determine this
    /// </remarks>
    public async Task<IpRoleEvaluation> Evaluate(int customerId, IReadOnlyCollection<string> roles)
    {
        var ipRoleConfigurations = await RoleProviderLookup.GetRoleConfigurations<IpRestrictionConfiguration>(
            dbContext, httpContextAccessor, logger, customerId, roles, RoleX.IsIpRole);

        if (ipRoleConfigurations.Count == 0) return IpRoleEvaluation.Empty;

        return GetIpRoleEvaluation(customerId, ipRoleConfigurations);
    }

    private IpRoleEvaluation GetIpRoleEvaluation(int customerId,
        Dictionary<string, IpRestrictionConfiguration> ipRoleConfigurations)
    {
        var clientIp = clientIpProvider.GetClientIp();
        var grantedRoles = ipRoleConfigurations
            .Where(kvp => kvp.Value.Contains(clientIp))
            .Select(kvp => kvp.Key)
            .ToHashSet();
        // If any provider has no location phrase then none are returned, listing only some locations would be
        // incomplete/misleading so generic wording is used instead
        var phrases = ipRoleConfigurations.Values.Select(c => c.LocationPhrase).ToList();
        var locationPhrases = phrases.Any(string.IsNullOrWhiteSpace)
            ? []
            : phrases.OfType<string>().Distinct().ToList();

        logger.LogDebug(
            "Client IP {ClientIp} granted {GrantedCount} of {IpRoleCount} IP roles for customer {CustomerId}",
            clientIp, grantedRoles.Count, ipRoleConfigurations.Count, customerId);
        return new IpRoleEvaluation(ipRoleConfigurations.Keys.ToHashSet(), grantedRoles, locationPhrases);
    }
}
