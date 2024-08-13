using System.Text.Json;
using Amazon.SecretsManager.Extensions.Caching;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Utils;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Oidc;

/// <summary>
/// Handles RoleProvisioning for OAuth2/OIDC configurations
/// </summary>
public class OidcRoleProviderHandler
{
    private readonly IAuth0Client auth0Client;
    private readonly SessionManagementService sessionManagementService;
    private readonly RoleProvisionGranter roleProvisionGranter;
    private readonly ISecretsManagerCache secretsManagerCache;
    private readonly ILogger<OidcRoleProviderHandler> logger;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public OidcRoleProviderHandler(
        IAuth0Client auth0Client, 
        SessionManagementService sessionManagementService,
        RoleProvisionGranter roleProvisionGranter,
        ISecretsManagerCache secretsManagerCache,
        ILogger<OidcRoleProviderHandler> logger)
    {
        this.auth0Client = auth0Client;
        this.sessionManagementService = sessionManagementService;
        this.roleProvisionGranter = roleProvisionGranter;
        this.secretsManagerCache = secretsManagerCache;
        this.logger = logger;
    }

    /// <summary>
    /// Generate redirect link to send user to idp
    /// </summary>
    public async Task<HandleRoleProvisionResponse> InitiateLoginRequest(int customerId, Uri requestOrigin,
        AccessService accessService, IProviderConfiguration providerConfiguration,
        CancellationToken cancellationToken = default)
    {
        var configuration = providerConfiguration.SafelyGetTypedConfig<OidcConfiguration>();
        ValidateProviderIsSupported(configuration);

        var roleProvisionTokenId = await sessionManagementService.CreateRoleProvisionToken(customerId,
            Array.Empty<string>(), requestOrigin.ToString(), cancellationToken);
        
        var loginUrl = auth0Client.GetAuthLoginUrl(configuration, accessService, roleProvisionTokenId);
        return HandleRoleProvisionResponse.Redirect(loginUrl);
    }

    /// <summary>
    /// Handle callback from idp, validate roleProvisionToken, exchange authCode for jwt + calculate DLCS roles
    /// </summary>
    public async Task<HandleRoleProvisionResponse> HandleLoginCallback(int customerId, string roleProvisionToken,
        string authCode, AccessService accessService, IProviderConfiguration providerConfiguration,
        CancellationToken cancellationToken = default)
    {
        var configuration = providerConfiguration.SafelyGetTypedConfig<OidcConfiguration>();
        ValidateProviderIsSupported(configuration);
        await EnsureSecrets(configuration, accessService.Id);

        // Validate the roleProviderToken is valid and hasn't been used
        var validateTokenResult =
            await sessionManagementService.TryGetValidToken(roleProvisionToken, cancellationToken);

        if (!validateTokenResult.Success)
        {
            logger.LogInformation("Received nonce token {Token} in oidc response that is invalid",
                roleProvisionToken);
            return HandleRoleProvisionResponse.Error("Auth code invalid");
        }

        var requestUri = new Uri(validateTokenResult.Value!.Origin);

        // Get DLCS roles from authcode
        var roles = await auth0Client.GetDlcsRolesForCode(configuration, accessService, authCode, cancellationToken);

        if (roles.IsNullOrEmpty()) return HandleRoleProvisionResponse.Error("Unable to get DLCS roles for user");

        return await roleProvisionGranter.CompleteRequest(customerId, requestUri, providerConfiguration,
            () => Task.FromResult(roles), cancellationToken);
    }

    private static void ValidateProviderIsSupported(OidcConfiguration configuration)
    {
        if (!configuration.Provider.Equals(OidcConfiguration.SupportedProviders.Auth0,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only Auth0 is supported as oidc provider");
        }
    }

    private async Task EnsureSecrets(OidcConfiguration configuration, Guid accessServiceId)
    {
        // Note that this isn't an arn, it's just a prefix on the secretsmanager name
        const string secretsManagerPrefix = "secretsmanager:";
        if (configuration.ClientSecret.StartsWith(secretsManagerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var secretName = configuration.ClientSecret[secretsManagerPrefix.Length..];
                logger.LogDebug("Fetching secretsmanager secret {SecretName} for {AccessService}", secretName,
                    accessServiceId);
                var secretsJson = await secretsManagerCache.GetSecretString(secretName);
                var secret = JsonSerializer.Deserialize<Secrets>(secretsJson, Options);
                configuration.ClientSecret = secret?.ClientSecret ?? string.Empty;

                if (string.IsNullOrWhiteSpace(configuration.ClientSecret))
                {
                    logger.LogWarning("Fetched secretsmanager secret {SecretName} for {AccessService} but got no value",
                        secretName, accessServiceId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching secret for {AccessService}", accessServiceId);
            }
        }
    }

    private record Secrets(string ClientSecret);
}