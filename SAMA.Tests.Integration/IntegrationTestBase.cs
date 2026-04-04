using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Data.Services;

namespace SAMA.Tests.Integration;

public abstract class IntegrationTestBase
{
    private static readonly ConcurrentDictionary<Type, Lazy<ClassState>> _classStates = new();
    internal static readonly ConcurrentBag<ClassState> AllClassStates = new();

    private ClassState _classState = null!;
    private ServiceProvider _serviceProvider = null!;

    protected SamaDbContext DbContext { get; private set; } = null!;

    protected IServiceProvider ServiceProvider => _serviceProvider;

    [TestInitialize]
    public virtual async Task InitializeTestAsync()
    {
        var type = GetType();

        var lazy = _classStates.GetOrAdd(type, _ => new Lazy<ClassState>(() =>
        {
            var schemaName = $"test_{type.Name.ToLowerInvariant()}_{Guid.CreateVersion7():N}";
            var connectionString = GetConnectionString(schemaName);

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.ConnectionStringBuilder.MaxPoolSize = 3;
            dataSourceBuilder.ConnectionStringBuilder.MinPoolSize = 0;
            dataSourceBuilder.ConnectionStringBuilder.ConnectionLifetime = 30;
            dataSourceBuilder.ConnectionStringBuilder.Timeout = 30;

            var state = new ClassState
            {
                SchemaName = schemaName,
                DataSource = dataSourceBuilder.Build()
            };
            AllClassStates.Add(state);
            return state;
        }));

        var isFirstTest = !lazy.IsValueCreated;
        _classState = lazy.Value;

        await InitializeServicesAsync();

        if (isFirstTest)
        {
            await CreateSchemaAsync();
            await ApplyMigrationsAsync();
        }
        else
        {
            await TruncateAllTablesAsync();
        }
    }

    [TestCleanup]
    public virtual async Task CleanupTestAsync()
    {
        try
        {
            if (DbContext != null)
            {
                await DbContext.Database.CloseConnectionAsync();
                await DbContext.DisposeAsync();
            }
        }
        finally
        {
            if (_serviceProvider != null)
            {
                await _serviceProvider.DisposeAsync();
            }
        }
    }

    protected void ConfigurePageModel(PageModel pageModel)
    {
        var httpContext = new DefaultHttpContext();
        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new PageActionDescriptor(), modelState);
        var modelMetadataProvider = new EmptyModelMetadataProvider();
        var viewData = new ViewDataDictionary(modelMetadataProvider, modelState);
        var tempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>());
        var pageContext = new PageContext(actionContext)
        {
            ViewData = viewData
        };

        pageModel.PageContext = pageContext;
        pageModel.TempData = tempData;
        pageModel.Url = Substitute.For<IUrlHelper>();
        pageModel.MetadataProvider = modelMetadataProvider;
    }

    internal static string GetAdminConnectionString()
    {
        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "samadb";
        var username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "sama";
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "sama-dev-pw";

        return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
    }

    private static string GetConnectionString(string schemaName)
    {
        return $"{GetAdminConnectionString()};Search Path={schemaName};Options=-c synchronous_commit=off";
    }

    private async Task CreateSchemaAsync()
    {
        await using var connection = await _classState.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE SCHEMA {_classState.SchemaName}";
        await command.ExecuteNonQueryAsync();
    }

    private async Task TruncateAllTablesAsync()
    {
        await using var connection = await _classState.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            DO $$ DECLARE r RECORD;
            BEGIN
                FOR r IN SELECT tablename FROM pg_tables WHERE schemaname = '{_classState.SchemaName}' AND tablename != '__EFMigrationsHistory'
                LOOP
                    EXECUTE format('TRUNCATE TABLE %I.%I RESTART IDENTITY CASCADE', '{_classState.SchemaName}', r.tablename);
                END LOOP;
            END $$;
            """;
        await command.ExecuteNonQueryAsync();
    }

    private Task InitializeServicesAsync()
    {
        var services = new ServiceCollection();

        var encryptionKey = "test-integration-key-32-chars-";
        services.AddSingleton(new EncryptionKeyProvider(encryptionKey));
        services.AddSingleton<AesEncryptionService>();

        services.AddDbContext<SamaDbContext>(options =>
        {
            options.UseNpgsql(_classState.DataSource);
            options.ConfigureWarnings(w => w.Throw(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.MultipleCollectionIncludeWarning));
        });

        services.AddLogging();

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 1;
            options.Password.RequiredUniqueChars = 1;
        })
        .AddEntityFrameworkStores<SamaDbContext>();

        // Register GlobalSettingsService for tests that need it
        services.AddSingleton<SAMA.Web.Services.GlobalSettingsService>();

        // Allow derived test classes to register additional services
        ConfigureServices(services);

        _serviceProvider = services.BuildServiceProvider();
        DbContext = _serviceProvider.GetRequiredService<SamaDbContext>();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Override this method to register additional services for a test class.
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    private async Task ApplyMigrationsAsync()
    {
        await DbContext.Database.MigrateAsync();
        await DbContext.Database.CloseConnectionAsync();
    }

    internal class ClassState
    {
        public required string SchemaName { get; init; }

        public required NpgsqlDataSource DataSource { get; init; }
    }
}
