using Microsoft.AspNetCore.Mvc;
using SAMA.Shared.Constants;
using SAMA.Web.Authorization;
using SAMA.Web.Models;
using SAMA.Web.Pages.Shared;
using SAMA.Web.Services;
using SAMA.Web.Services.Queries;

namespace SAMA.Web.Pages.Checks;

[RequireWorkspaceViewAccess]
public class DetailsModel(
    WorkspaceQueryService _workspaceQueryService,
    CheckQueryService _checkQueryService,
    ScriptOutputBuffer _scriptOutputBuffer,
    GlobalSettingsService _globalSettings)
    : WorkspacePageModel(_workspaceQueryService)
{
    public CheckDetailsViewModel Check { get; set; } = new();

    public List<ScriptOutputEntry> ScriptOutputs { get; set; } = [];

    public int RefreshIntervalSeconds { get; set; }

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

        Check = check;

        // Load script outputs if this is a script check
        if (check.CheckType == CheckTypes.Script)
        {
            ScriptOutputs = _scriptOutputBuffer.GetOutputs(check.Id).ToList();
        }

        RefreshIntervalSeconds = _globalSettings.DashboardRefreshIntervalSeconds;

        return Page();
    }

    public async Task<IActionResult> OnGetHistoryAsync(Guid? id, int hours = 24)
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

        var history = await _checkQueryService.GetCheckHistoryAsync(id.Value, hours);

        return new JsonResult(history);
    }

    public async Task<IActionResult> OnGetUptimeAsync(Guid? id, int hours = 24)
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

        var uptime = await _checkQueryService.GetCheckUptimeAsync(id.Value, hours);
        if (uptime == null)
        {
            return new JsonResult(new
            {
                UptimePercentage = 0.0,
                TotalChecks = 0,
                UpCount = 0,
                WarnCount = 0,
                DownCount = 0,
                AvailableCount = 0
            });
        }

        return new JsonResult(uptime);
    }
}
