using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SAMA.Web.Constants;
using SAMA.Web.Extensions;
using SAMA.Web.Services;
using SAMA.Web.Services.Queries;

namespace SAMA.Web.Pages.Admin.Settings;

[Authorize(Roles = AuthConstants.AdminRole)]
public class IndexModel(
    GlobalSettingsService _globalSettings,
    WorkspaceQueryService _workspaceQuery,
    ILogger<IndexModel> _logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> AvailableWorkspaces { get; set; } = [];

    public List<SelectListItem> AvailableTimeZones { get; set; } = [];

    public class InputModel
    {
        [Required(ErrorMessage = "Check results retention is required")]
        [Range(30, 3650, ErrorMessage = "Check results retention must be between 30 and 3650 days")]
        [Display(Name = "Check Results Retention (days)")]
        public int CheckResultsRetentionDays { get; set; }

        [Required(ErrorMessage = "Alert history retention is required")]
        [Range(30, 3650, ErrorMessage = "Alert history retention must be between 30 and 3650 days")]
        [Display(Name = "Alert History Retention (days)")]
        public int AlertHistoryRetentionDays { get; set; }

        [Required(ErrorMessage = "Audit log retention is required")]
        [Range(30, 3650, ErrorMessage = "Audit log retention must be between 30 and 3650 days")]
        [Display(Name = "Audit Log Retention (days)")]
        public int AuditLogRetentionDays { get; set; }

        [Required(ErrorMessage = "Dashboard refresh interval is required")]
        [Range(1, 300, ErrorMessage = "Dashboard refresh interval must be between 1 and 300 seconds")]
        [Display(Name = "Dashboard Refresh Interval (seconds)")]
        public int DashboardRefreshIntervalSeconds { get; set; }

        [Required(ErrorMessage = "Max recent alerts is required")]
        [Range(10, 500, ErrorMessage = "Max recent alerts must be between 10 and 500")]
        [Display(Name = "Max Recent Alerts")]
        public int MaxRecentAlerts { get; set; }

        [Required(ErrorMessage = "Default check timeout is required")]
        [Range(5, 3600, ErrorMessage = "Default check timeout must be between 5 seconds and 1 hour")]
        [Display(Name = "Default Check Timeout (seconds)")]
        public int DefaultCheckTimeoutSeconds { get; set; }

        [Required(ErrorMessage = "Notification timeout is required")]
        [Range(5, 300, ErrorMessage = "Notification timeout must be between 5 and 300 seconds")]
        [Display(Name = "Notification Timeout (seconds)")]
        public int NotificationTimeoutSeconds { get; set; }

        [Required(ErrorMessage = "Time zone is required")]
        [Display(Name = "Time Zone")]
        public string TimeZone { get; set; } = "UTC";

        [Display(Name = "Default Workspace")]
        public Guid? AnonymousDefaultWorkspaceId { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadPageDataAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadPageDataAsync();
            return Page();
        }

        try
        {
            _globalSettings.CheckResultsRetentionDays = Input.CheckResultsRetentionDays;
            _globalSettings.AlertHistoryRetentionDays = Input.AlertHistoryRetentionDays;
            _globalSettings.AuditLogRetentionDays = Input.AuditLogRetentionDays;
            _globalSettings.DashboardRefreshIntervalSeconds = Input.DashboardRefreshIntervalSeconds;
            _globalSettings.MaxRecentAlerts = Input.MaxRecentAlerts;
            _globalSettings.DefaultCheckTimeoutSeconds = Input.DefaultCheckTimeoutSeconds;
            _globalSettings.NotificationTimeoutSeconds = Input.NotificationTimeoutSeconds;
            _globalSettings.TimeZone = Input.TimeZone;
            _globalSettings.AnonymousDefaultWorkspaceId = Input.AnonymousDefaultWorkspaceId;

            _logger.LogInformation(
                "Global settings updated by {User}",
                User.Identity?.Name ?? "Unknown");

            TempData["SuccessMessage"] = "Settings updated successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating global settings");
            ModelState.AddModelError(string.Empty, "An error occurred while saving settings. Please try again.");
            await LoadPageDataAsync();
            return Page();
        }

        await LoadPageDataAsync();
        return Page();
    }

    private async Task LoadPageDataAsync()
    {
        await LoadWorkspacesAsync();
        LoadTimeZones();
        LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        Input.CheckResultsRetentionDays = _globalSettings.CheckResultsRetentionDays;
        Input.AlertHistoryRetentionDays = _globalSettings.AlertHistoryRetentionDays;
        Input.AuditLogRetentionDays = _globalSettings.AuditLogRetentionDays;
        Input.DashboardRefreshIntervalSeconds = _globalSettings.DashboardRefreshIntervalSeconds;
        Input.MaxRecentAlerts = _globalSettings.MaxRecentAlerts;
        Input.DefaultCheckTimeoutSeconds = _globalSettings.DefaultCheckTimeoutSeconds;
        Input.NotificationTimeoutSeconds = _globalSettings.NotificationTimeoutSeconds;
        Input.TimeZone = _globalSettings.TimeZone;
        Input.AnonymousDefaultWorkspaceId = _globalSettings.AnonymousDefaultWorkspaceId;
    }

    private async Task LoadWorkspacesAsync()
    {
        var allWorkspaces = await _workspaceQuery.GetWorkspacesAsync();
        var publicWorkspaces = allWorkspaces.Where(w => w.IsPublic);

        AvailableWorkspaces =
        [
            new SelectListItem("None (show workspace list)", "")
        ];
        AvailableWorkspaces.AddRange(publicWorkspaces.Select(w => new SelectListItem(w.Name, w.Id.ToString())));
    }

    private void LoadTimeZones()
    {
        AvailableTimeZones = TimeZoneExtensions.GetIanaTimeZones()
            .Select(tz => new SelectListItem($"{tz.DisplayName} ({tz.IanaId})", tz.IanaId))
            .ToList();
    }
}
