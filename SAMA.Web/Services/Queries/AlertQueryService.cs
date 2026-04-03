using Microsoft.EntityFrameworkCore;
using SAMA.Data;
using SAMA.Web.Models;

namespace SAMA.Web.Services.Queries;

public class AlertQueryService(SamaDbContext _dbContext)
{
    public virtual async Task<List<AlertListItemViewModel>> GetAlertsForCheckAsync(
        Guid checkId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Alerts
            .Where(a => a.CheckId == checkId)
            .Include(a => a.NotificationChannels)
            .OrderBy(a => a.Name)
            .Select(a => new AlertListItemViewModel
            {
                Id = a.Id,
                Name = a.Name,
                TriggerOnWarn = a.TriggerOnWarn,
                TriggerOnDown = a.TriggerOnDown,
                FailureThreshold = a.FailureThreshold,
                SendRecoveryNotification = a.SendRecoveryNotification,
                Enabled = a.Enabled,
                ChannelCount = a.NotificationChannels.Count,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public virtual async Task<AlertDetailsViewModel?> GetAlertDetailsAsync(
        Guid alertId,
        CancellationToken cancellationToken = default)
    {
        var alert = await _dbContext.Alerts
            .AsSplitQuery()
            .Include(a => a.Check)
                .ThenInclude(c => c.Workspace)
            .Include(a => a.NotificationChannels)
            .FirstOrDefaultAsync(a => a.Id == alertId, cancellationToken);

        if (alert == null)
        {
            return null;
        }

        var alertHistoryCount = await _dbContext.AlertHistories
            .CountAsync(ah => ah.AlertId == alertId, cancellationToken);

        return new AlertDetailsViewModel
        {
            Id = alert.Id,
            CheckId = alert.CheckId,
            CheckName = alert.Check.Name,
            WorkspaceId = alert.Check.WorkspaceId,
            WorkspaceName = alert.Check.Workspace.Name,
            Name = alert.Name,
            TriggerOnWarn = alert.TriggerOnWarn,
            TriggerOnDown = alert.TriggerOnDown,
            FailureThreshold = alert.FailureThreshold,
            SendRecoveryNotification = alert.SendRecoveryNotification,
            Enabled = alert.Enabled,
            CreatedAt = alert.CreatedAt,
            UpdatedAt = alert.UpdatedAt,
            Channels = alert.NotificationChannels
                .Select(nc => new AlertDetailsViewModel.ChannelInfo
                {
                    Id = nc.Id,
                    Name = nc.Name,
                    ChannelType = nc.ChannelType,
                    Enabled = nc.Enabled
                })
                .ToList(),
            AlertHistoryCount = alertHistoryCount
        };
    }

    public virtual async Task<AlertEditViewModel?> GetAlertForEditAsync(
        Guid alertId,
        CancellationToken cancellationToken = default)
    {
        var alert = await _dbContext.Alerts
            .AsSplitQuery()
            .Include(a => a.Check)
                .ThenInclude(c => c.Workspace)
            .Include(a => a.NotificationChannels)
            .FirstOrDefaultAsync(a => a.Id == alertId, cancellationToken);

        if (alert == null)
        {
            return null;
        }

        return new AlertEditViewModel
        {
            Id = alert.Id,
            CheckId = alert.CheckId,
            CheckName = alert.Check.Name,
            WorkspaceId = alert.Check.WorkspaceId,
            WorkspaceName = alert.Check.Workspace.Name,
            Name = alert.Name,
            TriggerOnWarn = alert.TriggerOnWarn,
            TriggerOnDown = alert.TriggerOnDown,
            FailureThreshold = alert.FailureThreshold,
            SendRecoveryNotification = alert.SendRecoveryNotification,
            Enabled = alert.Enabled,
            SelectedChannelIds = alert.NotificationChannels.Select(nc => nc.Id).ToList()
        };
    }

    public virtual async Task<List<RecentAlertViewModel>> GetRecentAlertsForWorkspaceAsync(
        Guid workspaceId,
        int maxAlerts,
        CancellationToken cancellationToken = default)
    {
        var recentTriggerEvents = await _dbContext.AlertHistories
            .AsNoTracking()
            .Where(ah => ah.Alert.Check.WorkspaceId == workspaceId && ah.Success)
            .GroupBy(ah => ah.TriggerEventId)
            .Select(g => new
            {
                RepresentativeId = g.OrderByDescending(ah => ah.SentAt).First().Id,
                MostRecentSentAt = g.Max(ah => ah.SentAt)
            })
            .OrderByDescending(x => x.MostRecentSentAt)
            .Take(maxAlerts)
            .ToListAsync(cancellationToken);

        if (recentTriggerEvents.Count == 0)
        {
            return [];
        }

        var alertHistoryIds = recentTriggerEvents.Select(x => x.RepresentativeId).ToList();

        return await _dbContext.AlertHistories
            .AsNoTracking()
            .Include(ah => ah.Alert)
                .ThenInclude(a => a.Check)
            .Where(ah => alertHistoryIds.Contains(ah.Id))
            .OrderByDescending(ah => ah.SentAt)
            .Select(ah => new RecentAlertViewModel
            {
                Id = ah.Id,
                CheckName = ah.Alert.Check.Name,
                CheckId = ah.Alert.CheckId,
                AlertName = ah.Alert.Name,
                Status = ah.Status,
                SentAt = ah.SentAt
            })
            .ToListAsync(cancellationToken);
    }
}
