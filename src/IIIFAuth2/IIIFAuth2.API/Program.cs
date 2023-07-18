using IIIFAuth2.API.Data;
using IIIFAuth2.API.Infrastructure;
using MediatR;
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

    builder.Services
        .AddHttpContextAccessor()
        .AddAuthServicesContext(builder.Configuration)
        .AddAuthServicesHealthChecks()
        .AddMediatR(typeof(Program))
        .AddControllers();

    var app = builder.Build();
    app
        .UseSerilogRequestLogging()
        .UseRouting()
        .TryRunMigrations(app.Configuration, app.Logger);

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