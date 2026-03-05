using Microsoft.EntityFrameworkCore;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Shared.Constants;
using SAMA.Web.Models;

namespace SAMA.Web.Services.Queries;

public class WorkspaceQueryService(SamaDbContext _dbContext)
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

        return await query
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
                UpCount = w.Checks.Count(c => c.Enabled && c.CheckResults
                    .OrderByDescending(r => r.CheckedAt).Select(r => r.Status).FirstOrDefault() == CheckStatuses.Up),
                WarnCount = w.Checks.Count(c => c.Enabled && c.CheckResults
                    .OrderByDescending(r => r.CheckedAt).Select(r => r.Status).FirstOrDefault() == CheckStatuses.Warn),
                DownCount = w.Checks.Count(c => c.Enabled && c.CheckResults
                    .OrderByDescending(r => r.CheckedAt).Select(r => r.Status).FirstOrDefault() == CheckStatuses.Down),
                NotificationChannelCount = w.NotificationChannels.Count,
                UserCount = w.UserWorkspaces.Count
            })
            .ToListAsync(cancellationToken);
    }

    public virtual async Task<WorkspaceDetailsViewModel?> GetWorkspaceDetailsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Workspaces
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
                UpCount = w.Checks.Count(c => c.Enabled && c.CheckResults
                    .OrderByDescending(r => r.CheckedAt).Select(r => r.Status).FirstOrDefault() == CheckStatuses.Up),
                WarnCount = w.Checks.Count(c => c.Enabled && c.CheckResults
                    .OrderByDescending(r => r.CheckedAt).Select(r => r.Status).FirstOrDefault() == CheckStatuses.Warn),
                DownCount = w.Checks.Count(c => c.Enabled && c.CheckResults
                    .OrderByDescending(r => r.CheckedAt).Select(r => r.Status).FirstOrDefault() == CheckStatuses.Down),
                NotificationChannelCount = w.NotificationChannels.Count,
                UserCount = w.UserWorkspaces.Count
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
