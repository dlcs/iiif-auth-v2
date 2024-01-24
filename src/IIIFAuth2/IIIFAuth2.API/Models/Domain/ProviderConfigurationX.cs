namespace IIIFAuth2.API.Models.Domain;

public static class ProviderConfigurationX
{
    public static T SafelyGetTypedConfig<T>(this IProviderConfiguration providerConfiguration)
        where T : IProviderConfiguration
        => providerConfiguration is not T configuration
            ? throw new ArgumentException("Unable to handle provided configuration", nameof(providerConfiguration))
            : configuration;
}