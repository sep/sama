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
    private string _schemaName = null!;
    private string _connectionString = null!;
    private ServiceProvider _serviceProvider = null!;
    private NpgsqlDataSource _dataSource = null!;

    protected SamaDbContext DbContext { get; private set; } = null!;

    protected IServiceProvider ServiceProvider => _serviceProvider;

    [TestInitialize]
    public virtual async Task InitializeTestAsync()
    {
        _schemaName = $"test_{GetType().Name.ToLowerInvariant()}_{Guid.CreateVersion7():N}";
        _connectionString = GetConnectionString();

        // Create a dedicated data source for this test class with limited pooling
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_connectionString);
        dataSourceBuilder.ConnectionStringBuilder.MaxPoolSize = 3;
        dataSourceBuilder.ConnectionStringBuilder.MinPoolSize = 0;
        dataSourceBuilder.ConnectionStringBuilder.ConnectionLifetime = 30;
        dataSourceBuilder.ConnectionStringBuilder.Timeout = 30;
        _dataSource = dataSourceBuilder.Build();

        await CreateSchemaAsync();
        await InitializeServicesAsync();
        await ApplyMigrationsAsync();
    }

    [TestCleanup]
    public virtual async Task CleanupTestAsync()
    {
        try
        {
            // Explicitly close all DbContext connections
            if (DbContext != null)
            {
                await DbContext.Database.CloseConnectionAsync();
                await DbContext.DisposeAsync();
            }

            // Dispose service provider (releases pooled connections)
            if (_serviceProvider != null)
            {
                await _serviceProvider.DisposeAsync();
            }

            // Drop schema
            await DropSchemaAsync();
        }
        finally
        {
            // Always dispose data source to release all pooled connections
            if (_dataSource != null)
            {
                await _dataSource.DisposeAsync();
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

    private string GetConnectionString()
    {
        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "samadb";
        var username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "sama";
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "sama-dev-pw";

        return $"Host={host};Port={port};Database={database};Username={username};Password={password};Search Path={_schemaName}";
    }

    private async Task CreateSchemaAsync()
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE SCHEMA {_schemaName}";
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropSchemaAsync()
    {
        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP SCHEMA IF EXISTS {_schemaName} CASCADE";
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            // Log but don't throw - we're in cleanup
            System.Diagnostics.Debug.WriteLine($"Failed to drop schema {_schemaName}: {ex.Message}");
        }
    }

    private Task InitializeServicesAsync()
    {
        var services = new ServiceCollection();

        var encryptionKey = "test-integration-key-32-chars-";
        services.AddSingleton(new EncryptionKeyProvider(encryptionKey));
        services.AddSingleton<AesEncryptionService>();

        services.AddDbContext<SamaDbContext>(options =>
        {
            options.UseNpgsql(_dataSource);
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
}
