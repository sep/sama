using Quartz;

namespace SAMA.Web.Services;

[DisallowConcurrentExecution]
public class DashboardCacheRefreshJob(
    DashboardCacheService _cacheService,
    ILogger<DashboardCacheRefreshJob> _logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        _cacheService.EvictStaleEntries();

        var workspaceIds = _cacheService.GetCachedWorkspaceIds();
        var timelineKeys = _cacheService.GetCachedTimelineKeys();
        var trendsKeys = _cacheService.GetCachedTrendsKeys();

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
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _cacheService.RefreshWorkspaceDataAsync(workspaceId, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh dashboard cache for workspace {WorkspaceId}", workspaceId);
            }
        }

        foreach (var (workspaceId, hours) in timelineKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _cacheService.RefreshTimelineAsync(workspaceId, hours, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh timeline cache for workspace {WorkspaceId}, hours {Hours}", workspaceId, hours);
            }
        }

        foreach (var (workspaceId, hours) in trendsKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _cacheService.RefreshTrendsAsync(workspaceId, hours, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh trends cache for workspace {WorkspaceId}, hours {Hours}", workspaceId, hours);
            }
        }
    }
}
