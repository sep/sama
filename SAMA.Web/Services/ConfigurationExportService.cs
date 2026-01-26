using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Data.Services;
using SAMA.Web.Models.Export;

namespace SAMA.Web.Services;

/// <summary>
/// Service for exporting SAMA configuration to a portable, encrypted format.
/// Exports workspaces, checks, notification channels, alerts.
/// </summary>
public class ConfigurationExportService(
    SamaDbContext _dbContext,
    ApplicationStateService _appStateService,
    AesEncryptionService _encryptionService)
{
    /// <summary>
    /// Exports all configuration from the database, encrypted with the provided password.
    /// </summary>
    /// <param name="password">Password used to encrypt the export data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Complete export DTO with encrypted workspaces</returns>
    public virtual async Task<SamaExportDto> ExportAllAsync(string password, CancellationToken cancellationToken = default)
    {
        var workspaces = await _dbContext.Workspaces
            .AsSplitQuery()
            .AsNoTracking()
            .Include(w => w.Checks)
                .ThenInclude(c => c.Alerts)
                    .ThenInclude(a => a.NotificationChannels)
            .Include(w => w.NotificationChannels)
                .ThenInclude(nc => nc.EventSubscriptions)
            .ToListAsync(cancellationToken);

        var workspaceDtos = workspaces.Select(MapWorkspace).ToList();
        var payloadJson = JsonSerializer.Serialize(workspaceDtos);
        var encryptedData = _encryptionService.Encrypt(payloadJson, password);

        return new SamaExportDto
        {
            SchemaVersion = 1,
            ExportedFromVersion = _appStateService.Version,
            ExportedAt = DateTimeOffset.UtcNow,
            EncryptedData = encryptedData
        };
    }

    private static WorkspaceExportDto MapWorkspace(Workspace workspace)
    {
        // Build a mapping from channel database ID to export ID
        var channelIdToExportId = new Dictionary<Guid, string>();
        var channelIndex = 1;
        foreach (var channel in workspace.NotificationChannels)
        {
            channelIdToExportId[channel.Id] = $"channel_{channelIndex++}";
        }

        return new WorkspaceExportDto
        {
            Name = workspace.Name,
            Description = workspace.Description,
            IsPublic = workspace.IsPublic,
            Checks = workspace.Checks.Select(c => MapCheck(c, channelIdToExportId)).ToList(),
            NotificationChannels = workspace.NotificationChannels.Select(nc => MapNotificationChannel(nc, channelIdToExportId)).ToList()
        };
    }

    private static CheckExportDto MapCheck(Check check, Dictionary<Guid, string> channelIdToExportId)
    {
        return new CheckExportDto
        {
            Name = check.Name,
            Description = check.Description,
            CheckType = check.CheckType,
            Configuration = check.ConfigurationJson,
            IntervalSeconds = check.IntervalSeconds,
            TimeoutSeconds = check.TimeoutSeconds,
            Enabled = check.Enabled,
            Alerts = check.Alerts.Select(a => MapAlert(a, channelIdToExportId)).ToList()
        };
    }

    private static AlertExportDto MapAlert(Alert alert, Dictionary<Guid, string> channelIdToExportId)
    {
        return new AlertExportDto
        {
            Name = alert.Name,
            TriggerOnWarn = alert.TriggerOnWarn,
            TriggerOnDown = alert.TriggerOnDown,
            FailureThreshold = alert.FailureThreshold,
            SendRecoveryNotification = alert.SendRecoveryNotification,
            Enabled = alert.Enabled,
            NotificationChannelExportIds = alert.NotificationChannels
                .Where(nc => channelIdToExportId.ContainsKey(nc.Id))
                .Select(nc => channelIdToExportId[nc.Id])
                .ToList()
        };
    }

    private static NotificationChannelExportDto MapNotificationChannel(NotificationChannel channel, Dictionary<Guid, string> channelIdToExportId)
    {
        return new NotificationChannelExportDto
        {
            ExportId = channelIdToExportId[channel.Id],
            Name = channel.Name,
            ChannelType = channel.ChannelType,
            Configuration = channel.ConfigurationJson,
            Enabled = channel.Enabled,
            EventSubscriptions = channel.EventSubscriptions.Select(s => s.EventType).ToList()
        };
    }
}
