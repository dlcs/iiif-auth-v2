using IIIFAuth2.API.Data;
using IIIFAuth2.API.Features.Access.Requests;
using IIIFAuth2.API.Infrastructure.Auth;
using IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Settings;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Razor;

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
            .Configure<ForwardedHeadersOptions>(opts =>
            {
                opts.ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;
            })
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
        => services
            .AddScoped<AuthAspectManager>()
            .AddScoped<ICustomerDomainChecker, CustomerDomainService>()
            .AddScoped<ICustomerDomainProvider, CustomerDomainService>()
            .AddScoped<RoleProviderService>()
            .AddScoped<ClickthroughRoleProviderHandler>()
            .AddScoped<SessionManagementService>()
            .AddScoped<SessionCleaner>()
            .AddScoped<RoleProviderHandlerResolver>(provider => roleProviderType => roleProviderType switch
            {
                RoleProviderType.Clickthrough => provider.GetRequiredService<ClickthroughRoleProviderHandler>(),
                _ => throw new ArgumentOutOfRangeException(nameof(roleProviderType), roleProviderType, null)
            });

    /// <summary>
    /// Add caching dependencies
    /// </summary>
    /// <remarks>
    /// This adds LazyCache, Z.EntityFramework.Plus.EFCore caching is also used but there is no setup as default
    /// MemoryCache is enough
    /// </remarks>
    public static IServiceCollection AddCaching(this IServiceCollection services) => services.AddLazyCache();
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