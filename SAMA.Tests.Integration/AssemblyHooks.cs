using Npgsql;

namespace SAMA.Tests.Integration;

[TestClass]
public static class AssemblyHooks
{
    [AssemblyInitialize]
    public static async Task CleanupStaleTestSchemasAsync(TestContext _)
    {
        var connString = IntegrationTestBase.GetAdminConnectionString();
        await using var dataSource = NpgsqlDataSource.Create(connString);
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();

        // Find test schemas older than 1 hour based on GUIDv7 timestamp in name
        cmd.CommandText = "SELECT nspname FROM pg_namespace WHERE nspname LIKE 'test_%'";
        await using var reader = await cmd.ExecuteReaderAsync();

        var staleSchemas = new List<string>();
        var cutoff = DateTimeOffset.UtcNow.AddHours(-1);

        while (await reader.ReadAsync())
        {
            var schemaName = reader.GetString(0);
            if (TryGetSchemaTimestamp(schemaName, out var timestamp) && timestamp < cutoff)
            {
                staleSchemas.Add(schemaName);
            }
        }

        await reader.CloseAsync();

        foreach (var schema in staleSchemas)
        {
            try
            {
                await using var dropCmd = conn.CreateCommand();
                dropCmd.CommandText = $"DROP SCHEMA IF EXISTS {schema} CASCADE";
                await dropCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to drop stale schema {schema}: {ex.Message}");
            }
        }
    }

    [AssemblyCleanup]
    public static async Task CleanupAllSchemasAsync()
    {
        foreach (var state in IntegrationTestBase.AllClassStates)
        {
            try
            {
                await using var conn = await state.DataSource.OpenConnectionAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"DROP SCHEMA IF EXISTS {state.SchemaName} CASCADE";
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to drop schema {state.SchemaName}: {ex.Message}");
            }
            finally
            {
                await state.DataSource.DisposeAsync();
            }
        }
    }

    private static bool TryGetSchemaTimestamp(string schemaName, out DateTimeOffset timestamp)
    {
        // Schema names are: test_{classname}_{guidv7hex}
        // GUIDv7 has the unix timestamp in ms in the first 48 bits
        timestamp = default;
        var lastUnderscore = schemaName.LastIndexOf('_');
        if (lastUnderscore < 0 || lastUnderscore + 1 >= schemaName.Length)
        {
            return false;
        }

        var guidHex = schemaName[(lastUnderscore + 1)..];
        if (guidHex.Length != 32 || !Guid.TryParse(guidHex, out _))
        {
            return false;
        }

        // First 12 hex chars = 48 bits = unix timestamp in milliseconds
        if (!long.TryParse(guidHex[..12], System.Globalization.NumberStyles.HexNumber, null, out var unixMs))
        {
            return false;
        }

        timestamp = DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
        return timestamp.Year is >= 2024 and <= 2100;
    }
}
