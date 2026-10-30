using Amazon.SecretsManager;
using Amazon.SecretsManager.Extensions.Caching;
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
using Microsoft.OpenApi;
using Serilog;
using Serilog.Extensions.Logging;

namespace IIIFAuth2.API.Infrastructure;

public static class ServiceCollectionX
{
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
    /// Add Swagger/OpenAPI generation and UI
    /// </summary>
    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(opts =>
        {
            opts.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "IIIF Auth2 API",
                Version = "v1"
            });
        });
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
            .AddScoped<IpRoleEvaluator>()
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
    /// Configures host to use x-forwarded-host, x-forwarded-proto and x-forwarded-for to set
    /// httpContext.Request.Host, .Scheme and .Connection.RemoteIpAddress respectively.
    /// If "KnownNetworks" configuration key found, this will be used to set ForwardedHeadersOptions.KnownNetworks.
    /// If "ForwardLimit" configuration key found, this will be used to set ForwardedHeadersOptions.ForwardLimit
    /// </summary>
    /// <remarks>
    /// If "KnownNetworks" key not found, all networks are allowed. This maintains the behaviour that was present in
    /// dotnet until .NET 8.0.17 + .NET 9.0.6 release and so avoids breaking changes.
    /// If "KnownNetworks" key is found then the default is maintained and any CIDR addresses are added.
    /// As the client IP is used to grant IpRestriction roles, "KnownNetworks" should be set in deployed environments
    /// to prevent x-forwarded-for being spoofed by callers that can reach the service directly.
    /// </remarks>
    public static IServiceCollection ConfigureForwardedHeaders(this IServiceCollection services,
        IConfiguration configuration)
    {
        const string configurationKey = "KnownNetworks";
        const string allNetworks = "AllNetworks";
        var knownNetworks = configuration.GetValue(configurationKey, allNetworks)!;
        var forwardLimit = configuration.GetValue<int?>("ForwardLimit", 1);

        var logger = new SerilogLoggerFactory(Log.Logger).CreateLogger("ServiceCollection");

        // Use x-forwarded-host, x-forwarded-proto and x-forwarded-for to set httpContext.Request.Host, .Scheme and
        // .Connection.RemoteIpAddress respectively
        return services.Configure<ForwardedHeadersOptions>(opts =>
        {
            opts.ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto |
                                    ForwardedHeaders.XForwardedFor;
            opts.ForwardLimit = forwardLimit;

            if (knownNetworks.Equals(allNetworks))
            {
                logger.LogWarning(
                    "Forwarded header values accepted from all networks and proxies. XFF Client IP can be spoofed");
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