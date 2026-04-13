using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SAMA.Web.Authorization;
using SAMA.Web.Models;
using SAMA.Web.Pages.Shared;
using SAMA.Web.Services;
using SAMA.Web.Services.Queries;

namespace SAMA.Web.Pages.Dashboard;

[RequireWorkspaceViewAccess]
public class IndexModel(
    WorkspaceQueryService _workspaceQueryService,
    GlobalSettingsService _globalSettings,
    MarkdownService _markdownService,
    DashboardCacheService _cacheService)
    : WorkspacePageModel(_workspaceQueryService)
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public IList<CheckListItemViewModel> Checks { get; set; } = [];

    public IList<RecentAlertViewModel> RecentAlerts { get; set; } = [];

    public string DashboardMessageHtml { get; set; } = string.Empty;

    public int RefreshIntervalSeconds { get; set; }

    public int TimelineHours { get; set; } = 24;

    public int TrendsHours { get; set; } = 24;

    public async Task<IActionResult> OnGetAsync(Guid workspaceId, int? timelineHours, int? trendsHours)
    {
        var result = await LoadWorkspaceContextAsync(workspaceId, "Dashboard");
        if (result != null)
        {
            return result;
        }

        RefreshIntervalSeconds = _globalSettings.DashboardRefreshIntervalSeconds;

        TimelineHours = timelineHours ?? 24;
        TrendsHours = trendsHours ?? 24;

        var workspaceData = await _cacheService.GetWorkspaceDataAsync(WorkspaceId);
        Checks = workspaceData.Checks;
        RecentAlerts = workspaceData.RecentAlerts;

        var dashboardMessage = await _workspaceQueryService.GetDashboardMessageAsync(WorkspaceId);
        DashboardMessageHtml = _markdownService.RenderToHtml(dashboardMessage);

        return Page();
    }

    public async Task<IActionResult> OnGetTimelineAsync(Guid workspaceId, int? timelineHours)
    {
        var result = await LoadWorkspaceContextAsync(workspaceId, "Dashboard");
        if (result != null)
        {
            return result;
        }

        var hours = timelineHours ?? 24;
        var timeline = await _cacheService.GetTimelineAsync(WorkspaceId, hours);
        var json = JsonSerializer.Serialize(timeline, CamelCaseOptions);
        return Content(
            $"""<script id="incidentTimelineChartData" type="application/json" hx-swap-oob="true">{json}</script>""",
            "text/html");
    }

    public async Task<IActionResult> OnGetTrendsAsync(Guid workspaceId, int? trendsHours)
    {
        var result = await LoadWorkspaceContextAsync(workspaceId, "Dashboard");
        if (result != null)
        {
            return result;
        }

        var hours = trendsHours ?? 24;
        var trends = await _cacheService.GetTrendsAsync(WorkspaceId, hours);
        var json = JsonSerializer.Serialize(trends, CamelCaseOptions);
        return Content(
            $"""<script id="responseTimeTrendsChartData" type="application/json" hx-swap-oob="true">{json}</script>""",
            "text/html");
    }
}
