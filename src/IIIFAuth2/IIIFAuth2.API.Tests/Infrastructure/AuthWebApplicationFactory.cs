using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IIIFAuth2.API.Tests.Infrastructure;

public class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string> configuration = new();
    private Action<IServiceCollection>? configureTestServices = null;

    /// <summary>
    /// Specify connection string to use for dbContext when building services
    /// </summary>
    /// <param name="connectionString">connection string to use for dbContext - docker instance</param>
    /// <returns>Current instance</returns>
    public AuthWebApplicationFactory WithConnectionString(string connectionString)
    {
        configuration["ConnectionStrings:Postgres"] = connectionString;
        return this;
    }
    
    /// <summary>
    /// Specify a configuration value to be set in appFactory
    /// </summary>
    /// <param name="key">Key of setting to update, in format ("foo:bar")</param>
    /// <param name="value">Value to set</param>
    /// <returns>Current instance</returns>
    public AuthWebApplicationFactory WithConfigValue(string key, string value)
    {
        configuration[key] = value;
        return this;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var projectDir = Directory.GetCurrentDirectory();
        //var configPath = Path.Combine(projectDir, "appsettings.Testing.json");

        builder
            .ConfigureAppConfiguration((context, conf) =>
            {
          //      conf.AddJsonFile(configPath);
                conf.AddInMemoryCollection(configuration);
            })
            .ConfigureServices(services =>
            {
                configureTestServices?.Invoke(services);
            })
            .UseEnvironment("Testing");

        return base.CreateHost(builder);
    }
}