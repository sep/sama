using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SAMA.Data;
using SAMA.Web.Extensions;
using SAMA.Web.Services;
using SAMA.Web.Services.Queries;

namespace SAMA.Web.Pages.Shared;

/// <summary>
/// Base page model for pages that operate within a workspace context.
/// Automatically loads workspace information and sets ViewData for layout rendering.
/// </summary>
public abstract class WorkspacePageModel : PageModel
{
    public Guid WorkspaceId { get; protected set; }

    public string WorkspaceName { get; protected set; } = string.Empty;

    private readonly WorkspaceQueryService _workspaceQueryService;

    public WorkspacePageModel(SamaDbContext dbContext, ApplicationStateService appStateService)
    {
        _workspaceQueryService = new WorkspaceQueryService(dbContext, appStateService);
    }

    public WorkspacePageModel(WorkspaceQueryService workspaceQueryService)
    {
        _workspaceQueryService = workspaceQueryService;
    }

    /// <summary>
    /// Loads workspace context and sets ViewData for the workspace layout.
    /// Call this in OnGetAsync/OnPostAsync before other logic.
    /// </summary>
    /// <param name="workspaceId">The workspace ID from query parameter</param>
    /// <param name="activeTab">The active tab name (Dashboard, Checks, Channels, Settings)</param>
    /// <returns>NotFoundResult if workspace doesn't exist, RedirectResult if no workspace ID provided, otherwise null to continue</returns>
    protected async Task<IActionResult?> LoadWorkspaceContextAsync(Guid? workspaceId, string activeTab)
    {
        if (!workspaceId.HasValue)
        {
            return RedirectToPage("/Workspaces/Index");
        }

        var workspace = await _workspaceQueryService.GetWorkspaceByIdAsync(workspaceId.Value);
        if (workspace == null)
        {
            return NotFound();
        }

        WorkspaceId = workspace.Id;
        WorkspaceName = workspace.Name;

        var userId = User.GetUserId();
        var canEdit = userId.HasValue && await HttpContext.RequestServices
            .GetRequiredService<WorkspaceAuthorizationService>()
            .CanEditWorkspace(userId.Value, workspace.Id);

        ViewData["WorkspaceId"] = workspace.Id.ToString("D");
        ViewData["WorkspaceName"] = workspace.Name;
        ViewData["ActiveTab"] = activeTab;
        ViewData["CanEdit"] = canEdit;

        return null;
    }
}
