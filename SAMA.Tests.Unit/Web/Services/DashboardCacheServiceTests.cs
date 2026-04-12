using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SAMA.Web.Models;
using SAMA.Web.Services;
using SAMA.Web.Services.Queries;
using static SAMA.Web.Services.DashboardCacheService;

namespace SAMA.Tests.Unit.Web.Services;

[TestClass]
public class DashboardCacheServiceTests
{
    private DashboardCacheService _cacheService = null!;
    private CheckQueryService _mockCheckQuery = null!;
    private AlertQueryService _mockAlertQuery = null!;
    private GlobalSettingsService _mockGlobalSettings = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockCheckQuery = Substitute.For<CheckQueryService>(null!, null!, null!, null!);
        _mockAlertQuery = Substitute.For<AlertQueryService>((SAMA.Data.SamaDbContext)null!);
        _mockGlobalSettings = Substitute.For<GlobalSettingsService>(null, null, null, null);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped(_ => _mockCheckQuery);
        serviceCollection.AddScoped(_ => _mockAlertQuery);
        serviceCollection.AddScoped(_ => _mockGlobalSettings);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        _cacheService = new DashboardCacheService(serviceProvider);
    }

    private static WorkspaceDashboardData CreateWorkspaceData()
    {
        return new WorkspaceDashboardData([], []);
    }

    private static WorkspaceIncidentTimelineViewModel CreateTimeline()
    {
        return new WorkspaceIncidentTimelineViewModel();
    }

    private static WorkspaceResponseTimeTrendsViewModel CreateTrends()
    {
        return new WorkspaceResponseTimeTrendsViewModel();
    }

    [TestMethod]
    public async Task GetWorkspaceDataShouldPopulateOnCacheMiss()
    {
        var workspaceId = Guid.NewGuid();
        _mockCheckQuery.GetChecksForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<CheckListItemViewModel>());
        _mockAlertQuery.GetRecentAlertsForWorkspaceAsync(workspaceId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<RecentAlertViewModel>());

        var result = await _cacheService.GetWorkspaceDataAsync(workspaceId);

        Assert.IsNotNull(result);
        await _mockCheckQuery.Received(1).GetChecksForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task GetWorkspaceDataShouldReturnCachedOnHit()
    {
        var workspaceId = Guid.NewGuid();
        _cacheService.SetWorkspaceData(workspaceId, CreateWorkspaceData());

        var result = await _cacheService.GetWorkspaceDataAsync(workspaceId);

        Assert.IsNotNull(result);
        await _mockCheckQuery.DidNotReceive().GetChecksForWorkspaceAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task GetTimelineShouldPopulateOnCacheMiss()
    {
        var workspaceId = Guid.NewGuid();
        var expected = CreateTimeline();
        _mockCheckQuery.GetWorkspaceIncidentTimelineAsync(workspaceId, 24, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await _cacheService.GetTimelineAsync(workspaceId, 24);

        Assert.AreSame(expected, result);
    }

    [TestMethod]
    public async Task GetTimelineShouldReturnCachedOnHit()
    {
        var workspaceId = Guid.NewGuid();
        var original = CreateTimeline();
        _cacheService.SetTimeline(workspaceId, 24, original);

        var result = await _cacheService.GetTimelineAsync(workspaceId, 24);

        Assert.AreSame(original, result);
        await _mockCheckQuery.DidNotReceive().GetWorkspaceIncidentTimelineAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task GetTrendsShouldPopulateOnCacheMiss()
    {
        var workspaceId = Guid.NewGuid();
        var expected = CreateTrends();
        _mockCheckQuery.GetWorkspaceResponseTimeTrendsAsync(workspaceId, 24, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await _cacheService.GetTrendsAsync(workspaceId, 24);

        Assert.AreSame(expected, result);
    }

    [TestMethod]
    public async Task GetTrendsShouldReturnCachedOnHit()
    {
        var workspaceId = Guid.NewGuid();
        var original = CreateTrends();
        _cacheService.SetTrends(workspaceId, 24, original);

        var result = await _cacheService.GetTrendsAsync(workspaceId, 24);

        Assert.AreSame(original, result);
        await _mockCheckQuery.DidNotReceive().GetWorkspaceResponseTimeTrendsAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public void InvalidateWorkspaceShouldRemoveWorkspaceDataOnly()
    {
        var workspaceId = Guid.NewGuid();
        _cacheService.SetWorkspaceData(workspaceId, CreateWorkspaceData());
        _cacheService.SetTimeline(workspaceId, 24, CreateTimeline());
        _cacheService.SetTrends(workspaceId, 24, CreateTrends());

        _cacheService.InvalidateWorkspace(workspaceId);

        Assert.AreEqual(0, _cacheService.GetCachedWorkspaceIds().Count);
        Assert.IsTrue(_cacheService.GetCachedTimelineKeys().Count > 0);
        Assert.IsTrue(_cacheService.GetCachedTrendsKeys().Count > 0);
    }

    [TestMethod]
    public void InvalidateAllForWorkspaceShouldRemoveAllEntriesForWorkspace()
    {
        var workspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        _cacheService.SetWorkspaceData(workspaceId, CreateWorkspaceData());
        _cacheService.SetTimeline(workspaceId, 24, CreateTimeline());
        _cacheService.SetTrends(workspaceId, 24, CreateTrends());
        _cacheService.SetWorkspaceData(otherWorkspaceId, CreateWorkspaceData());
        _cacheService.SetTimeline(otherWorkspaceId, 24, CreateTimeline());

        _cacheService.InvalidateAllForWorkspace(workspaceId);

        Assert.AreEqual(1, _cacheService.GetCachedWorkspaceIds().Count);
        Assert.AreEqual(otherWorkspaceId, _cacheService.GetCachedWorkspaceIds()[0]);
        Assert.AreEqual(1, _cacheService.GetCachedTimelineKeys().Count);
        Assert.AreEqual(0, _cacheService.GetCachedTrendsKeys().Count);
    }

    [TestMethod]
    public async Task GetWorkspaceDataShouldOverwriteExistingEntry()
    {
        var workspaceId = Guid.NewGuid();
        _cacheService.SetWorkspaceData(workspaceId, CreateWorkspaceData());
        _cacheService.SetWorkspaceData(workspaceId, CreateWorkspaceData());

        var result = await _cacheService.GetWorkspaceDataAsync(workspaceId);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, _cacheService.GetCachedWorkspaceIds().Count);
    }

    [TestMethod]
    public void TimelineCacheShouldKeySeparatelyByHours()
    {
        var workspaceId = Guid.NewGuid();
        _cacheService.SetTimeline(workspaceId, 6, CreateTimeline());
        _cacheService.SetTimeline(workspaceId, 24, CreateTimeline());

        Assert.AreEqual(2, _cacheService.GetCachedTimelineKeys().Count);
    }

    [TestMethod]
    public void TrendsCacheShouldKeySeparatelyByHours()
    {
        var workspaceId = Guid.NewGuid();
        _cacheService.SetTrends(workspaceId, 6, CreateTrends());
        _cacheService.SetTrends(workspaceId, 24, CreateTrends());

        Assert.AreEqual(2, _cacheService.GetCachedTrendsKeys().Count);
    }

    [TestMethod]
    public async Task GetWorkspaceDataShouldPreventCacheStampede()
    {
        var workspaceId = Guid.NewGuid();
        var callCount = 0;
        var delayTcs = new TaskCompletionSource();

        _mockCheckQuery.GetChecksForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                Interlocked.Increment(ref callCount);
                await delayTcs.Task;
                return new List<CheckListItemViewModel>();
            });
        _mockAlertQuery.GetRecentAlertsForWorkspaceAsync(workspaceId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<RecentAlertViewModel>());

        var task1 = _cacheService.GetWorkspaceDataAsync(workspaceId);
        var task2 = _cacheService.GetWorkspaceDataAsync(workspaceId);

        delayTcs.SetResult();

        var result1 = await task1;
        var result2 = await task2;

        Assert.AreEqual(1, callCount);
        Assert.IsNotNull(result1);
        Assert.IsNotNull(result2);
    }

    [TestMethod]
    public void EvictStaleEntriesShouldNotRemoveRecentEntries()
    {
        var workspaceId = Guid.NewGuid();
        _cacheService.SetWorkspaceData(workspaceId, CreateWorkspaceData());
        _cacheService.SetTimeline(workspaceId, 24, CreateTimeline());
        _cacheService.SetTrends(workspaceId, 24, CreateTrends());

        _cacheService.EvictStaleEntries();

        Assert.AreEqual(1, _cacheService.GetCachedWorkspaceIds().Count);
        Assert.AreEqual(1, _cacheService.GetCachedTimelineKeys().Count);
        Assert.AreEqual(1, _cacheService.GetCachedTrendsKeys().Count);
    }

    [TestMethod]
    public void SetWorkspaceDataShouldNotPreventEvictionOfStaleEntry()
    {
        var workspaceId = Guid.NewGuid();
        _cacheService.SetWorkspaceData(workspaceId, CreateWorkspaceData());
        BackdateLastAccessedAt("_workspaceCache", workspaceId);

        _cacheService.SetWorkspaceData(workspaceId, CreateWorkspaceData());
        _cacheService.EvictStaleEntries();

        Assert.AreEqual(0, _cacheService.GetCachedWorkspaceIds().Count);
    }

    [TestMethod]
    public void SetTimelineShouldNotPreventEvictionOfStaleEntry()
    {
        var workspaceId = Guid.NewGuid();
        _cacheService.SetTimeline(workspaceId, 24, CreateTimeline());
        BackdateLastAccessedAt("_timelineCache", (workspaceId, 24));

        _cacheService.SetTimeline(workspaceId, 24, CreateTimeline());
        _cacheService.EvictStaleEntries();

        Assert.AreEqual(0, _cacheService.GetCachedTimelineKeys().Count);
    }

    [TestMethod]
    public void SetTrendsShouldNotPreventEvictionOfStaleEntry()
    {
        var workspaceId = Guid.NewGuid();
        _cacheService.SetTrends(workspaceId, 24, CreateTrends());
        BackdateLastAccessedAt("_trendsCache", (workspaceId, 24));

        _cacheService.SetTrends(workspaceId, 24, CreateTrends());
        _cacheService.EvictStaleEntries();

        Assert.AreEqual(0, _cacheService.GetCachedTrendsKeys().Count);
    }

    [TestMethod]
    public void GetCachedWorkspaceIdsShouldReturnAllCachedEntries()
    {
        var workspaceId1 = Guid.NewGuid();
        var workspaceId2 = Guid.NewGuid();
        _cacheService.SetWorkspaceData(workspaceId1, CreateWorkspaceData());
        _cacheService.SetWorkspaceData(workspaceId2, CreateWorkspaceData());

        var activeIds = _cacheService.GetCachedWorkspaceIds();

        Assert.AreEqual(2, activeIds.Count);
        CollectionAssert.Contains(activeIds, workspaceId1);
        CollectionAssert.Contains(activeIds, workspaceId2);
    }

    private void BackdateLastAccessedAt<TKey>(string fieldName, TKey key)
    {
        var field = typeof(DashboardCacheService).GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (System.Collections.IDictionary)field.GetValue(_cacheService)!;
        var entry = dict[key!];
        var prop = entry!.GetType().GetProperty("LastAccessedAt")!;
        prop.SetValue(entry, DateTimeOffset.UtcNow.AddMinutes(-15));
    }
}
