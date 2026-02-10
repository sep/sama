using Microsoft.AspNetCore.Mvc;
using SAMA.Web.Authorization;
using SAMA.Web.Pages.Shared;
using SAMA.Web.Services.Commands;
using SAMA.Web.Services.Queries;

namespace SAMA.Web.Pages.Checks;

[RequireWorkspaceEditAccess]
public class DeleteModel(WorkspaceQueryService _workspaceQueryService, CheckQueryService _checkQueryService, CheckCommandService _checkCommandService)
    : WorkspacePageModel(_workspaceQueryService)
{
    public CheckDeleteViewModel CheckToDelete { get; set; } = new();

    public class CheckDeleteViewModel
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string CheckType { get; set; } = string.Empty;

        public string Schedule { get; set; } = string.Empty;

        public bool Enabled { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public int ResultCount { get; set; }

        public int AlertCount { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid? id)
    {
        if (!id.HasValue)
        {
            return NotFound();
        }

        var check = await _checkQueryService.GetCheckDetailsAsync(id.Value);
        if (check == null)
        {
            return NotFound();
        }

        var result = await LoadWorkspaceContextAsync(check.WorkspaceId, "Checks");
        if (result != null)
        {
            return result;
        }

        CheckToDelete = new CheckDeleteViewModel
        {
            Id = check.Id,
            Name = check.Name,
            CheckType = check.CheckType,
            Schedule = check.Schedule,
            Enabled = check.Enabled,
            CreatedAt = check.CreatedAt,
            ResultCount = check.ResultCount,
            AlertCount = check.AlertCount
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid? id)
    {
        if (!id.HasValue)
        {
            return NotFound();
        }

        var check = await _checkQueryService.GetCheckDetailsAsync(id.Value);
        if (check == null)
        {
            return NotFound();
        }

        var checkName = check.Name;
        var workspaceId = check.WorkspaceId;

        var deleted = await _checkCommandService.DeleteCheckAsync(
            id.Value,
            User.Identity?.Name ?? "System");

        if (!deleted)
        {
            return BadRequest();
        }

        TempData["SuccessMessage"] = $"Check '{checkName}' deleted successfully.";

        return RedirectToPage("Index", new { workspaceId });
    }
}
