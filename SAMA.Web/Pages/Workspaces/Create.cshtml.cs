using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SAMA.Web.Constants;
using SAMA.Web.Services;
using SAMA.Web.Services.Commands;

namespace SAMA.Web.Pages.Workspaces;

[Authorize(Roles = AuthConstants.AdminRole)]
public class CreateModel(
    WorkspaceCommandService _workspaceCommandService,
    MarkdownService _markdownService)
    : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string DashboardMessagePreview { get; set; } = string.Empty;

    public class InputModel
    {
        [Required(ErrorMessage = "Workspace name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        [StringLength(2000, ErrorMessage = "Dashboard message cannot exceed 2000 characters")]
        public string? DashboardMessage { get; set; }

        public bool IsPublic { get; set; }
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            DashboardMessagePreview = _markdownService.RenderToHtml(Input.DashboardMessage);
            return Page();
        }

        var workspaceId = await _workspaceCommandService.CreateWorkspaceAsync(
            Input.Name,
            Input.Description,
            Input.DashboardMessage,
            Input.IsPublic,
            User.Identity?.Name ?? "System");

        TempData["SuccessMessage"] = $"Workspace '{Input.Name}' created successfully.";

        return RedirectToPage("Index");
    }

    public IActionResult OnPostPreviewMarkdown([FromForm(Name = $"{nameof(Input)}.{nameof(InputModel.DashboardMessage)}")] string? markdown)
    {
        var html = _markdownService.RenderToHtml(markdown);
        return Content(html, "text/html");
    }
}
