using Amazon.SecretsManager;
using Amazon.SecretsManager.Extensions.Caching;
using AWSSDK;
using IIIFAuth2.API.Data;
using IIIFAuth2.API.Infrastructure.Auth;
using IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning;
using IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Oidc;
using IIIFAuth2.API.Settings;
using IIIFAuth2.API.Utils;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Razor;
using Serilog;
using Serilog.Extensions.Logging;

namespace IIIFAuth2.API.Infrastructure;

public static class ServiceCollectionX
{
    /// <summary>
    /// Add required health checks
    /// </summary>
    public static IServiceCollection AddAuthServicesHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddDbContextCheck<AuthServicesContext>();
        return services;
    }
    
    /// <summary>
    /// Configure AspNet - controllers, razorViews etc
    /// </summary>
    /// <param name="services">Current IServiceCollection object</param>
    /// <returns>Modified IServiceCollection object</returns>
    public static IServiceCollection ConfigureAspnetMvc(this IServiceCollection services)
    {
        services
            .Configure<MvcOptions>(opts =>
            {
                opts.Conventions.Add(new FeatureControllerModelConvention());
            })
            .Configure<RazorViewEngineOptions>(opts =>
            {
                opts.ViewLocationFormats.Clear();
                opts.ViewLocationFormats.Add(@"{Feature}\{0}.cshtml");
                opts.ViewLocationFormats.Add(@"{Feature}\Views\{0}.cshtml");
                opts.ViewLocationFormats.Add(@"\Features\{0}\{1}.cshtml");

                opts.ViewLocationExpanders.Add(new FeatureFolderViewExpander());
            });
        
        services.AddControllers();
        services.AddRazorPages();
        return services;
    }

    /// <summary>
    /// Configure IOptions bindings
    /// </summary>
    public static IServiceCollection ConfigureOptions(this IServiceCollection services,
        ConfigurationManager configuration)
        => services
            .Configure<ApiSettings>(configuration)
            .Configure<AuthSettings>(configuration.GetSection("Auth"));

    /// <summary>
    /// Add dependencies for handling auth requests
    /// </summary>
    public static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        var serviceCollection = services
            .AddScoped<AuthAspectManager>()
            .AddScoped<ICustomerDomainChecker, CustomerDomainService>()
            .AddScoped<ICustomerDomainProvider, CustomerDomainService>()
            .AddScoped<RoleProviderService>()
            .AddScoped<RoleProvisionGranter>()
            .AddScoped<IJwtTokenHandler, JwtTokenHandler>()
            .AddScoped<OidcRoleProviderHandler>()
            .AddScoped<ClickThroughProviderHandler>()
            .AddScoped<SessionManagementService>()
            .AddSingleton<ClaimsConverter>()
            .AddScoped<SessionCleaner>();

        services.AddHttpClient<IOAuthClient, OAuthClient>();

        return serviceCollection;
    }

    /// <summary>
    /// Add caching dependencies
    /// </summary>
    /// <remarks>
    /// This adds LazyCache, Z.EntityFramework.Plus.EFCore caching is also used but there is no setup as default
    /// MemoryCache is enough. SecretsManagerCache also uses memory cache but that is configured with AWS deps
    /// </remarks>
    public static IServiceCollection AddCaching(this IServiceCollection services) => services.AddLazyCache();

    /// <summary>
    /// Add AWS dependencies
    /// </summary>
    public static IServiceCollection AddAws(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddDefaultAWSOptions(configuration.GetAWSOptions())
            .AddAWSService<IAmazonSecretsManager>()
            .AddSingleton<ISecretsManagerCache, SecretsManagerCache>();

    /// <summary>
    /// Configures host to use x-forwarded-host and x-forwarded-proto to set httpContext.Request.Host and .Scheme
    /// respectively.
    /// If "KnownNetworks" configuration key found, this will be used to set ForwardedHeadersOptions.KnownNetworks 
    /// </summary>
    /// <remarks>
    /// If "KnownNetworks" key not found, all networks are allowed. This maintains the behaviour that was present in
    /// dotnet until .NET 8.0.17 + .NET 9.0.6 release and so avoids breaking changes.
    /// If "KnownNetworks" key is found then the default is maintained and any CIDR addresses are added
    /// </remarks>
    public static IServiceCollection ConfigureForwardedHeaders(this IServiceCollection services,
        IConfiguration configuration)
    {
        const string configurationKey = "KnownNetworks";
        const string allNetworks = "AllNetworks";
        var knownNetworks = configuration.GetValue(configurationKey, allNetworks)!;

        var logger = new SerilogLoggerFactory(Log.Logger).CreateLogger("ServiceCollection");

        // Use x-forwarded-host and x-forwarded-proto to set httpContext.Request.Host and .Scheme respectively
        return services.Configure<ForwardedHeadersOptions>(opts =>
        {
            opts.ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;

            if (knownNetworks.Equals(allNetworks))
            {
                logger.LogWarning("Forwarded header values accepted from all networks and proxies");
                opts.KnownIPNetworks.Clear();
                opts.KnownProxies.Clear();
            }
            else
            {
                logger.LogInformation("Forwarded header values accepted from networks: {KnownNetworks}", knownNetworks);
                foreach (var kn in knownNetworks.SplitSeparatedString(","))
                {
                    opts.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(kn));
                }
            }
        });
    }

}

/// <summary>
/// <see cref="IControllerModelConvention"/> that sets "feature" property in ControllerModel
/// </summary>
public class FeatureControllerModelConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        controller.Properties.Add("feature", DeriveFeatureFolderName(controller));
    }
    
    private string DeriveFeatureFolderName(ControllerModel model)
    {
        var controllerNamespace = model.ControllerType.Namespace ?? string.Empty;
        var result = controllerNamespace.Split('.')
            .SkipWhile(s => s != "Features")
            .Aggregate(string.Empty, Path.Combine);

        return result;
    }
}

/// <summary>
/// <see cref="IViewLocationExpander"/> to find views in Feature folders
/// </summary>
public class FeatureFolderViewExpander : IViewLocationExpander
{
    public void PopulateValues(ViewLocationExpanderContext context)
    {
        // no-op
    }

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        var controllerDescriptor = context.ActionContext.ActionDescriptor as ControllerActionDescriptor;
        var featureName = controllerDescriptor?.Properties["feature"] as string;

        foreach (var location in viewLocations)
        {
            yield return location.Replace("{Feature}", featureName);
        }
    }
}