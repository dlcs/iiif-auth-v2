using Npgsql;

namespace IIIFAuth2.TestUI.Infrastructure;

public record KnownRole(string Id, int Customer, string Name);

public record RoleLookupResult(IReadOnlyList<KnownRole> Roles, string? Error);

/// <summary>
/// Reads known roles directly from the iiif-auth-v2 database. Failure is not fatal, roles can be free-typed.
/// </summary>
public class RoleLookup(IConfiguration configuration, ILogger<RoleLookup> logger)
{
    public async Task<RoleLookupResult> GetRoles(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrEmpty(connectionString))
        {
            return new RoleLookupResult([], "No 'Postgres' connection string configured");
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command =
                new NpgsqlCommand("SELECT id, customer, name FROM roles ORDER BY customer, name", connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var roles = new List<KnownRole>();
            while (await reader.ReadAsync(cancellationToken))
            {
                roles.Add(new KnownRole(reader.GetString(0), reader.GetInt32(1), reader.GetString(2)));
            }

            return new RoleLookupResult(roles, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to read roles from database");
            return new RoleLookupResult([], ex.Message);
        }
    }
}
