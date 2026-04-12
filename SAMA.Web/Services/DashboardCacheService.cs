using System.Collections.Concurrent;
using SAMA.Web.Models;
using SAMA.Web.Services.Queries;

namespace SAMA.Web.Services;

public class DashboardCacheService(IServiceProvider _serviceProvider)
{
    private const int MinHours = 1;
    private const int MaxHours = 168;
    private const int MaxWorkspaceEntries = 50;
    private const int MaxTimelineEntries = 50;
    private const int MaxTrendsEntries = 50;
    private static readonly TimeSpan EvictionThreshold = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<Guid, CacheEntry<WorkspaceDashboardData>> _workspaceCache = new();
    private readonly ConcurrentDictionary<(Guid WorkspaceId, int Hours), CacheEntry<WorkspaceIncidentTimelineViewModel>> _timelineCache = new();
    private readonly ConcurrentDictionary<(Guid WorkspaceId, int Hours), CacheEntry<WorkspaceResponseTimeTrendsViewModel>> _trendsCache = new();

    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _workspaceLocks = new();
    private readonly ConcurrentDictionary<(Guid, int), SemaphoreSlim> _timelineLocks = new();
    private readonly ConcurrentDictionary<(Guid, int), SemaphoreSlim> _trendsLocks = new();

    public async Task<WorkspaceDashboardData> GetWorkspaceDataAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        if (_workspaceCache.TryGetValue(workspaceId, out var entry))
        {
            entry.LastAccessedAt = DateTimeOffset.UtcNow;
            return entry.Data;
        }

        var semaphore = _workspaceLocks.GetOrAdd(workspaceId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_workspaceCache.TryGetValue(workspaceId, out entry))
            {
                entry.LastAccessedAt = DateTimeOffset.UtcNow;
                return entry.Data;
            }

            var data = await PopulateWorkspaceDataAsync(workspaceId, cancellationToken);
            SetWorkspaceData(workspaceId, data);
            return data;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<WorkspaceIncidentTimelineViewModel> GetTimelineAsync(Guid workspaceId, int hours, CancellationToken cancellationToken = default)
    {
        hours = Math.Clamp(hours, MinHours, MaxHours);
        var key = (workspaceId, hours);
        if (_timelineCache.TryGetValue(key, out var entry))
        {
            entry.LastAccessedAt = DateTimeOffset.UtcNow;
            return entry.Data;
        }

        var semaphore = _timelineLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_timelineCache.TryGetValue(key, out entry))
            {
                entry.LastAccessedAt = DateTimeOffset.UtcNow;
                return entry.Data;
            }

            var data = await PopulateTimelineAsync(workspaceId, hours, cancellationToken);
            SetTimeline(workspaceId, hours, data);
            return data;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<WorkspaceResponseTimeTrendsViewModel> GetTrendsAsync(Guid workspaceId, int hours, CancellationToken cancellationToken = default)
    {
        hours = Math.Clamp(hours, MinHours, MaxHours);
        var key = (workspaceId, hours);
        if (_trendsCache.TryGetValue(key, out var entry))
        {
            entry.LastAccessedAt = DateTimeOffset.UtcNow;
            return entry.Data;
        }

        var semaphore = _trendsLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_trendsCache.TryGetValue(key, out entry))
            {
                entry.LastAccessedAt = DateTimeOffset.UtcNow;
                return entry.Data;
            }

            var data = await PopulateTrendsAsync(workspaceId, hours, cancellationToken);
            SetTrends(workspaceId, hours, data);
            return data;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task RefreshWorkspaceDataAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var semaphore = _workspaceLocks.GetOrAdd(workspaceId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var data = await PopulateWorkspaceDataAsync(workspaceId, cancellationToken);
            SetWorkspaceData(workspaceId, data);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task RefreshTimelineAsync(Guid workspaceId, int hours, CancellationToken cancellationToken = default)
    {
        hours = Math.Clamp(hours, MinHours, MaxHours);
        var key = (workspaceId, hours);
        var semaphore = _timelineLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var data = await PopulateTimelineAsync(workspaceId, hours, cancellationToken);
            SetTimeline(workspaceId, hours, data);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task RefreshTrendsAsync(Guid workspaceId, int hours, CancellationToken cancellationToken = default)
    {
        hours = Math.Clamp(hours, MinHours, MaxHours);
        var key = (workspaceId, hours);
        var semaphore = _trendsLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var data = await PopulateTrendsAsync(workspaceId, hours, cancellationToken);
            SetTrends(workspaceId, hours, data);
        }
        finally
        {
            semaphore.Release();
        }
    }

    internal void SetWorkspaceData(Guid workspaceId, WorkspaceDashboardData data)
    {
        _workspaceCache.AddOrUpdate(
            workspaceId,
            _ => new CacheEntry<WorkspaceDashboardData>(data),
            (_, existing) =>
            {
                existing.Data = data;
                existing.LastRefreshedAt = DateTimeOffset.UtcNow;
                return existing;
            });

        EnforceSizeLimit(_workspaceCache, MaxWorkspaceEntries);
    }

    internal void SetTimeline(Guid workspaceId, int hours, WorkspaceIncidentTimelineViewModel data)
    {
        var key = (workspaceId, hours);
        _timelineCache.AddOrUpdate(
            key,
            _ => new CacheEntry<WorkspaceIncidentTimelineViewModel>(data),
            (_, existing) =>
            {
                existing.Data = data;
                existing.LastRefreshedAt = DateTimeOffset.UtcNow;
                return existing;
            });

        EnforceSizeLimit(_timelineCache, MaxTimelineEntries);
    }

    internal void SetTrends(Guid workspaceId, int hours, WorkspaceResponseTimeTrendsViewModel data)
    {
        var key = (workspaceId, hours);
        _trendsCache.AddOrUpdate(
            key,
            _ => new CacheEntry<WorkspaceResponseTimeTrendsViewModel>(data),
            (_, existing) =>
            {
                existing.Data = data;
                existing.LastRefreshedAt = DateTimeOffset.UtcNow;
                return existing;
            });

        EnforceSizeLimit(_trendsCache, MaxTrendsEntries);
    }

    public void InvalidateWorkspace(Guid workspaceId)
    {
        _workspaceCache.TryRemove(workspaceId, out _);
    }

    public void InvalidateAllForWorkspace(Guid workspaceId)
    {
        _workspaceCache.TryRemove(workspaceId, out _);

        foreach (var key in _timelineCache.Keys)
        {
            if (key.WorkspaceId == workspaceId)
            {
                _timelineCache.TryRemove(key, out _);
            }
        }

        foreach (var key in _trendsCache.Keys)
        {
            if (key.WorkspaceId == workspaceId)
            {
                _trendsCache.TryRemove(key, out _);
            }
        }
    }

    public List<Guid> GetCachedWorkspaceIds()
    {
        return _workspaceCache.Keys.ToList();
    }

    public List<(Guid WorkspaceId, int Hours)> GetCachedTimelineKeys()
    {
        return _timelineCache.Keys.ToList();
    }

    public List<(Guid WorkspaceId, int Hours)> GetCachedTrendsKeys()
    {
        return _trendsCache.Keys.ToList();
    }

    public void EvictStaleEntries()
    {
        var cutoff = DateTimeOffset.UtcNow - EvictionThreshold;

        foreach (var kvp in _workspaceCache)
        {
            if (kvp.Value.LastAccessedAt < cutoff)
            {
                _workspaceCache.TryRemove(kvp.Key, out _);
            }
        }

        foreach (var kvp in _timelineCache)
        {
            if (kvp.Value.LastAccessedAt < cutoff)
            {
                _timelineCache.TryRemove(kvp.Key, out _);
            }
        }

        foreach (var kvp in _trendsCache)
        {
            if (kvp.Value.LastAccessedAt < cutoff)
            {
                _trendsCache.TryRemove(kvp.Key, out _);
            }
        }
    }

    private static void EnforceSizeLimit<TKey, TValue>(ConcurrentDictionary<TKey, CacheEntry<TValue>> cache, int maxEntries)
        where TKey : notnull
        where TValue : class
    {
        var count = cache.Count;
        if (count <= maxEntries)
        {
            return;
        }

        var toRemove = cache
            .OrderBy(kvp => kvp.Value.LastAccessedAt)
            .Take(count - maxEntries)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            cache.TryRemove(key, out _);
        }
    }

    private async Task<WorkspaceDashboardData> PopulateWorkspaceDataAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var checkQueryService = scope.ServiceProvider.GetRequiredService<CheckQueryService>();
        var alertQueryService = scope.ServiceProvider.GetRequiredService<AlertQueryService>();
        var globalSettings = scope.ServiceProvider.GetRequiredService<GlobalSettingsService>();

        var checks = await checkQueryService.GetChecksForWorkspaceAsync(workspaceId, cancellationToken);
        var recentAlerts = await alertQueryService.GetRecentAlertsForWorkspaceAsync(
            workspaceId, globalSettings.MaxRecentAlerts, cancellationToken);

        return new WorkspaceDashboardData(
            Checks: checks,
            RecentAlerts: recentAlerts
        );
    }

    private async Task<WorkspaceIncidentTimelineViewModel> PopulateTimelineAsync(Guid workspaceId, int hours, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var checkQueryService = scope.ServiceProvider.GetRequiredService<CheckQueryService>();
        return await checkQueryService.GetWorkspaceIncidentTimelineAsync(workspaceId, hours, cancellationToken: cancellationToken);
    }

    private async Task<WorkspaceResponseTimeTrendsViewModel> PopulateTrendsAsync(Guid workspaceId, int hours, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var checkQueryService = scope.ServiceProvider.GetRequiredService<CheckQueryService>();
        return await checkQueryService.GetWorkspaceResponseTimeTrendsAsync(workspaceId, hours, cancellationToken: cancellationToken);
    }

    public record WorkspaceDashboardData(
        IList<CheckListItemViewModel> Checks,
        IList<RecentAlertViewModel> RecentAlerts);

    internal class CacheEntry<TValue> where TValue : class
    {
        public TValue Data { get; set; }

        public DateTimeOffset LastRefreshedAt { get; set; }

        public DateTimeOffset LastAccessedAt { get; set; }

        public CacheEntry(TValue data)
        {
            Data = data;
            LastRefreshedAt = DateTimeOffset.UtcNow;
            LastAccessedAt = DateTimeOffset.UtcNow;
        }
    }
}
