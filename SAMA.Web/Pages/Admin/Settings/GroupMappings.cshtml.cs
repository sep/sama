using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SAMA.Web.Constants;
using SAMA.Web.Models;
using SAMA.Web.Services.Commands;
using SAMA.Web.Services.Queries;

namespace SAMA.Web.Pages.Admin.Settings;

[Authorize(Roles = AuthConstants.AdminRole)]
public class GroupMappingsModel(
    GroupMappingQueryService _groupMappingQuery,
    GroupMappingCommandService _groupMappingCommand,
    WorkspaceQueryService _workspaceQuery,
    ILogger<GroupMappingsModel> _logger) : PageModel
{
    public List<GroupMappingViewModel> Mappings { get; set; } = [];

    public List<SelectListItem> AvailableWorkspaces { get; set; } = [];

    [BindProperty]
    public AddMappingInputModel Input { get; set; } = new();

    public class AddMappingInputModel
    {
        [Display(Name = "Workspace")]
        public Guid? WorkspaceId { get; set; }

        [Required(ErrorMessage = "Identity provider is required")]
        [Display(Name = "Identity Provider")]
        public string IdentityProvider { get; set; } = "LDAP";

        [Required(ErrorMessage = "External group ID is required")]
        [Display(Name = "External Group (DN or Name)")]
        [MaxLength(500)]
        public string ExternalGroupId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Role is required")]
        [Display(Name = "Role")]
        public string Role { get; set; } = AuthConstants.EditorRole;
    }

    public async Task OnGetAsync()
    {
        await LoadPageDataAsync();
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadPageDataAsync();
            return Page();
        }

        // Global Admin mapping must have null WorkspaceId and Admin role
        if (Input.WorkspaceId == null && Input.Role != AuthConstants.AdminRole)
        {
            TempData["Error"] = "Global mappings (no workspace) can only assign the Admin role.";
            return RedirectToPage();
        }

        // Workspace mappings should not assign Admin role
        if (Input.WorkspaceId != null && Input.Role == AuthConstants.AdminRole)
        {
            TempData["Error"] = "The Admin role can only be assigned as a global mapping (no workspace selected).";
            return RedirectToPage();
        }

        if (await _groupMappingQuery.MappingExistsAsync(Input.WorkspaceId, Input.IdentityProvider, Input.ExternalGroupId))
        {
            TempData["Error"] = "A mapping for this group, provider, and workspace already exists.";
            return RedirectToPage();
        }

        try
        {
            await _groupMappingCommand.CreateMappingAsync(
                Input.WorkspaceId,
                Input.IdentityProvider,
                Input.ExternalGroupId,
                Input.Role,
                User.Identity?.Name ?? "Unknown");

            TempData["Success"] = "Group mapping added successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating group mapping");
            TempData["Error"] = "An error occurred while creating the group mapping.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        try
        {
            var deleted = await _groupMappingCommand.DeleteMappingAsync(
                id,
                User.Identity?.Name ?? "Unknown");

            TempData["Success"] = deleted
                ? "Group mapping deleted."
                : "Group mapping not found.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting group mapping {MappingId}", id);
            TempData["Error"] = "An error occurred while deleting the group mapping.";
        }

        return RedirectToPage();
    }

    private async Task LoadPageDataAsync()
    {
        Mappings = await _groupMappingQuery.GetAllMappingsAsync();

        var workspaces = await _workspaceQuery.GetWorkspacesAsync();
        AvailableWorkspaces =
        [
            new SelectListItem("— Global Admin —", ""),
            .. workspaces.Select(w => new SelectListItem(w.Name, w.Id.ToString())),
        ];
    }
}
