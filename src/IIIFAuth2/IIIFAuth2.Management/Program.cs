using IIIFAuth2.Core.Data;
using IIIFAuth2.Management.Infrastructure;
using IIIFAuth2.Management.Settings;
using Serilog;

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

    var managementSettings = builder.Configuration.Get<ManagementSettings>()!;

    builder.Services
        .ConfigureOptions(builder.Configuration)
        .AddHttpContextAccessor()
        .AddAuthServicesContext(builder.Configuration)
        .AddAuthServicesHealthChecks()
        .AddMediatR(config => config.RegisterServicesFromAssemblyContaining<Program>())
        .AddLazyCache()
        .AddDlcsApiClient(managementSettings.Dlcs)
        .AddSwagger()
        .AddDelegatedAuthHandler(opts => opts.Realm = "DLCS-API");

    builder.Services.AddAuthorization();
    builder.Services.AddControllers();

    var app = builder.Build();
    app
        .UseSerilogRequestLogging()
        .UseRouting()
        .UseAuthentication()
        .UseAuthorization()
        .TryRunMigrations(app.Configuration, app.Logger);

    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapControllers().RequireAuthorization();
    app.MapHealthChecks("/health");

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
