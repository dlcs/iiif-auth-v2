using IIIFAuth2.API.Data;
using IIIFAuth2.API.Infrastructure;
using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Settings;
using JetBrains.Annotations;
using MediatR;
using Serilog;

// Prevent R# flagging View() as not found
[assembly: AspMvcViewLocationFormat(@"~\Features\Access\Views\{0}.cshtml")]

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

Log.Information("Application starting..");

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((hostContext, loggerConfiguration)
        => loggerConfiguration
            .ReadFrom.Configuration(hostContext.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithCorrelationIdHeader());
    
    builder.Services
        .ConfigureOptions(builder.Configuration)
        .AddHttpContextAccessor()
        .AddScoped<IUrlPathProvider, UrlPathProvider>()
        .AddAuthServices()
        .AddAuthServicesContext(builder.Configuration)
        .AddAuthServicesHealthChecks()
        .AddMediatR(typeof(Program))
        .AddCaching()
        .AddAws(builder.Configuration)
        .ConfigureAspnetMvc();

    var apiSettings = builder.Configuration.Get<ApiSettings>()!;
    
    var app = builder.Build();
    app
        .UseSerilogRequestLogging()
        .HandlePathBase(apiSettings.PathBase, app.Logger)
        .UseRouting()
        .TryRunMigrations(app.Configuration, app.Logger);

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseForwardedHeaders();
    app.MapRazorPages();
    app.MapControllers();
    app.UseEndpoints(endpoints => { endpoints.MapHealthChecks("/health"); });

    app.Run();
}
catch (HostAbortedException)
{
    // No-op - required when adding migrations,
    // See: https://github.com/dotnet/efcore/issues/29809#issuecomment-1345132260
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception on startup");
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}

// required for WebApplicationFactory
public partial class Program { }