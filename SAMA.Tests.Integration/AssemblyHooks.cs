namespace SAMA.Tests.Integration;

[TestClass]
public static class AssemblyHooks
{
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
}
