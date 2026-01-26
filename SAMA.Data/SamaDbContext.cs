using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SAMA.Data.Entities;
using SAMA.Data.Services;

namespace SAMA.Data;

public class SamaDbContext(
    DbContextOptions<SamaDbContext> _options,
    EncryptionKeyProvider _keyProvider,
    AesEncryptionService _encryptionService)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(_options)
{
    // DbSets
    public DbSet<GlobalSetting> GlobalSettings => Set<GlobalSetting>();

    public DbSet<Workspace> Workspaces => Set<Workspace>();

    public DbSet<UserWorkspace> UserWorkspaces => Set<UserWorkspace>();

    public DbSet<WorkspaceGroupMapping> WorkspaceGroupMappings => Set<WorkspaceGroupMapping>();

    public DbSet<NotificationChannel> NotificationChannels => Set<NotificationChannel>();

    public DbSet<EventSubscription> EventSubscriptions => Set<EventSubscription>();

    public DbSet<Check> Checks => Set<Check>();

    public DbSet<CheckResult> CheckResults => Set<CheckResult>();

    public DbSet<Alert> Alerts => Set<Alert>();

    public DbSet<AlertHistory> AlertHistories => Set<AlertHistory>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations (indexes, relationships, constraints, etc.)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SamaDbContext).Assembly);

        // Configure UUIDv7 for all GUID primary keys
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey != null)
            {
                foreach (var property in primaryKey.Properties)
                {
                    if (property.ClrType == typeof(Guid))
                    {
                        property.SetValueGeneratorFactory((_, _) => new UuidV7ValueGenerator());
                    }
                }
            }
        }

        // Create a value comparer for Dictionary<string, JsonElement>
        var dictionaryComparer = new ValueComparer<Dictionary<string, JsonElement>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && JsonSerializer.Serialize(c1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(c2, (JsonSerializerOptions?)null)),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(c, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!
        );

        // Apply encryption converters (runtime-only, not used during migrations)
        // These convert C# objects to encrypted JSON strings in the database
        modelBuilder.Entity<NotificationChannel>()
            .Property(nc => nc.ConfigurationJson)
            .HasConversion(
                v => _encryptionService.Encrypt(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), _keyProvider.Key),
                v => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(_encryptionService.Decrypt(v, _keyProvider.Key), (JsonSerializerOptions?)null)!,
                dictionaryComparer
            );

        modelBuilder.Entity<Check>()
            .Property(c => c.ConfigurationJson)
            .HasConversion(
                v => _encryptionService.Encrypt(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), _keyProvider.Key),
                v => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(_encryptionService.Decrypt(v, _keyProvider.Key), (JsonSerializerOptions?)null)!,
                dictionaryComparer
            );
    }
}
