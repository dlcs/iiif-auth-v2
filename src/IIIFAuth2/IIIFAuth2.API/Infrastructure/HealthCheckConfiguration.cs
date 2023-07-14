using IIIFAuth2.API.Data;

namespace IIIFAuth2.API.Infrastructure;

public static class HealthCheckConfiguration
{
    /// <summary>
    /// Add required health checks
    /// </summary>
    public static IServiceCollection AddAuthServicesHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddDbContextCheck<AuthServicesContext>();
        return services;
    }
} 