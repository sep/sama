using Microsoft.EntityFrameworkCore;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Shared.Constants;
using SAMA.Web.Models;

namespace SAMA.Web.Services.Queries;

public class WorkspaceQueryService(SamaDbContext _dbContext, ApplicationStateService _appStateService)
{
    public virtual async Task<Workspace?> GetWorkspaceByIdAsync(Guid workspaceId)
    {
        return await _dbContext.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workspaceId);
    }

    public virtual async Task<List<WorkspaceDetailsViewModel>> GetWorkspacesAsync(
        List<Guid>? workspaceIds = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Workspaces.AsQueryable();

        if (workspaceIds != null)
        {
            query = query.Where(w => workspaceIds.Contains(w.Id));
        }

        var workspaces = await query
            .AsNoTracking()
            .OrderBy(w => w.Name)
            .Select(w => new WorkspaceDetailsViewModel
            {
                Id = w.Id,
                Name = w.Name,
                Description = w.Description,
                DashboardMessage = w.DashboardMessage,
                IsPublic = w.IsPublic,
                CreatedAt = w.CreatedAt,
                UpdatedAt = w.UpdatedAt,
                CheckCount = w.Checks.Count,
                NotificationChannelCount = w.NotificationChannels.Count,
                UserCount = w.UserWorkspaces.Count
            })
            .ToListAsync(cancellationToken);

        await PopulateStatusCounts(workspaces, cancellationToken);
        return workspaces;
    }

    public virtual async Task<string?> GetDashboardMessageAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Workspaces
            .AsNoTracking()
            .Where(w => w.Id == workspaceId)
            .Select(w => w.DashboardMessage)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<WorkspaceDetailsViewModel?> GetWorkspaceDetailsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _dbContext.Workspaces
            .AsNoTracking()
            .Where(w => w.Id == workspaceId)
            .Select(w => new WorkspaceDetailsViewModel
            {
                Id = w.Id,
                Name = w.Name,
                Description = w.Description,
                DashboardMessage = w.DashboardMessage,
                IsPublic = w.IsPublic,
                CreatedAt = w.CreatedAt,
                UpdatedAt = w.UpdatedAt,
                CheckCount = w.Checks.Count,
                NotificationChannelCount = w.NotificationChannels.Count,
                UserCount = w.UserWorkspaces.Count
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (workspace != null)
        {
            await PopulateStatusCounts([workspace], cancellationToken);
        }
        return workspace;
    }

    private async Task PopulateStatusCounts(
        List<WorkspaceDetailsViewModel> workspaces,
        CancellationToken cancellationToken)
    {
        if (workspaces.Count == 0)
        {
            return;
        }

        var startupTime = _appStateService.StartupTime;
        var wsIds = workspaces.Select(w => w.Id).ToList();

        var checkStatuses = await _dbContext.Checks
            .AsNoTracking()
            .Where(c => wsIds.Contains(c.WorkspaceId) && c.Enabled)
            .Select(c => new
            {
                c.WorkspaceId,
                c.UpdatedAt,
                LatestResult = c.CheckResults
                    .OrderByDescending(cr => cr.CheckedAt)
                    .Select(cr => new { cr.Status, CheckedAt = (DateTimeOffset?)cr.CheckedAt })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        foreach (var ws in workspaces)
        {
            foreach (var check in checkStatuses.Where(c => c.WorkspaceId == ws.Id))
            {
                var status = check.LatestResult?.Status;
                var lastCheckedAt = check.LatestResult?.CheckedAt;
                if (!lastCheckedAt.HasValue ||
                    lastCheckedAt.Value < startupTime ||
                    check.UpdatedAt > lastCheckedAt.Value)
                {
                    status = null;
                }

                switch (status)
                {
                    case CheckStatuses.Up:
                        ws.UpCount++;
                        break;
                    case CheckStatuses.Warn:
                        ws.WarnCount++;
                        break;
                    case CheckStatuses.Down:
                        ws.DownCount++;
                        break;
                }
            }
        }
    }
}
