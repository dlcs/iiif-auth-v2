using IIIFAuth2.API.Infrastructure.Auth.Models;
using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Utils;
using IIIFAuth2.Core.Data;
using IIIFAuth2.Core.Data.Entities;
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
        if (roles.Count == 0) return IpRoleEvaluation.Empty;

        var customerRoles = await dbContext.Roles.GetCachedCustomerRecords(customerId, CacheKeys.Roles);
        var requestedRoles = customerRoles.Where(r => roles.Contains(r.Id)).ToList();
        if (requestedRoles.Count == 0) return IpRoleEvaluation.Empty;

        var ipRoleConfigurations = await GetIpRestrictionLookup(customerId, requestedRoles);

        if (ipRoleConfigurations.Count == 0) return IpRoleEvaluation.Empty;

        return GetIpRoleEvaluation(customerId, ipRoleConfigurations);
    }

    /// <summary>
    /// Get a lookup of role-id:role-provider configuration
    /// </summary>
    private async Task<Dictionary<string, IpRestrictionConfiguration>> GetIpRestrictionLookup(int customerId,
        List<Role> requestedRoles)
    {
        var accessServices =
            (await dbContext.AccessServices.GetCachedCustomerRecords(customerId, CacheKeys.AccessService))
            .ToDictionary(a => a.Id);
        var host = httpContextAccessor.SafeHttpContext().Request.Host.Value ?? string.Empty;

        var ipRoleConfigurations = new Dictionary<string, IpRestrictionConfiguration>();
        foreach (var role in requestedRoles)
        {
            var configuration = accessServices.TryGetValue(role.AccessServiceId, out var accessService)
                ? accessService.RoleProvider?.Configuration.GetConfigurationOrDefault(host)
                : null;

            var isIpProvider = configuration is IpRestrictionConfiguration;
            var hasIpPrefix = role.Id.IsIpRole();
            if (isIpProvider != hasIpPrefix)
            {
                // Validation of this will be enforced on write in future
                logger.LogWarning(
                    "Role {Role} for customer {CustomerId} mismatch - has ip prefix: {HasIpPrefix}, has IpRestriction provider: {IsIpProvider}",
                    role.Id, customerId, hasIpPrefix, isIpProvider);
            }

            if (configuration is IpRestrictionConfiguration ipRestrictionConfiguration)
            {
                ipRoleConfigurations[role.Id] = ipRestrictionConfiguration;
            }
        }

        return ipRoleConfigurations;
    }

    private IpRoleEvaluation GetIpRoleEvaluation(int customerId,
        Dictionary<string, IpRestrictionConfiguration> ipRoleConfigurations)
    {
        var clientIp = clientIpProvider.GetClientIp();
        var grantedRoles = ipRoleConfigurations
            .Where(kvp => kvp.Value.Contains(clientIp))
            .Select(kvp => kvp.Key)
            .ToHashSet();
        var placeNames = ipRoleConfigurations.Values
            .Select(c => c.PlaceName)
            .OfType<string>()
            .Distinct()
            .ToList();

        logger.LogDebug(
            "Client IP {ClientIp} granted {GrantedCount} of {IpRoleCount} IP roles for customer {CustomerId}",
            clientIp, grantedRoles.Count, ipRoleConfigurations.Count, customerId);
        return new IpRoleEvaluation(ipRoleConfigurations.Keys.ToHashSet(), grantedRoles, placeNames);
    }
}
