using Quartz;

namespace SAMA.Web.Services;

[DisallowConcurrentExecution]
public class DashboardCacheRefreshJob(
    DashboardCacheService _cacheService,
    ILogger<DashboardCacheRefreshJob> _logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        _cacheService.EvictStaleEntries();

        var workspaceIds = _cacheService.GetCacheableWorkspaceIds();
        var timelineKeys = _cacheService.GetCacheableTimelineKeys();
        var trendsKeys = _cacheService.GetCacheableTrendsKeys();

        if (workspaceIds.Count == 0 && timelineKeys.Count == 0 && trendsKeys.Count == 0)
        {
            return;
        }

        _logger.LogDebug(
            "Refreshing dashboard cache: {WorkspaceCount} workspace(s), {TimelineCount} timeline(s), {TrendsCount} trends",
            workspaceIds.Count,
            timelineKeys.Count,
            trendsKeys.Count);

        foreach (var workspaceId in workspaceIds)
        {
            try
            {
                await _cacheService.RefreshWorkspaceDataAsync(workspaceId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh dashboard cache for workspace {WorkspaceId}", workspaceId);
            }
        }

        foreach (var (workspaceId, hours) in timelineKeys)
        {
            try
            {
                await _cacheService.RefreshTimelineAsync(workspaceId, hours);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh timeline cache for workspace {WorkspaceId}, hours {Hours}", workspaceId, hours);
            }
        }

        foreach (var (workspaceId, hours) in trendsKeys)
        {
            try
            {
                await _cacheService.RefreshTrendsAsync(workspaceId, hours);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh trends cache for workspace {WorkspaceId}, hours {Hours}", workspaceId, hours);
            }
        }
    }
}
