using IIIFAuth2.API.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace IIIFAuth2.API.Tests.Infrastructure;

/// <summary>
/// Xunit fixture that manages lifecycle for Postgres 13 container with migrations applied.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer  postgresContainer;

    public AuthServicesContext DbContext { get; private set; } = null!;
    public string ConnectionString { get; private set; } = null!;
        
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
        }
        catch (Exception ex)
        {
            var m = ex.Message;
            throw;
        }
    }

    public Task DisposeAsync() => postgresContainer.StopAsync();
    
    private void SetPropertiesFromContainer()
    {
        ConnectionString = postgresContainer.GetConnectionString();

        // Create new context using connection string for Postgres container
        DbContext = new AuthServicesContext(
            new DbContextOptionsBuilder<AuthServicesContext>()
                .UseNpgsql(ConnectionString)
                .UseSnakeCaseNamingConvention()
                .Options
        );
    }
}

[CollectionDefinition(CollectionName)]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string CollectionName = "Database Collection";
}