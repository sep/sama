using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Web.Constants;
using SAMA.Web.Models;

namespace SAMA.Web.Services.Commands;

public class CheckCommandService(
    SamaDbContext _samaDbContext,
    CheckSchedulerService _checkSchedulerService,
    EventSubscriptionService _eventSubscriptionService,
    CheckChangeDetectionService _changeDetectionService,
    DashboardCacheService _dashboardCacheService,
    ILogger<CheckCommandService> _logger)
{
    public virtual async Task<Guid> CreateCheckAsync(
        Guid workspaceId,
        string name,
        string? description,
        string checkType,
        string schedule,
        int timeoutSeconds,
        Dictionary<string, JsonElement> configuration,
        bool enabled,
        string performedBy,
        CancellationToken cancellationToken = default)
    {
        var check = new Check
        {
            WorkspaceId = workspaceId,
            Name = name,
            Description = description,
            CheckType = checkType,
            Schedule = schedule,
            TimeoutSeconds = timeoutSeconds,
            ConfigurationJson = configuration,
            Enabled = enabled
        };

        _samaDbContext.Checks.Add(check);
        await _samaDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {User} created check {CheckName} (Type: {CheckType}, WorkspaceId: {WorkspaceId})",
            performedBy,
            check.Name,
            check.CheckType,
            check.WorkspaceId);

        // Create default alert
        var defaultAlert = new Alert
        {
            CheckId = check.Id,
            Name = $"{check.Name} - Default Alert",
            TriggerOnWarn = true,
            TriggerOnDown = true,
            FailureThreshold = 1,
            SendRecoveryNotification = true,
            Enabled = true,
            NotificationChannels = []
        };

        _samaDbContext.Alerts.Add(defaultAlert);
        await _samaDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Auto-created default alert for check {CheckId}",
            check.Id);

        if (check.Enabled)
        {
            await _checkSchedulerService.ScheduleCheckAsync(check.Id, check.Schedule);
            _logger.LogInformation("Scheduled check {CheckId} for execution", check.Id);
        }

        _dashboardCacheService.InvalidateAllForWorkspace(workspaceId);

        // Trigger lifecycle event for check creation
        var workspace = await _samaDbContext.Workspaces.FindAsync([workspaceId], cancellationToken);
        var lifecycleContext = new LifecycleEventContext
        {
            EventType = EventTypes.CheckCreated,
            CheckId = check.Id,
            CheckName = check.Name,
            CheckType = check.CheckType,
            WorkspaceName = workspace?.Name ?? "Unknown",
            Timestamp = DateTimeOffset.UtcNow,
            PerformedBy = performedBy
        };

        await _eventSubscriptionService.TriggerLifecycleEventAsync(
            workspaceId,
            lifecycleContext,
            cancellationToken);

        return check.Id;
    }

    public virtual async Task<bool> UpdateCheckAsync(
        Guid checkId,
        string name,
        string? description,
        string checkType,
        string schedule,
        int timeoutSeconds,
        Dictionary<string, JsonElement> configuration,
        bool enabled,
        string performedBy,
        CancellationToken cancellationToken = default)
    {
        var checkToUpdate = await _samaDbContext.Checks
            .Include(c => c.Workspace)
            .FirstOrDefaultAsync(c => c.Id == checkId, cancellationToken);
        if (checkToUpdate == null)
        {
            return false;
        }

        var wasEnabled = checkToUpdate.Enabled;

        var configurationChanges = _changeDetectionService.DetectChanges(
            checkToUpdate,
            name,
            description,
            checkType,
            schedule,
            timeoutSeconds,
            configuration,
            enabled);

        checkToUpdate.Name = name;
        checkToUpdate.Description = description;
        checkToUpdate.CheckType = checkType;
        checkToUpdate.Schedule = schedule;
        checkToUpdate.TimeoutSeconds = timeoutSeconds;
        checkToUpdate.ConfigurationJson = configuration;
        checkToUpdate.Enabled = enabled;
        checkToUpdate.UpdatedAt = DateTimeOffset.UtcNow;

        await _samaDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {User} updated check {CheckName} (Id: {CheckId})",
            performedBy,
            checkToUpdate.Name,
            checkToUpdate.Id);

        var lifecycleContext = new LifecycleEventContext
        {
            EventType = EventTypes.CheckUpdated,
            CheckId = checkToUpdate.Id,
            CheckName = checkToUpdate.Name,
            CheckType = checkToUpdate.CheckType,
            WorkspaceName = checkToUpdate.Workspace.Name,
            Timestamp = DateTimeOffset.UtcNow,
            PerformedBy = performedBy,
            ConfigurationChanges = configurationChanges
        };

        await _eventSubscriptionService.TriggerLifecycleEventAsync(
            checkToUpdate.WorkspaceId,
            lifecycleContext,
            cancellationToken);

        _dashboardCacheService.InvalidateAllForWorkspace(checkToUpdate.WorkspaceId);

        if (enabled)
        {
            await _checkSchedulerService.ScheduleCheckAsync(checkToUpdate.Id, checkToUpdate.Schedule);
            _logger.LogInformation(
                "Scheduled check {CheckId} with schedule {Schedule}",
                checkToUpdate.Id,
                checkToUpdate.Schedule);
        }
        else if (wasEnabled)
        {
            await _checkSchedulerService.UnscheduleCheckAsync(checkToUpdate.Id);
            _logger.LogInformation("Unscheduled check {CheckId}", checkToUpdate.Id);
        }

        return true;
    }

    public virtual async Task<bool> DeleteCheckAsync(
        Guid checkId,
        string performedBy,
        CancellationToken cancellationToken = default)
    {
        var check = await _samaDbContext.Checks
            .Include(c => c.Workspace)
            .FirstOrDefaultAsync(c => c.Id == checkId, cancellationToken);

        if (check == null)
        {
            return false;
        }

        var checkName = check.Name;
        var checkType = check.CheckType;
        var workspaceId = check.WorkspaceId;
        var workspaceName = check.Workspace.Name;

        if (check.Enabled)
        {
            await _checkSchedulerService.UnscheduleCheckAsync(check.Id);
            _logger.LogInformation("Unscheduled check {CheckId} before deletion", check.Id);
        }

        _samaDbContext.Checks.Remove(check);
        await _samaDbContext.SaveChangesAsync(cancellationToken);

        _dashboardCacheService.InvalidateAllForWorkspace(workspaceId);

        _logger.LogInformation(
            "User {User} deleted check {CheckName} (Id: {CheckId})",
            performedBy,
            checkName,
            checkId);

        // Trigger lifecycle event for check deletion
        var lifecycleContext = new LifecycleEventContext
        {
            EventType = EventTypes.CheckDeleted,
            CheckId = checkId,
            CheckName = checkName,
            CheckType = checkType,
            WorkspaceName = workspaceName,
            Timestamp = DateTimeOffset.UtcNow,
            PerformedBy = performedBy
        };

        await _eventSubscriptionService.TriggerLifecycleEventAsync(
            workspaceId,
            lifecycleContext,
            cancellationToken);

        return true;
    }
}
