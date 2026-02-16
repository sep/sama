using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SAMA.Web.Constants;
using SAMA.Web.Models.Export;
using SAMA.Web.Services;

namespace SAMA.Web.Pages.Admin.Settings;

[Authorize(Roles = AuthConstants.AdminRole)]
public class ImportExportModel(
    ConfigurationExportService _exportService,
    ConfigurationImportService _importService,
    ILogger<ImportExportModel> _logger) : PageModel
{
    [BindProperty]
    public ExportInputModel ExportInput { get; set; } = new();

    [BindProperty]
    public ImportInputModel ImportInput { get; set; } = new();

    public List<SelectListItem> ImportStrategyOptions { get; set; } =
    [
        new SelectListItem("Skip existing workspaces", nameof(ImportMergeStrategy.SkipExisting)),
        new SelectListItem("Merge into existing workspaces", nameof(ImportMergeStrategy.MergeIntoExisting)),
        new SelectListItem("Replace existing workspaces", nameof(ImportMergeStrategy.ReplaceExisting))
    ];

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

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostExportAsync()
    {
        ModelState.Clear();

        if (string.IsNullOrWhiteSpace(ExportInput.Password) || ExportInput.Password.Length < 14)
        {
            TempData["ExportError"] = "Password must be at least 14 characters.";
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
            return Page();
        }
    }

    public async Task<IActionResult> OnPostImportAsync()
    {
        ModelState.Clear();

        if (ImportInput.File == null || ImportInput.File.Length == 0)
        {
            TempData["ImportError"] = "Please select an export file.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(ImportInput.Password))
        {
            TempData["ImportError"] = "Password is required for decryption.";
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

        return Page();
    }
}
