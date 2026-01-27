using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SAMA.Web.Constants;
using SAMA.Web.Models.Export;
using SAMA.Web.Services;
using SAMA.Web.Services.Queries;

namespace SAMA.Web.Pages.Admin;

[Authorize(Roles = AuthConstants.AdminRole)]
public class SettingsModel(
    GlobalSettingsService _globalSettings,
    WorkspaceQueryService _workspaceQuery,
    ConfigurationExportService _exportService,
    ConfigurationImportService _importService,
    ILogger<SettingsModel> _logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public ExportInputModel ExportInput { get; set; } = new();

    [BindProperty]
    public ImportInputModel ImportInput { get; set; } = new();

    public List<SelectListItem> AvailableWorkspaces { get; set; } = [];

    public List<SelectListItem> ImportStrategyOptions { get; set; } =
    [
        new SelectListItem("Skip existing workspaces", nameof(ImportMergeStrategy.SkipExisting)),
        new SelectListItem("Merge into existing workspaces", nameof(ImportMergeStrategy.MergeIntoExisting)),
        new SelectListItem("Replace existing workspaces", nameof(ImportMergeStrategy.ReplaceExisting))
    ];

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
        [Range(5, 300, ErrorMessage = "Default check timeout must be between 5 and 300 seconds")]
        [Display(Name = "Default Check Timeout (seconds)")]
        public int DefaultCheckTimeoutSeconds { get; set; }

        [Required(ErrorMessage = "Notification timeout is required")]
        [Range(5, 300, ErrorMessage = "Notification timeout must be between 5 and 300 seconds")]
        [Display(Name = "Notification Timeout (seconds)")]
        public int NotificationTimeoutSeconds { get; set; }

        [Display(Name = "Default Workspace")]
        public Guid? AnonymousDefaultWorkspaceId { get; set; }
    }

    public class ExportInputModel
    {
        [Display(Name = "Encryption Password")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }

    public class ImportInputModel
    {
        [Display(Name = "Export File")]
        public IFormFile? File { get; set; }

        [Display(Name = "Decryption Password")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Import Strategy")]
        public ImportMergeStrategy ImportStrategy { get; set; } = ImportMergeStrategy.SkipExisting;
    }

    public async Task OnGetAsync()
    {
        await LoadWorkspacesAsync();
        LoadCurrentSettings();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadWorkspacesAsync();
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
            await LoadWorkspacesAsync();
            return Page();
        }

        await LoadWorkspacesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostExportAsync()
    {
        ModelState.Clear();

        if (string.IsNullOrWhiteSpace(ExportInput.Password) || ExportInput.Password.Length < 14)
        {
            TempData["ExportError"] = "Password must be at least 14 characters.";
            await LoadWorkspacesAsync();
            LoadCurrentSettings();
            return Page();
        }

        try
        {
            var export = await _exportService.ExportAllAsync(ExportInput.Password);
            var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            var fileName = $"sama-export-{DateTimeOffset.UtcNow:yyyy-MM-dd}.json";

            _logger.LogInformation("Configuration exported by {User}", User.Identity?.Name ?? "Unknown");

            return File(bytes, "application/json", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting configuration");
            TempData["ExportError"] = "An error occurred while exporting configuration.";
            await LoadWorkspacesAsync();
            LoadCurrentSettings();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostImportAsync()
    {
        ModelState.Clear();

        if (ImportInput.File == null || ImportInput.File.Length == 0)
        {
            TempData["ImportError"] = "Please select an export file.";
            await LoadWorkspacesAsync();
            LoadCurrentSettings();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(ImportInput.Password))
        {
            TempData["ImportError"] = "Password is required for decryption.";
            await LoadWorkspacesAsync();
            LoadCurrentSettings();
            return Page();
        }

        try
        {
            using var stream = ImportInput.File.OpenReadStream();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var export = JsonSerializer.Deserialize<SamaExportDto>(json);
            if (export == null)
            {
                TempData["ImportError"] = "Invalid export file format.";
                await LoadWorkspacesAsync();
                LoadCurrentSettings();
                return Page();
            }

            var result = await _importService.ImportAsync(export, ImportInput.Password, ImportInput.ImportStrategy);

            if (!result.Success)
            {
                TempData["ImportError"] = string.Join(" ", result.Errors);
            }
            else
            {
                var summary = new List<string>();
                if (result.WorkspacesCreated > 0)
                {
                    summary.Add($"{result.WorkspacesCreated} workspace(s) created");
                }

                if (result.WorkspacesUpdated > 0)
                {
                    summary.Add($"{result.WorkspacesUpdated} workspace(s) updated");
                }

                if (result.ChecksCreated > 0)
                {
                    summary.Add($"{result.ChecksCreated} check(s) created");
                }

                if (result.NotificationChannelsCreated > 0)
                {
                    summary.Add($"{result.NotificationChannelsCreated} notification channel(s) created");
                }

                if (result.AlertsCreated > 0)
                {
                    summary.Add($"{result.AlertsCreated} alert(s) created");
                }

                var message = summary.Count > 0
                    ? $"Import completed: {string.Join(", ", summary)}."
                    : "Import completed with no changes.";

                TempData["ImportSuccess"] = message;

                if (result.Warnings.Count > 0)
                {
                    TempData["ImportWarnings"] = result.Warnings;
                }

                _logger.LogInformation(
                    "Configuration imported by {User}: {WorkspacesCreated} workspaces created, {WorkspacesUpdated} updated",
                    User.Identity?.Name ?? "Unknown",
                    result.WorkspacesCreated,
                    result.WorkspacesUpdated);
            }
        }
        catch (JsonException)
        {
            TempData["ImportError"] = "Invalid export file format. Please select a valid SAMA export file.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing configuration");
            TempData["ImportError"] = "An error occurred while importing configuration.";
        }

        await LoadWorkspacesAsync();
        LoadCurrentSettings();
        return Page();
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
}
