using Amazon;
using Amazon.Runtime;
using Amazon.SQS;

namespace IIIFAuth2.Management.Infrastructure.Aws;

/// <summary>
/// Base class for wiring up AWS dependencies. Handles using either LocalStack or AWS dependant on config.
/// </summary>
public static class AWSConfiguration
{
    /// <summary>
    /// Setup AWS environment by configuring appropriate services.
    /// </summary>
    public static AwsBuilder SetupAWS(this IServiceCollection services, IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configurationSection = configuration.GetSection(AWSSettings.SettingsName);
        services.Configure<AWSSettings>(configurationSection);
        var awsSettings = configurationSection.Get<AWSSettings>() ?? new AWSSettings();

        var useLocalStack = environment.IsDevelopment() && awsSettings.UseLocalStack;

        if (!useLocalStack)
        {
            services.AddDefaultAWSOptions(configuration.GetAWSOptions());
        }

        return new AwsBuilder(awsSettings, services, useLocalStack);
    }
}

/// <summary>
/// Wrapper around awssdk.extensions.netcore.setup methods for configuring AWS services.
/// Switches between 'real' AWS and LocalStack depending on configuration settings.
/// If "AWS:UseLocalStack" = true, and environment = Develop then localstack used. Else AWS
/// </summary>
public class AwsBuilder(AWSSettings awsSettings, IServiceCollection services, bool useLocalStack)
{
    /// <summary>
    /// Add <see cref="IAmazonSQS"/> to service collection with specified lifetime.
    /// </summary>
    /// <param name="lifetime">ServiceLifetime for dependency</param>
    /// <returns>Current <see cref="AwsBuilder"/> instance</returns>
    public AwsBuilder WithAmazonSQS(ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        if (useLocalStack)
        {
            var serviceDescriptor = ServiceDescriptor.Describe(typeof(IAmazonSQS), _ =>
            {
                if (string.IsNullOrWhiteSpace(awsSettings.SQS.ServiceUrl))
                {
                    throw new InvalidOperationException("AWS:SQS:ServiceUrl must be set when using LocalStack");
                }

                var amazonSqsConfig = new AmazonSQSConfig
                {
                    UseHttp = true,
                    RegionEndpoint = RegionEndpoint.USEast1,
                    ServiceURL = awsSettings.SQS.ServiceUrl,
                    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
                };
                return new AmazonSQSClient(new BasicAWSCredentials("foo", "bar"), amazonSqsConfig);
            }, lifetime);
            services.Add(serviceDescriptor);
        }
        else
        {
            services.AddAWSService<IAmazonSQS>(lifetime);
        }

        return this;
    }
}
