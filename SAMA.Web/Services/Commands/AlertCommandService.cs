using Microsoft.EntityFrameworkCore;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Web.Constants;
using SAMA.Web.Models;

namespace SAMA.Web.Services.Commands;

public class AlertCommandService(
    SamaDbContext _dbContext,
    CheckSchedulerService _schedulerService,
    EventSubscriptionService _eventSubscriptionService,
    AlertChangeDetectionService _alertChangeDetectionService,
    DashboardCacheService _dashboardCacheService,
    ILogger<AlertCommandService> _logger)
{
    public virtual async Task<CreateUpdateAlertResultViewModel> CreateAlertAsync(
        Guid checkId,
        string name,
        bool triggerOnWarn,
        bool triggerOnDown,
        int failureThreshold,
        bool sendRecoveryNotification,
        bool enabled,
        List<Guid> selectedChannelIds,
        string performedBy,
        CancellationToken cancellationToken = default)
    {
        var check = await _dbContext.Checks
            .Include(c => c.Workspace)
            .FirstOrDefaultAsync(c => c.Id == checkId, cancellationToken);

        if (check == null)
        {
            return new CreateUpdateAlertResultViewModel
            {
                Success = false,
                ErrorMessage = "Check not found"
            };
        }

        var selectedChannels = await _dbContext.NotificationChannels
            .Where(nc => selectedChannelIds.Contains(nc.Id))
            .ToListAsync(cancellationToken);

        var alert = new Alert
        {
            CheckId = checkId,
            Name = name,
            TriggerOnWarn = triggerOnWarn,
            TriggerOnDown = triggerOnDown,
            FailureThreshold = failureThreshold,
            SendRecoveryNotification = sendRecoveryNotification,
            Enabled = enabled,
            NotificationChannels = selectedChannels
        };

        _dbContext.Alerts.Add(alert);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dashboardCacheService.InvalidateWorkspace(check.WorkspaceId);

        _logger.LogInformation(
            "User {User} created alert {AlertName} for check {CheckId} with {ChannelCount} channels",
            performedBy,
            alert.Name,
            alert.CheckId,
            selectedChannels.Count);

        var configurationChanges = _alertChangeDetectionService.BuildCreationInfo(
            alert.Name,
            triggerOnWarn,
            triggerOnDown,
            failureThreshold,
            sendRecoveryNotification,
            enabled,
            selectedChannelIds);

        var lifecycleContext = new LifecycleEventContext
        {
            EventType = EventTypes.CheckUpdated,
            CheckId = check.Id,
            CheckName = check.Name,
            CheckType = check.CheckType,
            WorkspaceName = check.Workspace.Name,
            Timestamp = DateTimeOffset.UtcNow,
            PerformedBy = performedBy,
            ConfigurationChanges = configurationChanges
        };

        await _eventSubscriptionService.TriggerLifecycleEventAsync(
            check.WorkspaceId,
            lifecycleContext,
            cancellationToken);

        var allChannelsCount = selectedChannels.Count == 0
            ? await _dbContext.NotificationChannels.CountAsync(
                nc => nc.WorkspaceId == check.WorkspaceId && nc.Enabled,
                cancellationToken)
            : 0;

        var shouldTriggerCheck = alert.Enabled && (selectedChannels.Count > 0 || allChannelsCount > 0);

        if (shouldTriggerCheck)
        {
            await _schedulerService.TriggerImmediateCheckAsync(checkId);
        }

        return new CreateUpdateAlertResultViewModel
        {
            Success = true,
            AlertId = alert.Id,
            ShouldTriggerCheck = shouldTriggerCheck,
            ChannelCount = selectedChannels.Count,
            AllChannelsCount = allChannelsCount
        };
    }

    public virtual async Task<CreateUpdateAlertResultViewModel> UpdateAlertAsync(
        Guid alertId,
        string name,
        bool triggerOnWarn,
        bool triggerOnDown,
        int failureThreshold,
        bool sendRecoveryNotification,
        bool enabled,
        List<Guid> selectedChannelIds,
        string performedBy,
        CancellationToken cancellationToken = default)
    {
        var alertToUpdate = await _dbContext.Alerts
            .AsSplitQuery()
            .Include(a => a.NotificationChannels)
            .Include(a => a.Check)
                .ThenInclude(c => c.Workspace)
            .FirstOrDefaultAsync(a => a.Id == alertId, cancellationToken);
        if (alertToUpdate == null)
        {
            return new CreateUpdateAlertResultViewModel
            {
                AlertId = alertId,
                Success = false,
                ErrorMessage = "Alert not found"
            };
        }

        var wasEnabled = alertToUpdate.Enabled;
        var hadChannels = alertToUpdate.NotificationChannels.Count > 0;

        var configurationChanges = _alertChangeDetectionService.DetectChanges(
            alertToUpdate,
            name,
            triggerOnWarn,
            triggerOnDown,
            failureThreshold,
            sendRecoveryNotification,
            enabled,
            selectedChannelIds);

        var selectedChannels = await _dbContext.NotificationChannels
            .Where(nc => selectedChannelIds.Contains(nc.Id))
            .ToListAsync(cancellationToken);

        alertToUpdate.Name = name;
        alertToUpdate.TriggerOnWarn = triggerOnWarn;
        alertToUpdate.TriggerOnDown = triggerOnDown;
        alertToUpdate.FailureThreshold = failureThreshold;
        alertToUpdate.SendRecoveryNotification = sendRecoveryNotification;
        alertToUpdate.Enabled = enabled;
        alertToUpdate.UpdatedAt = DateTimeOffset.UtcNow;

        alertToUpdate.NotificationChannels.Clear();
        foreach (var channel in selectedChannels)
        {
            alertToUpdate.NotificationChannels.Add(channel);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _dashboardCacheService.InvalidateWorkspace(alertToUpdate.Check.WorkspaceId);

        _logger.LogInformation(
            "User {User} updated alert {AlertName} (Id: {AlertId}) with {ChannelCount} channels",
            performedBy,
            alertToUpdate.Name,
            alertToUpdate.Id,
            selectedChannels.Count);

        var lifecycleContext = new LifecycleEventContext
        {
            EventType = EventTypes.CheckUpdated,
            CheckId = alertToUpdate.Check.Id,
            CheckName = alertToUpdate.Check.Name,
            CheckType = alertToUpdate.Check.CheckType,
            WorkspaceName = alertToUpdate.Check.Workspace.Name,
            Timestamp = DateTimeOffset.UtcNow,
            PerformedBy = performedBy,
            ConfigurationChanges = configurationChanges
        };

        await _eventSubscriptionService.TriggerLifecycleEventAsync(
            alertToUpdate.Check.WorkspaceId,
            lifecycleContext,
            cancellationToken);

        var allChannelsCount = selectedChannels.Count == 0
            ? await _dbContext.NotificationChannels.CountAsync(
                nc => nc.WorkspaceId == alertToUpdate.Check.WorkspaceId && nc.Enabled,
                cancellationToken)
            : 0;

        var hasChannelsNow = selectedChannels.Count > 0 || allChannelsCount > 0;
        var shouldTriggerCheck = alertToUpdate.Enabled && hasChannelsNow && (!wasEnabled || !hadChannels);

        if (shouldTriggerCheck)
        {
            await _schedulerService.TriggerImmediateCheckAsync(alertToUpdate.CheckId);
        }

        return new CreateUpdateAlertResultViewModel
        {
            AlertId = alertId,
            Success = true,
            ShouldTriggerCheck = shouldTriggerCheck,
            ChannelCount = selectedChannels.Count,
            AllChannelsCount = allChannelsCount
        };
    }

    public virtual async Task<bool> DeleteAlertAsync(
        Guid alertId,
        string performedBy,
        CancellationToken cancellationToken = default)
    {
        var alert = await _dbContext.Alerts
            .Include(a => a.Check)
                .ThenInclude(c => c.Workspace)
            .FirstOrDefaultAsync(a => a.Id == alertId, cancellationToken);

        if (alert == null)
        {
            return false;
        }

        var alertName = alert.Name;
        var checkId = alert.CheckId;

        _dbContext.Alerts.Remove(alert);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dashboardCacheService.InvalidateWorkspace(alert.Check.WorkspaceId);

        _logger.LogInformation(
            "User {User} deleted alert {AlertName} (Id: {AlertId})",
            performedBy,
            alertName,
            alertId);

        var configurationChanges = _alertChangeDetectionService.BuildDeletionInfo(alertName);

        var lifecycleContext = new LifecycleEventContext
        {
            EventType = EventTypes.CheckUpdated,
            CheckId = alert.Check.Id,
            CheckName = alert.Check.Name,
            CheckType = alert.Check.CheckType,
            WorkspaceName = alert.Check.Workspace.Name,
            Timestamp = DateTimeOffset.UtcNow,
            PerformedBy = performedBy,
            ConfigurationChanges = configurationChanges
        };

        await _eventSubscriptionService.TriggerLifecycleEventAsync(
            alert.Check.WorkspaceId,
            lifecycleContext,
            cancellationToken);

        return true;
    }
}
