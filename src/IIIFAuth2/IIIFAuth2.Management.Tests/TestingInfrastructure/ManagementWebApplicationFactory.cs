using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IIIFAuth2.Management.Tests.TestingInfrastructure;

public class ManagementWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> configuration = new();
    private Action<IServiceCollection>? configureTestServices;

    /// <summary>
    /// Specify connection string to use for dbContext when building services
    /// </summary>
    /// <param name="connectionString">connection string to use for dbContext - docker instance</param>
    /// <returns>Current instance</returns>
    public ManagementWebApplicationFactory WithConnectionString(string connectionString)
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
    public ManagementWebApplicationFactory WithConfigValue(string key, string value)
    {
        configuration[key] = value;
        return this;
    }
    
    /// <summary>
    /// Action to call in ConfigureTestServices
    /// </summary>
    /// <returns>Current instance</returns>
    public ManagementWebApplicationFactory WithTestServices(Action<IServiceCollection> configure)
    {
        this.configureTestServices = configure;
        return this;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var projectDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(projectDir, "appsettings.Testing.json");

        // Host configuration is applied before Program.cs runs so is visible to settings read eagerly at startup.
        // App configuration is applied at Build() but takes precedence over the app's own appsettings files.
        builder
            .ConfigureHostConfiguration(conf =>
            {
                conf.AddJsonFile(configPath);
                conf.AddInMemoryCollection(configuration);
            })
            .ConfigureAppConfiguration((context, conf) =>
            {
                conf.AddJsonFile(configPath);
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