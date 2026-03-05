using System.Security.Claims;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Models.Result;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Oidc;

public class ClaimsConverter
{
    private readonly ILogger<ClaimsConverter> logger;

    public ClaimsConverter(ILogger<ClaimsConverter> logger)
    {
        this.logger = logger;
    }

    public ResultStatus<IReadOnlyCollection<string>> GetDlcsRolesFromClaims(ClaimsPrincipal claimsPrincipal,
        OidcConfiguration oidcConfiguration)
    {
        try
        {
            var claimTypesToCheck = GetClaimTypes(oidcConfiguration.ClaimType)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var claims = claimsPrincipal.Claims
                .Where(c => claimTypesToCheck.Contains(c.Type))
                .ToArray();

            if (claims.Length == 0)
            {
                logger.LogInformation("ClaimsPrincipal {PrincipalId} does not have required claim '{ClaimType}'",
                    claimsPrincipal.Identity, oidcConfiguration.ClaimType);
                return ResultStatus<IReadOnlyCollection<string>>.Unsuccessful();
            }


            var claimMappings = oidcConfiguration.Mapping ?? new Dictionary<string, string[]>();
            var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unmappedClaims = new List<Claim>();

            foreach (var claim in claims)
            {
                if (claimMappings.TryGetValue(claim.Value, out var mappedRoles))
                {
                    roles.UnionWith(mappedRoles);
                    continue;
                }

                unmappedClaims.Add(claim);
            }

            if (roles.Count is 0) 
            {
                switch (oidcConfiguration.UnknownValueBehaviour)
                {
                    case UnknownMappingValueBehaviour.UseClaim:
                        roles.UnionWith(unmappedClaims.Select(c => c.Value));
                        break;
                    case UnknownMappingValueBehaviour.Fallback:
                        roles.UnionWith(oidcConfiguration.FallbackMapping ?? Array.Empty<string>());
                        break;
                    case UnknownMappingValueBehaviour.Unknown:
                        return ResultStatus<IReadOnlyCollection<string>>.Unsuccessful();
                        //breaks
                    case UnknownMappingValueBehaviour.Throw:
                        throw new InvalidOperationException(
                            $"ClaimsPrincipal {claimsPrincipal.Identity} has claim(s) for '{oidcConfiguration.ClaimType}' that cannot be mapped to DLCS roles, and UnknownValueBehaviour is set to '{oidcConfiguration.UnknownValueBehaviour}'.");
                        //breaks
                    default:
                        foreach (var claim in unmappedClaims)
                        {
                            logger.LogWarning(
                                "ClaimsPrincipal {PrincipalId} has claim '{ClaimType}':'{ClaimValue}' that cannot be mapped. Throwing exception.",
                                claimsPrincipal.Identity, claim.Type, claim.Value);
                        }
                        break;
                }
            }

            var distinctRoles = roles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            return distinctRoles.Length > 0
                ? ResultStatus<IReadOnlyCollection<string>>.Successful(distinctRoles)
                : ResultStatus<IReadOnlyCollection<string>>.Unsuccessful();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unexpected error converting claims to DLCS role for ClaimsPrincipal {PrincipalId}",
                claimsPrincipal.Identity);
            return ResultStatus<IReadOnlyCollection<string>>.Unsuccessful();
        }
    }

    private static IEnumerable<string> GetClaimTypes(string configuredClaimType)
    {
        yield return configuredClaimType;

        // Common Entra role claim handling: with MapInboundClaims=false the claim arrives as "roles"
        if (configuredClaimType.Equals(ClaimTypes.Role, StringComparison.OrdinalIgnoreCase) ||
            configuredClaimType.EndsWith("/claims/role", StringComparison.OrdinalIgnoreCase))
        {
            yield return "roles";
        }

        if (configuredClaimType.Equals("roles", StringComparison.OrdinalIgnoreCase))
        {
            yield return ClaimTypes.Role;
        }
    }
}
