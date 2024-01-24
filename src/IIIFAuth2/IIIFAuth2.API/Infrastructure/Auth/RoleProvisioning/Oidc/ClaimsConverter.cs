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
            var claim = claimsPrincipal.Claims.SingleOrDefault(c => c.Type == oidcConfiguration.ClaimType);
            if (claim == null)
            {
                logger.LogInformation("ClaimsPrincipal {PrincipalId} does not have required claim '{ClaimType}'",
                    claimsPrincipal.Identity, oidcConfiguration.ClaimType);
                return ResultStatus<IReadOnlyCollection<string>>.Unsuccessful();
            }

            ResultStatus<IReadOnlyCollection<string>> LogAndReturn(params string[] roles)
            {
                logger.LogDebug(
                    "ClaimsPrincipal {PrincipalId} has claim '{ClaimType}':'{ClaimValue}'. Given role(s) {@DlcsRoles}",
                    claimsPrincipal.Identity, claim.Type, claim.Value, roles);

                return ResultStatus<IReadOnlyCollection<string>>.Successful(roles);
            }

            // Claim found - attempt to map it to a DLCS role
            var claimMappings = oidcConfiguration.Mapping ?? new Dictionary<string, string[]>();
            if (claimMappings.TryGetValue(claim.Value, out var mappedRoles))
            {
                return LogAndReturn(mappedRoles);
            }

            logger.LogDebug(
                "ClaimsPrincipal {PrincipalId} has claim '{ClaimType}':'{ClaimValue}' which cannot be mapped. Using fallback behaviour {FallbackBehaviour}",
                claimsPrincipal.Identity, claim.Type, claim.Value, oidcConfiguration.UnknownValueBehaviour);
            if (oidcConfiguration.UnknownValueBehaviour == UnknownMappingValueBehaviour.UseClaim)
            {
                return LogAndReturn(claim.Value);
            }
            else if (oidcConfiguration.UnknownValueBehaviour == UnknownMappingValueBehaviour.Fallback)
            {
                return LogAndReturn(oidcConfiguration.FallbackMapping ?? Array.Empty<string>());
            }
            else
            {
                logger.LogWarning(
                    "ClaimsPrincipal {PrincipalId} has claim '{ClaimType}':'{ClaimValue}' that cannot be mapped. Throwing exception.",
                    claimsPrincipal.Identity, claim.Type, claim.Value);
                return ResultStatus<IReadOnlyCollection<string>>.Unsuccessful();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unexpected error converting claims to DLCS role for ClaimsPrincipal {PrincipalId}",
                claimsPrincipal.Identity);
            return ResultStatus<IReadOnlyCollection<string>>.Unsuccessful();;
        }
    }
}
