﻿using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
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
    
    public const int Customer = 99;
    public const string ClickthroughService = "clickthrough";
    public Guid AccessId;
    public const string ClickthroughRoleUri = "http://dlcs.test/99/clickthrough";

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
        DbContext.Database.ExecuteSqlRaw($"DELETE FROM roles WHERE access_service_id != '{AccessId.ToString()}';");
        DbContext.Database.ExecuteSqlRaw($"DELETE FROM access_services WHERE id != '{AccessId.ToString()}';");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM role_providers;");
    }

    private void SeedData()
    {
        var accessService = new AccessService
        {
            Customer = Customer,
            Id = AccessId,
            Name = ClickthroughService,
            Profile = "active",
        };
        DbContext.AccessServices.Add(accessService);

        DbContext.Roles.Add(new Role
        {
            Customer = Customer,
            Id = ClickthroughRoleUri,
            Name = "clickthrough-role",
            AccessServiceId = accessService.Id
        });
        AccessId = accessService.Id;
        DbContext.SaveChanges();
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