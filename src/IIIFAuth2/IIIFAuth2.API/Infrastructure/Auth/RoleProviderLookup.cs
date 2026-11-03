using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Utils;
using IIIFAuth2.Core.Data;
using IIIFAuth2.Core.Models.Domain;

namespace IIIFAuth2.API.Infrastructure.Auth;

internal static class RoleProviderLookup
{
    /// <summary>
    /// Get a lookup of role-id:provider configuration, for requested roles whose access-service's role-provider has
    /// a configuration of type <typeparamref name="T"/>. The provider type is authoritative, a mismatch with the role
    /// prefix (checked by <paramref name="hasRolePrefix"/>) is logged only.
    /// </summary>
    public static async Task<Dictionary<string, T>> GetRoleConfigurations<T>(
        AuthServicesContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        ILogger logger,
        int customerId,
        IReadOnlyCollection<string> roles,
        Func<string, bool> hasRolePrefix)
        where T : class, IProviderConfiguration
    {
        var lookup = new Dictionary<string, T>();
        if (roles.Count == 0) return lookup;

        var customerRoles = await dbContext.Roles.GetCachedCustomerRecords(customerId, CacheKeys.Roles);
        var requestedRoles = customerRoles.Where(r => roles.Contains(r.Id)).ToList();
        if (requestedRoles.Count == 0) return lookup;

        var accessServices =
            (await dbContext.AccessServices.GetCachedCustomerRecords(customerId, CacheKeys.AccessService))
            .ToDictionary(a => a.Id);
        // Called internally by Orchestrator, so this is the host Orchestrator calls (unless it forwards
        // x-forwarded-host) rather than the public host. In practice these providers use "default" config
        var host = httpContextAccessor.SafeHttpContext().Request.Host.Value ?? string.Empty;

        foreach (var role in requestedRoles)
        {
            var configuration = accessServices.TryGetValue(role.AccessServiceId, out var accessService)
                ? accessService.RoleProvider?.Configuration.GetConfigurationOrDefault(host)
                : null;

            var isProvider = configuration is T;
            var hasPrefix = hasRolePrefix(role.Id);
            if (isProvider != hasPrefix)
            {
                // Validation of this will be enforced on write in future. Debug as this is called per request
                logger.LogDebug(
                    "Role {Role} for customer {CustomerId} mismatch - has prefix: {HasPrefix}, has {ProviderType} provider: {IsProvider}",
                    role.Id, customerId, hasPrefix, typeof(T).Name, isProvider);
            }

            if (configuration is T typed) lookup[role.Id] = typed;
        }

        return lookup;
    }
}
