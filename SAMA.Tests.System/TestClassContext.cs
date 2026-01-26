using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Npgsql;

namespace SAMA.Tests.System;

/// <summary>
/// Holds the app instance and database schema for a single test class.
/// Each test class gets its own isolated context for parallel execution.
/// </summary>
internal class TestClassContext : IDisposable
{
    private static readonly TimeSpan AppStartupTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan AppShutdownTimeout = TimeSpan.FromSeconds(5);

    private readonly string _schemaName;
    private readonly NpgsqlDataSource _dataSource;
    private Process? _appProcess;

    public string BaseUrl { get; }

    public string TestRunId { get; }

    private TestClassContext(string schemaName, string baseUrl, Process appProcess, NpgsqlDataSource dataSource)
    {
        _schemaName = schemaName;
        BaseUrl = baseUrl;
        TestRunId = schemaName;
        _appProcess = appProcess;
        _dataSource = dataSource;
    }

    public static async Task<TestClassContext> CreateAsync()
    {
        var schemaName = $"systest_{Guid.CreateVersion7():N}";
        var port = GetAvailablePort();
        var baseUrl = $"https://localhost:{port}";

        // Create data source and schema
        var connectionString = GetBaseConnectionString();
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.ConnectionStringBuilder.MaxPoolSize = 2;
        var dataSource = dataSourceBuilder.Build();

        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"CREATE SCHEMA {schemaName}";
            await command.ExecuteNonQueryAsync();
        }

        // Start the app
        var appProcess = await StartAppAsync(schemaName, port, baseUrl);

        return new TestClassContext(schemaName, baseUrl, appProcess, dataSource);
    }

    public void Dispose()
    {
        StopProcess(_appProcess);
        _appProcess = null;

        try
        {
            using var connection = _dataSource.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP SCHEMA IF EXISTS {_schemaName} CASCADE";
            command.ExecuteNonQuery();
        }
        catch
        {
        }

        _dataSource.Dispose();
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string GetBaseConnectionString()
    {
        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "samadb";
        var username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "sama";
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "sama-dev-pw";

        return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
    }

    private static async Task<Process> StartAppAsync(string schemaName, int port, string baseUrl)
    {
        var projectPath = FindProjectPath();
        var connectionString = $"{GetBaseConnectionString()};Search Path={schemaName}";

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectPath}\" --no-build --no-launch-profile",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        startInfo.Environment["ASPNETCORE_URLS"] = $"https://localhost:{port}";
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["ConnectionStrings__DefaultConnection"] = connectionString;
        startInfo.Environment["Encryption__Key"] = "system-test-encryption-key-do-not-use-in-production";

        // Pass parent PID so child process can exit if parent dies (handles hard kills)
        startInfo.Environment["SAMA_PARENT_PID"] = Environment.ProcessId.ToString();

        var appProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the application process.");

        appProcess.BeginOutputReadLine();
        appProcess.BeginErrorReadLine();

        try
        {
            await WaitForAppReadyAsync(appProcess, baseUrl);
        }
        catch
        {
            StopProcess(appProcess);
            throw;
        }

        return appProcess;
    }

    private static string FindProjectPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            var webProjectPath = Path.Combine(directory.FullName, "SAMA.Web", "SAMA.Web.csproj");
            if (File.Exists(webProjectPath))
            {
                return webProjectPath;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find SAMA.Web.csproj. Make sure the solution structure is correct.");
    }

    private static async Task WaitForAppReadyAsync(Process appProcess, string baseUrl)
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };

        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < AppStartupTimeout)
        {
            if (appProcess.HasExited)
            {
                throw new InvalidOperationException(
                    $"Application process exited unexpectedly with code {appProcess.ExitCode}.");
            }

            try
            {
                var response = await client.GetAsync($"{baseUrl}/health");
                if (response.StatusCode == HttpStatusCode.OK ||
                    response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Application did not become ready within {AppStartupTimeout.TotalSeconds} seconds.");
    }

    private static void StopProcess(Process? process)
    {
        if (process == null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit((int)AppShutdownTimeout.TotalMilliseconds);
            }
        }
        catch
        {
        }
        finally
        {
            process.Dispose();
        }
    }
}
