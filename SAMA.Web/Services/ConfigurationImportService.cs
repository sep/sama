using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Data.Services;
using SAMA.Web.Models.Export;

namespace SAMA.Web.Services;

/// <summary>
/// Service for importing SAMA configuration from an encrypted export file.
/// Supports schema version migration for backward compatibility.
/// </summary>
public class ConfigurationImportService(
    SamaDbContext _dbContext,
    AesEncryptionService _encryptionService,
    CheckSchedulerService _schedulerService)
{
    /// <summary>
    /// Current schema version supported by this import service.
    /// </summary>
    public const int CurrentSchemaVersion = 2;

    /// <summary>
    /// Result of an import operation.
    /// </summary>
    public class ImportResult
    {
        public bool Success { get; set; }

        public List<string> Errors { get; set; } = [];

        public List<string> Warnings { get; set; } = [];

        public int WorkspacesCreated { get; set; }

        public int WorkspacesUpdated { get; set; }

        public int ChecksCreated { get; set; }

        public int NotificationChannelsCreated { get; set; }

        public int AlertsCreated { get; set; }

        public int ChecksScheduled { get; set; }
    }

    /// <summary>
    /// Imports configuration from an encrypted export DTO.
    /// Creates new workspaces or merges into existing ones based on name matching.
    /// </summary>
    /// <param name="export">The export data to import</param>
    /// <param name="password">Password used to decrypt the export data</param>
    /// <param name="mergeStrategy">How to handle existing workspaces with the same name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success/failure and statistics</returns>
    public virtual async Task<ImportResult> ImportAsync(
        SamaExportDto export,
        string password,
        ImportMergeStrategy mergeStrategy = ImportMergeStrategy.SkipExisting,
        CancellationToken cancellationToken = default)
    {
        var result = new ImportResult { Success = true };

        // Validate and migrate schema if needed
        if (export.SchemaVersion > CurrentSchemaVersion)
        {
            result.Success = false;
            result.Errors.Add($"Export schema version {export.SchemaVersion} is newer than supported version {CurrentSchemaVersion}. Please upgrade SAMA.");
            return result;
        }

        // Decrypt the payload
        List<WorkspaceExportDto> workspaces;
        try
        {
            var decryptedJson = _encryptionService.Decrypt(export.EncryptedData, password);

            if (export.SchemaVersion < CurrentSchemaVersion)
            {
                decryptedJson = MigrateJson(decryptedJson, export.SchemaVersion, result);
            }

            workspaces = JsonSerializer.Deserialize<List<WorkspaceExportDto>>(decryptedJson) ?? [];
        }
        catch (CryptographicException)
        {
            result.Success = false;
            result.Errors.Add("Failed to decrypt export data. The password may be incorrect.");
            return result;
        }
        catch (JsonException ex)
        {
            result.Success = false;
            result.Errors.Add($"Failed to parse decrypted export data: {ex.Message}");
            return result;
        }

        // Import workspaces
        foreach (var workspaceDto in workspaces)
        {
            await ImportWorkspaceAsync(workspaceDto, mergeStrategy, result, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    /// <summary>
    /// Migrates raw JSON from an older schema version to the current version.
    /// Operates on the JSON string before deserialization so removed/renamed properties are handled correctly.
    /// </summary>
    private static string MigrateJson(string json, int fromVersion, ImportResult result)
    {
        if (fromVersion < 2)
        {
            var workspaces = JsonNode.Parse(json)!.AsArray();
            foreach (var workspace in workspaces)
            {
                var checks = workspace?["Checks"]?.AsArray();
                if (checks == null)
                {
                    continue;
                }

                foreach (var check in checks)
                {
                    if (check is not JsonObject checkObj)
                    {
                        continue;
                    }

                    if (checkObj.Remove("IntervalSeconds", out var intervalNode))
                    {
                        checkObj["Schedule"] = intervalNode!.GetValue<int>().ToString();
                    }
                }
            }

            json = workspaces.ToJsonString();
            result.Warnings.Add("Migrated export from schema v1 to v2 (IntervalSeconds → Schedule).");
        }

        return json;
    }

    private async Task ImportWorkspaceAsync(
        WorkspaceExportDto workspaceDto,
        ImportMergeStrategy mergeStrategy,
        ImportResult result,
        CancellationToken cancellationToken)
    {
        var existingWorkspace = await _dbContext.Workspaces
            .AsSplitQuery()
            .Include(w => w.NotificationChannels)
            .Include(w => w.Checks)
                .ThenInclude(c => c.Alerts)
            .FirstOrDefaultAsync(w => w.Name == workspaceDto.Name, cancellationToken);

        if (existingWorkspace != null)
        {
            switch (mergeStrategy)
            {
                case ImportMergeStrategy.SkipExisting:
                    result.Warnings.Add($"Workspace '{workspaceDto.Name}' already exists, skipping.");
                    return;

                case ImportMergeStrategy.MergeIntoExisting:
                    await MergeIntoWorkspaceAsync(existingWorkspace, workspaceDto, result, cancellationToken);
                    result.WorkspacesUpdated++;
                    return;

                case ImportMergeStrategy.ReplaceExisting:
                    _dbContext.Workspaces.Remove(existingWorkspace);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    break;
            }
        }

        // Create new workspace
        var workspace = new Workspace
        {
            Name = workspaceDto.Name,
            Description = workspaceDto.Description,
            IsPublic = workspaceDto.IsPublic,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Workspaces.Add(workspace);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Import notification channels first (alerts reference them by ExportId)
        var exportIdToChannel = new Dictionary<string, NotificationChannel>();
        foreach (var channelDto in workspaceDto.NotificationChannels)
        {
            var channel = CreateNotificationChannel(workspace.Id, channelDto);
            _dbContext.NotificationChannels.Add(channel);
            exportIdToChannel[channelDto.ExportId] = channel;
            result.NotificationChannelsCreated++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Import checks with their alerts
        foreach (var checkDto in workspaceDto.Checks)
        {
            var check = CreateCheck(workspace.Id, checkDto);
            _dbContext.Checks.Add(check);
            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var alertDto in checkDto.Alerts)
            {
                var alert = CreateAlert(check.Id, alertDto, exportIdToChannel, result);
                _dbContext.Alerts.Add(alert);
                result.AlertsCreated++;
            }

            // Schedule enabled checks with Quartz
            if (check.Enabled)
            {
                await _schedulerService.ScheduleCheckAsync(check.Id, check.Schedule, cancellationToken);
                result.ChecksScheduled++;
            }

            result.ChecksCreated++;
        }

        result.WorkspacesCreated++;
    }

    private async Task MergeIntoWorkspaceAsync(
        Workspace workspace,
        WorkspaceExportDto workspaceDto,
        ImportResult result,
        CancellationToken cancellationToken)
    {
        // Update workspace properties
        workspace.Description = workspaceDto.Description;
        workspace.IsPublic = workspaceDto.IsPublic;
        workspace.UpdatedAt = DateTimeOffset.UtcNow;

        // Build lookup for existing channels by name and ExportId mapping
        var existingChannels = workspace.NotificationChannels.ToDictionary(c => c.Name);
        var exportIdToChannel = new Dictionary<string, NotificationChannel>();

        // Import notification channels (skip existing ones with the same name)
        foreach (var channelDto in workspaceDto.NotificationChannels)
        {
            if (existingChannels.TryGetValue(channelDto.Name, out var existingChannel))
            {
                // Channel already exists - map ExportId to existing channel for alert linking
                exportIdToChannel[channelDto.ExportId] = existingChannel;
                result.Warnings.Add($"Notification channel '{channelDto.Name}' already exists in workspace '{workspace.Name}', skipping.");
                continue;
            }

            var channel = CreateNotificationChannel(workspace.Id, channelDto);
            _dbContext.NotificationChannels.Add(channel);
            exportIdToChannel[channelDto.ExportId] = channel;
            result.NotificationChannelsCreated++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Build lookup for existing checks
        var existingChecks = workspace.Checks.ToDictionary(c => c.Name);

        // Import new checks (skip existing)
        foreach (var checkDto in workspaceDto.Checks)
        {
            if (existingChecks.ContainsKey(checkDto.Name))
            {
                result.Warnings.Add($"Check '{checkDto.Name}' already exists in workspace '{workspace.Name}', skipping.");
                continue;
            }

            var check = CreateCheck(workspace.Id, checkDto);
            _dbContext.Checks.Add(check);
            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var alertDto in checkDto.Alerts)
            {
                var alert = CreateAlert(check.Id, alertDto, exportIdToChannel, result);
                _dbContext.Alerts.Add(alert);
                result.AlertsCreated++;
            }

            // Schedule enabled checks with Quartz
            if (check.Enabled)
            {
                await _schedulerService.ScheduleCheckAsync(check.Id, check.Schedule, cancellationToken);
                result.ChecksScheduled++;
            }

            result.ChecksCreated++;
        }
    }

    private NotificationChannel CreateNotificationChannel(Guid workspaceId, NotificationChannelExportDto dto)
    {
        var channel = new NotificationChannel
        {
            WorkspaceId = workspaceId,
            Name = dto.Name,
            ChannelType = dto.ChannelType,
            ConfigurationJson = dto.Configuration,
            Enabled = dto.Enabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        foreach (var eventType in dto.EventSubscriptions)
        {
            channel.EventSubscriptions.Add(new EventSubscription
            {
                EventType = eventType,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        return channel;
    }

    private Check CreateCheck(Guid workspaceId, CheckExportDto dto)
    {
        return new Check
        {
            WorkspaceId = workspaceId,
            Name = dto.Name,
            Description = dto.Description,
            CheckType = dto.CheckType,
            ConfigurationJson = dto.Configuration,
            Schedule = dto.Schedule,
            TimeoutSeconds = dto.TimeoutSeconds,
            Enabled = dto.Enabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private Alert CreateAlert(
        Guid checkId,
        AlertExportDto dto,
        Dictionary<string, NotificationChannel> exportIdToChannel,
        ImportResult result)
    {
        var alert = new Alert
        {
            CheckId = checkId,
            Name = dto.Name,
            TriggerOnWarn = dto.TriggerOnWarn,
            TriggerOnDown = dto.TriggerOnDown,
            FailureThreshold = dto.FailureThreshold,
            SendRecoveryNotification = dto.SendRecoveryNotification,
            Enabled = dto.Enabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        foreach (var exportId in dto.NotificationChannelExportIds)
        {
            if (exportIdToChannel.TryGetValue(exportId, out var channel))
            {
                alert.NotificationChannels.Add(channel);
            }
            else
            {
                result.Warnings.Add($"Alert '{dto.Name}' references unknown notification channel export ID '{exportId}', skipping channel assignment.");
            }
        }

        return alert;
    }
}
