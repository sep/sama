using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SAMA.Web.Authorization;
using SAMA.Web.Extensions;
using SAMA.Web.Models;
using SAMA.Web.Pages.Shared;
using SAMA.Web.Services;
using SAMA.Web.Services.Commands;
using SAMA.Web.Services.Queries;

using SharedConstants = SAMA.Shared.Constants;

namespace SAMA.Web.Pages.Checks;

[RequireWorkspaceEditAccess]
public class EditModel(WorkspaceQueryService _workspaceQueryService, CheckQueryService _checkQueryService, CheckConfigurationService _checkConfigService, CheckCommandService _checkCommandService)
    : WorkspacePageModel(_workspaceQueryService)
{
    public List<SelectListItem> CheckTypes { get; set; } = [];

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel : CheckInputBase
    {
        public Guid Id { get; set; }

        public Guid WorkspaceId { get; set; }

        [Required(ErrorMessage = "Check name is required")]
        [StringLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Check type is required")]
        public override string CheckType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Schedule is required")]
        [StringLength(100, ErrorMessage = "Schedule cannot exceed 100 characters")]
        public string Schedule { get; set; } = string.Empty;

        [Required(ErrorMessage = "Timeout is required")]
        [Range(5, 3600, ErrorMessage = "Timeout must be between 5 seconds and 1 hour")]
        public int TimeoutSeconds { get; set; }

        public bool Enabled { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid? id)
    {
        if (!id.HasValue)
        {
            return NotFound();
        }

        var check = await _checkQueryService.GetCheckForEditAsync(id.Value);

        if (check == null)
        {
            return NotFound();
        }

        var result = await LoadWorkspaceContextAsync(check.WorkspaceId, "Checks");
        if (result != null)
        {
            return result;
        }

        Input = new InputModel
        {
            Id = check.Id,
            WorkspaceId = check.WorkspaceId,
            Name = check.Name,
            Description = check.Description,
            CheckType = check.CheckType,
            Schedule = check.Schedule,
            TimeoutSeconds = check.TimeoutSeconds,
            Enabled = check.Enabled
        };

        _checkConfigService.PopulateFromConfiguration(Input, check.ConfigurationJson);

        PopulateCheckTypes();
        return Page();
    }

    /// <summary>
    /// HTMX handler to load configuration fields based on selected check type.
    /// </summary>
    /// <param name="checkType">Check type string</param>
    /// <returns>Edit page with configuration fields for the selected check type</returns>
    public IActionResult OnGetConfigFields(string checkType)
    {
        Input.CheckType = checkType;
        return Page();
    }

    public IActionResult OnGetSchedulePreview([FromQuery(Name = "Input.Schedule")] string schedule)
    {
        if (string.IsNullOrWhiteSpace(schedule))
        {
            return Content("--");
        }

        var error = ScheduleExtensions.ValidateSchedule(schedule);
        if (error != null)
        {
            return Content($"⚠ {error}");
        }

        return Content($"⮩ {ScheduleExtensions.ToDisplayString(schedule)}");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        _checkConfigService.ValidateConfiguration(ModelState, Input);

        var scheduleError = ScheduleExtensions.ValidateSchedule(Input.Schedule);
        if (scheduleError != null)
        {
            ModelState.AddModelError("Input.Schedule", scheduleError);
        }

        if (!ModelState.IsValid)
        {
            var result = await LoadWorkspaceContextAsync(Input.WorkspaceId, "Checks");
            if (result != null)
            {
                return result;
            }

            PopulateCheckTypes();
            return Page();
        }

        var configuration = _checkConfigService.BuildConfiguration(Input);

        var updated = await _checkCommandService.UpdateCheckAsync(
            Input.Id,
            Input.Name,
            Input.Description,
            Input.CheckType,
            Input.Schedule,
            Input.TimeoutSeconds,
            configuration,
            Input.Enabled,
            User.Identity?.Name ?? "System");

        if (!updated)
        {
            return BadRequest();
        }

        TempData["SuccessMessage"] = Input.Enabled
            ? $"Check '{Input.Name}' was updated successfully and will be checked shortly."
            : $"Check '{Input.Name}' was updated successfully.";

        return RedirectToPage("Index", new { workspaceId = Input.WorkspaceId });
    }

    private void PopulateCheckTypes()
    {
        CheckTypes = SharedConstants.CheckTypes.AllCheckTypes.Select(ct =>
            new SelectListItem
            {
                Value = ct,
                Text = SharedConstants.CheckTypes.GetFullDisplayName(ct)
            }).ToList();
    }
}
