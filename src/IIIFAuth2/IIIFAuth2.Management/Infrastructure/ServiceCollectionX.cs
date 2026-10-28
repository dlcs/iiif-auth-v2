using System.Net.Http.Headers;
using IIIFAuth2.Management.Features.CustomerCreation;
using IIIFAuth2.Management.Infrastructure.Aws;
using IIIFAuth2.Management.Infrastructure.Auth;
using IIIFAuth2.Management.Infrastructure.Dlcs;
using IIIFAuth2.Management.Infrastructure.Sqs;
using IIIFAuth2.Management.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi;

namespace IIIFAuth2.Management.Infrastructure;

public static class ServiceCollectionX
{
    /// <summary>
    /// Configure IOptions bindings
    /// </summary>
    public static IServiceCollection ConfigureOptions(this IServiceCollection services,
        ConfigurationManager configuration)
        => services.Configure<ManagementSettings>(configuration);

    /// <summary>
    /// Add AWS dependencies, uses LocalStack if configured
    /// </summary>
    public static IServiceCollection AddAws(this IServiceCollection services, IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddSingleton<SqsListener>()
            .AddSingleton<SqsQueueUtilities>()
            .SetupAWS(configuration, environment)
            .WithAmazonSQS();

        return services;
    }

    /// <summary>
    /// Add background SQS listeners. A listener is only added if the corresponding queue name is configured.
    /// </summary>
    public static IServiceCollection AddBackgroundServices(this IServiceCollection services, AWSSettings aws)
    {
        if (!string.IsNullOrEmpty(aws.SQS.CustomerCreatedQueueName))
        {
            services
                .AddHostedService(sp =>
                    ActivatorUtilities.CreateInstance<BackgroundListenerService<CustomerCreatedMessageHandler>>(sp,
                        aws.SQS.CustomerCreatedQueueName))
                .AddScoped<CustomerCreatedMessageHandler>();
        }

        return services;
    }

    /// <summary>
    /// Add <see cref="IDlcsApiClient"/> to services collection.
    /// </summary>
    public static IServiceCollection AddDlcsApiClient(this IServiceCollection services, DlcsSettings dlcsSettings)
    {
        if (dlcsSettings.ApiRoot is not { IsAbsoluteUri: true })
        {
            throw new InvalidOperationException("Dlcs:ApiRoot configuration is required and must be an absolute URI");
        }

        services
            .AddScoped<AmbientAuthHandler>()
            .AddHttpClient<IDlcsApiClient, DlcsApiClient>(client =>
            {
                client.BaseAddress = dlcsSettings.ApiRoot;
                client.Timeout = TimeSpan.FromMilliseconds(dlcsSettings.ApiDefaultTimeoutMs);
                client.DefaultRequestHeaders.UserAgent.Add(
                    new ProductInfoHeaderValue("IIIFAuth2ManagementDlcsApiClient", "1.0.0"));
            })
            .AddHttpMessageHandler<AmbientAuthHandler>();

        return services;
    }

    /// <summary>
    /// Add <see cref="DelegatedAuthHandler"/> to services collection.
    /// </summary>
    public static AuthenticationBuilder AddDelegatedAuthHandler(this IServiceCollection services,
        Action<DelegatedAuthenticationOptions> configureOptions)
        => services
            .AddScoped<IAuthenticator, DelegatedAuthenticator>()
            .AddAuthentication(BasicAuthenticationDefaults.AuthenticationScheme)
            .AddScheme<DelegatedAuthenticationOptions, DelegatedAuthHandler>(
                BasicAuthenticationDefaults.AuthenticationScheme, configureOptions);

    /// <summary>
    /// Add Swagger/OpenAPI generation and UI
    /// </summary>
    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(opts =>
        {
            opts.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "IIIF Auth2 Management API",
                Version = "v1"
            });
        });
        return services;
    }
}
