using System.Reflection;
using DnsClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Quartz;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Data.Services;
using SAMA.Shared.Checks;
using SAMA.Shared.Factories;
using SAMA.Shared.Wrappers;
using SAMA.Web.Constants;
using SAMA.Web.HealthChecks;
using SAMA.Web.Middleware;
using SAMA.Web.Services;
using SAMA.Web.Services.Commands;
using SAMA.Web.Services.NotificationChannels;
using SAMA.Web.Services.Queries;
using SAMA.Web.Wrappers;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Initializing...");

var builder = WebApplication.CreateBuilder(args);

var inMemoryLogSink = new InMemoryLogSink();

// Add services to the container.
builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}\n               {Message:lj}{NewLine}{Exception}")
        .WriteTo.Sink(inMemoryLogSink));

if (builder.Environment.IsDevelopment())
{
    // NOTE: this prevents modern hot reload functionality from working reliably
    builder.Services.AddRazorPages().AddRazorRuntimeCompilation();
}
else
{
    builder.Services.AddRazorPages();
}
builder.Services.AddControllers(config =>
{
    // Require authenticated admin users by default
    var policy = new AuthorizationPolicyBuilder()
                     .RequireAuthenticatedUser()
                     .RequireRole(AuthConstants.AdminRole)
                     .Build();
    config.Filters.Add(new AuthorizeFilter(policy));
});

// Configure database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<SamaDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.ConfigureWarnings(w => w.Throw(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.MultipleCollectionIncludeWarning));
});

// Configure Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;

    // Password requirements - encourage passphrases over complexity
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 14;
    options.Password.RequiredUniqueChars = 4;
})
.AddEntityFrameworkStores<SamaDbContext>();

// Configure application cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// Configure ASP.NET Data Protection
// When running in Docker, use a volume-mounted directory for keys
// Otherwise, use the default ASP.NET location
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("SAMA");

string keysPath;
if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != null)
{
    // Docker: use volume-mounted directory
    keysPath = "/app/keys";
    if (!Directory.Exists(keysPath))
    {
        throw new InvalidOperationException($"Keys directory does not exist: {keysPath}. Ensure the volume is properly mounted in docker-compose.yml");
    }

    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}
else
{
    // Non-Docker: use system data directory
    keysPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SAMA", "keys");
    Directory.CreateDirectory(keysPath);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

// Configure encryption service with optional key from configuration
var encryptionKey = EF.IsDesignTime ? "test-key-design-time-only" : builder.Configuration["Encryption:Key"];

builder.Services.AddSingleton(sp =>
{
    var key = encryptionKey;

    if (string.IsNullOrWhiteSpace(key))
    {
        // Generate or retrieve a persistent random encryption key protected by Data Protection
        var dataProtectionProvider = sp.GetRequiredService<IDataProtectionProvider>();
        var protector = dataProtectionProvider.CreateProtector("SAMA.EncryptionKey");
        var encryptionKeyPath = Path.Combine(keysPath, "encryption.key");

        if (File.Exists(encryptionKeyPath))
        {
            // Load and unprotect existing key
            var protectedKey = File.ReadAllText(encryptionKeyPath);
            key = protector.Unprotect(protectedKey);
            Log.Information("Using auto-generated encryption key from {KeyPath}", encryptionKeyPath);
        }
        else
        {
            // Generate new random key using CSPRNG
            var keyBytes = new byte[32]; // 256 bits
            System.Security.Cryptography.RandomNumberGenerator.Fill(keyBytes);
            key = Convert.ToBase64String(keyBytes);

            // Protect and persist key to disk
            var protectedKey = protector.Protect(key);
            Directory.CreateDirectory(keysPath);
            File.WriteAllText(encryptionKeyPath, protectedKey);
            Log.Information("Generated new encryption key and saved to {KeyPath}", encryptionKeyPath);
        }
    }

    return new EncryptionKeyProvider(key);
});

// Configure default HttpClient
builder.Services.AddHttpClient();

// Register custom HttpClient factory for check executors
builder.Services.AddSingleton<ConfigurableHttpClientFactory>();

// Register wrapper factories and related services for check executors
builder.Services.AddSingleton<TcpClientFactory>();
builder.Services.AddSingleton<SslStreamFactory>();
builder.Services.AddSingleton<PingFactory>();
builder.Services.AddSingleton<ProcessFactory>();
builder.Services.AddSingleton<CustomTlsValidator>();

// Register notification channel factories
builder.Services.AddSingleton<SmtpClientFactory>();

// Register DNS lookup client for DNS check executor
builder.Services.AddTransient<ILookupClient>((_) =>
{
    var lookupClientOptions = new LookupClientOptions
    {
        Timeout = TimeSpan.FromSeconds(5),
        UseCache = false,
    };
    return new LookupClient(lookupClientOptions);
});

// Register all check executors with keyed services
var executorTypes = typeof(ICheckExecutor).Assembly
    .GetTypes()
    .Where(t => t.IsClass && !t.IsAbstract && typeof(ICheckExecutor).IsAssignableFrom(t));
foreach (var executorType in executorTypes)
{
    var checkTypeAttr = executorType.GetCustomAttribute<CheckTypeAttribute>();
    if (checkTypeAttr != null)
    {
        builder.Services.AddKeyedTransient(typeof(ICheckExecutor), checkTypeAttr.CheckType, executorType);
    }
}

// Register application services
builder.Services.AddSingleton(inMemoryLogSink);
builder.Services.AddSingleton<ApplicationStateService>();
builder.Services.AddSingleton<AesEncryptionService>();
builder.Services.AddSingleton<GlobalSettingsService>();
builder.Services.AddSingleton<MarkdownService>();
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddScoped<SetupService>();
builder.Services.AddScoped<NotificationChannelConfigurationService>();
builder.Services.AddScoped<CheckConfigurationService>();
builder.Services.AddScoped<CheckChangeDetectionService>();
builder.Services.AddScoped<AlertChangeDetectionService>();
builder.Services.AddScoped<SensitiveDataMaskingService>();
builder.Services.AddScoped<CheckSchedulerService>();
builder.Services.AddScoped<AlertHandlerService>();
builder.Services.AddScoped<EventSubscriptionService>();
builder.Services.AddScoped<WorkspaceAuthorizationService>();
builder.Services.AddScoped<UserPreferencesService>();
builder.Services.AddScoped<ConfigurationExportService>();
builder.Services.AddScoped<ConfigurationImportService>();

// Register CQRS-lite query services
builder.Services.AddScoped<WorkspaceQueryService>();
builder.Services.AddScoped<CheckQueryService>();
builder.Services.AddScoped<AlertQueryService>();
builder.Services.AddScoped<ChannelQueryService>();
builder.Services.AddScoped<EventSubscriptionQueryService>();
builder.Services.AddScoped<UserQueryService>();

// Register CQRS-lite command services
builder.Services.AddScoped<CheckCommandService>();
builder.Services.AddScoped<AlertCommandService>();
builder.Services.AddScoped<ChannelCommandService>();
builder.Services.AddScoped<WorkspaceCommandService>();
builder.Services.AddScoped<EventSubscriptionCommandService>();
builder.Services.AddScoped<UserCommandService>();

// Register all notification channel handlers with keyed services
var channelHandlerTypes = typeof(INotificationChannelHandler).Assembly
    .GetTypes()
    .Where(t => t.IsClass && !t.IsAbstract && typeof(INotificationChannelHandler).IsAssignableFrom(t));
foreach (var handlerType in channelHandlerTypes)
{
    var channelTypeAttr = handlerType.GetCustomAttribute<ChannelTypeAttribute>();
    if (channelTypeAttr != null)
    {
        builder.Services.AddKeyedTransient(typeof(INotificationChannelHandler), channelTypeAttr.ChannelType, handlerType);
    }
}

// Configure Quartz.NET
builder.Services.AddQuartz(q =>
{
    q.UseInMemoryStore();

    // Register data cleanup job (runs daily at 5 AM local time)
    q.AddJob<DataCleanupJob>(opts => opts
        .WithIdentity("data-cleanup-job")
        .StoreDurably());

    q.AddTrigger(opts => opts
        .ForJob("data-cleanup-job")
        .WithIdentity("data-cleanup-trigger")
        .WithCronSchedule("0 0 5 * * ?")
        .StartAt(DateBuilder.FutureDate(1, IntervalUnit.Minute)));
});

builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
});

// Configure health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SamaDbContext>("database")
    .AddCheck<QuartzSchedulerHealthCheck>("scheduler");

// Register parent process monitor for system tests (gracefully shuts down if parent is killed)
builder.Services.AddHostedService<ParentProcessMonitorService>();

var app = builder.Build();

app.UseSerilogRequestLogging();

// Apply migrations and seed database on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var httpClientFactory = services.GetRequiredService<ConfigurableHttpClientFactory>();

    // Initialize ApplicationStateService to capture accurate startup time
    _ = services.GetRequiredService<ApplicationStateService>();

    try
    {
        logger.LogInformation("Applying database migrations...");
        var context = services.GetRequiredService<SamaDbContext>();
        await context.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully");

        logger.LogInformation("Seeding database...");
        var seeder = services.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
        logger.LogInformation("Database seeding completed successfully");

        // Warm up HttpClient
        logger.LogInformation("Warming up HTTP client connections...");
        using var httpClient = httpClientFactory.CreateClient(true, false, 1);
        try
        {
            // Make warmup requests to trigger DNS, connection pooling, etc.
            await httpClient.GetAsync("https://127.0.0.1:65535", HttpCompletionOption.ResponseHeadersRead);
        }
        catch (Exception)
        {
            // Ignore any errors during warmup
        }

        logger.LogInformation("Check initialization will begin shortly");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "An error occurred during application startup. Application will be aborted.");
        throw;
    }
}

_ = Task.Run(async () =>
{
    await Task.Delay(TimeSpan.FromSeconds(10));

    using var delayedScope = app.Services.CreateScope();
    var delayedLogger = delayedScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var delayedDbContext = delayedScope.ServiceProvider.GetRequiredService<SamaDbContext>();
    var schedulerService = delayedScope.ServiceProvider.GetRequiredService<CheckSchedulerService>();

    delayedLogger.LogInformation("Scheduling enabled checks...");

    var checks = await delayedDbContext.Checks
        .AsNoTracking()
        .Where(c => c.Enabled)
        .Select(c => new { c.Id, c.IntervalSeconds })
        .ToListAsync();

    delayedLogger.LogInformation("Scheduling {CheckCount} check(s)", checks.Count);

    foreach (var check in checks)
    {
        await schedulerService.ScheduleCheckAsync(check.Id, check.IntervalSeconds);
    }

    delayedLogger.LogInformation("Check scheduling completed successfully");
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

app.UseRouting();

// Add HTMX redirect middleware before authentication
app.UseMiddleware<HtmxRedirectMiddleware>();

// Add setup redirect middleware before authentication
app.UseMiddleware<SetupMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var appState = context.RequestServices.GetRequiredService<ApplicationStateService>();

        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            version = appState.Version,
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.ToString(),
            entries = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.ToString(),
                data = e.Value.Data.Count > 0 ? e.Value.Data : null,
                exception = e.Value.Exception?.Message
            })
        });

        await context.Response.WriteAsync(result);
    }
}).AllowAnonymous();

app.Run();
