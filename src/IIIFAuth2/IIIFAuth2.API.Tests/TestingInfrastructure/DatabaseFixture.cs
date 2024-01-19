using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace IIIFAuth2.API.Tests.TestingInfrastructure;

/// <summary>
/// Xunit fixture that manages lifecycle for Postgres 13 container with migrations applied.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer  postgresContainer;

    public AuthServicesContext DbContext { get; private set; } = null!;
    public string ConnectionString { get; private set; } = null!;
    
    public const int Customer = 99;
    public const string CookieDomain = "localhost";
    public const string ClickthroughService = "clickthrough";
    public const string OidcService = "oidc";
    public Guid ClickthroughAccessId;
    public Guid ClickthroughRoleProviderId; 
    public Guid OidcAccessId;
    public Guid OidcRoleProviderId; 
    public const string ClickthroughRoleUri = "http://dlcs.test/99/clickthrough";
    public const string OidcRoleUri = "http://dlcs.test/99/oidc";

    public DatabaseFixture()
    {
        var postgresBuilder = new PostgreSqlBuilder()
            .WithImage("postgres:13-alpine")
            .WithDatabase("db")
            .WithUsername("postgres")
            .WithPassword("postgtes_pword")
            .WithCleanUp(true)
            .WithLabel("iiifauth2_test", "True");
        
        postgresContainer = postgresBuilder.Build();
    }
    
    public async Task InitializeAsync()
    {
        // Start DB + apply migrations
        try
        {
            await postgresContainer.StartAsync();
            SetPropertiesFromContainer();
            await DbContext.Database.MigrateAsync();
            SeedData();
        }
        catch (Exception ex)
        {
            var m = ex.Message;
            throw;
        }
    }

    public void CleanUp()
    {
        DbContext.Database.ExecuteSqlRaw("DELETE FROM session_users;");
        DbContext.Database.ExecuteSqlRaw($"DELETE FROM roles WHERE access_service_id not in ('{ClickthroughAccessId.ToString()}', '{OidcAccessId.ToString()}');");
        DbContext.Database.ExecuteSqlRaw($"DELETE FROM access_services WHERE id not in ('{ClickthroughAccessId.ToString()}', '{OidcAccessId.ToString()}');");
        DbContext.Database.ExecuteSqlRaw($"DELETE FROM role_providers WHERE id not in ('{ClickthroughRoleProviderId.ToString()}', '{OidcRoleProviderId.ToString()}');");
        DbContext.Database.ExecuteSqlRaw($"DELETE FROM customer_cookie_domains WHERE customer != '{Customer}';");
    }

    private void SeedData()
    {
        var clickthroughRoleProvider = new RoleProvider
        {
            Configuration = new RoleProviderConfiguration
            {
                [RoleProviderConfiguration.DefaultKey] = new ClickthroughConfiguration
                {
                    Config = RoleProviderType.Clickthrough, GestureMessage = "Test-Message", GestureTitle = "Test-Title"
                }
            }
        };
        var oidcRoleProvider = new RoleProvider
        {
            Configuration = new RoleProviderConfiguration
            {
                [RoleProviderConfiguration.DefaultKey] = new OidcConfiguration
                {
                    Config = RoleProviderType.Oidc, GestureMessage = "Test-Message", GestureTitle = "Test-Title",
                    Provider = "auth0", Domain = "http://sample-domain.idp", Scopes = "test-scope",
                    ClientId = "clientId", ClientSecret = "secretsmanager:clientSecret",
                    UnknownValueBehaviour = UnknownMappingValueBehaviour.Fallback,
                    FallbackMapping = new[] { "OidcRoleUri" }
                }
            }
        };
        DbContext.RoleProviders.AddRange(clickthroughRoleProvider, oidcRoleProvider);
        
        var clickthroughAccessService = new AccessService
        {
            Customer = Customer,
            Id = ClickthroughAccessId,
            Name = ClickthroughService,
            Profile = "active",
            RoleProvider = clickthroughRoleProvider
        };
        var oidcAccessService = new AccessService
        {
            Customer = Customer,
            Id = OidcAccessId,
            Name = OidcService,
            Profile = "active",
            RoleProvider = oidcRoleProvider
        };
        DbContext.AccessServices.AddRange(clickthroughAccessService, oidcAccessService);

        DbContext.Roles.AddRange(new Role
        {
            Customer = Customer,
            Id = ClickthroughRoleUri,
            Name = "clickthrough-role",
            AccessServiceId = clickthroughAccessService.Id
        }, new Role
        {
            Customer = Customer,
            Id = OidcRoleUri,
            Name = "oid-role",
            AccessServiceId = oidcAccessService.Id
        });
        ClickthroughAccessId = clickthroughAccessService.Id;
        ClickthroughRoleProviderId = clickthroughAccessService.RoleProviderId.Value;
        OidcAccessId = oidcAccessService.Id;
        OidcRoleProviderId = oidcAccessService.RoleProviderId.Value;

        DbContext.CustomerCookieDomains.Add(new CustomerCookieDomain
        {
            Customer = Customer,
            Domains = new List<string> { CookieDomain }
        });
        DbContext.SaveChanges();

        var x = DbContext.AccessServices;
    }

    public Task DisposeAsync() => postgresContainer.StopAsync();

    public AuthServicesContext CreateNewAuthServiceContext()
        => new(
            new DbContextOptionsBuilder<AuthServicesContext>()
                .UseNpgsql(ConnectionString)
                .UseSnakeCaseNamingConvention()
                .Options
        );
    
    private void SetPropertiesFromContainer()
    {
        ConnectionString = postgresContainer.GetConnectionString();

        // Create new context using connection string for Postgres container
        DbContext = CreateNewAuthServiceContext();
    }
}

[CollectionDefinition(CollectionName)]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string CollectionName = "Database Collection";
}