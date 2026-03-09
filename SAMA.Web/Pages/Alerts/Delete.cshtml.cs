using Microsoft.AspNetCore.Mvc;
using SAMA.Web.Authorization;
using SAMA.Web.Models;
using SAMA.Web.Pages.Shared;
using SAMA.Web.Services.Commands;
using SAMA.Web.Services.Queries;

namespace SAMA.Web.Pages.Alerts;

[RequireWorkspaceEditAccess]
public class DeleteModel(WorkspaceQueryService _workspaceQueryService, AlertQueryService _alertQueryService, AlertCommandService _alertCommandService)
    : WorkspacePageModel(_workspaceQueryService)
{
    public Guid CheckId { get; set; }

    public string CheckName { get; set; } = string.Empty;

    public AlertDetailsViewModel AlertToDelete { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid? id)
    {
        if (!id.HasValue)
        {
            return NotFound();
        }

        var alert = await _alertQueryService.GetAlertDetailsAsync(id.Value);
        if (alert == null)
        {
            return NotFound();
        }

        var result = await LoadWorkspaceContextAsync(alert.WorkspaceId, "Checks");
        if (result != null)
        {
            return result;
        }

        CheckId = alert.CheckId;
        CheckName = alert.CheckName;
        AlertToDelete = alert;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid? id)
    {
        if (!id.HasValue)
        {
            return NotFound();
        }

        var alert = await _alertQueryService.GetAlertDetailsAsync(id.Value);
        if (alert == null)
        {
            return NotFound();
        }

        var success = await _alertCommandService.DeleteAlertAsync(
            id.Value,
            User.Identity?.Name ?? "System");

        if (!success)
        {
            TempData["ErrorMessage"] = "Failed to delete alert rule.";
            return RedirectToPage("Index", new { checkId = alert.CheckId });
        }

        TempData["SuccessMessage"] = $"Alert rule '{alert.Name}' deleted successfully.";

        return RedirectToPage("Index", new { checkId = alert.CheckId });
    }
}
