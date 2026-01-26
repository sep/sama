using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace SAMA.Tests.System;

/// <summary>
/// Cleanup tests to remove stale system test artifacts.
/// Run with: cleanup.bat or cleanup.sh
/// WARNING: This will delete all systest_* schemas and clear all smtp4dev emails.
/// </summary>
[TestClass]
[CleanupTestCondition]
public class CleanupTests
{
    private const string Smtp4DevApiUrl = "http://localhost:6467/api";

    [TestMethod]
    public async Task CleanupStaleSchemasAndEmails()
    {
        var schemasDeleted = await CleanupSchemasAsync();
        var emailsDeleted = await CleanupSmtp4DevEmailsAsync();

        Console.WriteLine($"Cleanup complete: {schemasDeleted} schemas deleted, {emailsDeleted} emails deleted.");
    }

    private static async Task<int> CleanupSchemasAsync()
    {
        var connectionString = GetConnectionString();
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync();

        // Find all systest_* schemas
        var schemas = new List<string>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT schema_name FROM information_schema.schemata WHERE schema_name LIKE 'systest_%'";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                schemas.Add(reader.GetString(0));
            }
        }

        // Drop each schema
        foreach (var schema in schemas)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"DROP SCHEMA IF EXISTS {schema} CASCADE";
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine($"Dropped schema: {schema}");
        }

        return schemas.Count;
    }

    private static async Task<int> CleanupSmtp4DevEmailsAsync()
    {
        using var httpClient = new HttpClient();

        try
        {
            // Get all messages to count them
            var response = await httpClient.GetAsync($"{Smtp4DevApiUrl}/Messages");
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Warning: Could not connect to smtp4dev at {Smtp4DevApiUrl}");
                return 0;
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var results = json.GetProperty("results");
            var count = results.GetArrayLength();

            // Delete all messages
            var deleteResponse = await httpClient.DeleteAsync($"{Smtp4DevApiUrl}/Messages/*");
            if (deleteResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Cleared {count} emails from smtp4dev");
            }

            return count;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Warning: Could not connect to smtp4dev: {ex.Message}");
            return 0;
        }
    }

    private static string GetConnectionString()
    {
        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "samadb";
        var username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "sama";
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "sama-dev-pw";

        return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
    }
}
