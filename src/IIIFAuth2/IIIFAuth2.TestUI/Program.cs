using IIIFAuth2.TestUI.FakeOrchestrator;
using IIIFAuth2.TestUI.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var settings = builder.Configuration.Get<TestUISettings>() ?? new TestUISettings();
var authServiceRoot = settings.AuthServiceRoot.ToString().TrimEnd('/') + "/";

builder.Services.AddRazorPages();
builder.Services
    .AddSingleton<DeliverableStore>()
    .AddSingleton<CallLog>()
    .AddSingleton<RoleLookup>()
    .AddTransient<CallLoggingHandler>()
    .AddCors(options => options.AddPolicy(FakeOrchestratorEndpoints.CorsPolicy,
        policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// Cookies are manually forwarded (as Orchestrator header propagation), so don't let HttpClient manage them
builder.Services
    .AddHttpClient<AuthServiceClient>(client => client.BaseAddress = new Uri(authServiceRoot))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false })
    .AddHttpMessageHandler<CallLoggingHandler>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseCors();

app.MapRazorPages();
app.MapFakeOrchestrator();
app.MapGet("/calls/json", (CallLog callLog, long? after) => callLog.GetAfter(after ?? 0));

app.Logger.LogInformation("Using iiif-auth-v2 at {AuthServiceRoot}", authServiceRoot);
app.Run();
