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
    CheckQueryService _checkQueryService,
    AlertQueryService _alertQueryService,
    GlobalSettingsService _globalSettings,
    MarkdownService _markdownService)
    : WorkspacePageModel(_workspaceQueryService)
{
    public IList<CheckListItemViewModel> Checks { get; set; } = [];

    public IList<RecentAlertViewModel> RecentAlerts { get; set; } = [];

    public WorkspaceIncidentTimelineViewModel IncidentTimeline { get; set; } = new();

    public WorkspaceResponseTimeTrendsViewModel ResponseTimeTrends { get; set; } = new();

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

        var workspace = await _workspaceQueryService.GetWorkspaceDetailsAsync(WorkspaceId);
        if (workspace != null)
        {
            DashboardMessageHtml = _markdownService.RenderToHtml(workspace.DashboardMessage);
        }

        Checks = await _checkQueryService.GetChecksForWorkspaceAsync(WorkspaceId);
        RecentAlerts = await _alertQueryService.GetRecentAlertsForWorkspaceAsync(
            WorkspaceId,
            _globalSettings.MaxRecentAlerts);

        IncidentTimeline = await _checkQueryService.GetWorkspaceIncidentTimelineAsync(WorkspaceId, TimelineHours);

        ResponseTimeTrends = await _checkQueryService.GetWorkspaceResponseTimeTrendsAsync(WorkspaceId, TrendsHours);

        return Page();
    }
}
