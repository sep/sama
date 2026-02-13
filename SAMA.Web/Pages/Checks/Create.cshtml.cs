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
public class CreateModel(
    WorkspaceQueryService _workspaceQueryService,
    CheckConfigurationService _checkConfigService,
    CheckCommandService _checkCommandService,
    GlobalSettingsService _globalSettings)
    : WorkspacePageModel(_workspaceQueryService)
{
    public List<SelectListItem> CheckTypes { get; set; } = [];

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel : CheckInputBase
    {
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
        public string Schedule { get; set; } = "60";

        [Required(ErrorMessage = "Timeout is required")]
        [Range(5, 3600, ErrorMessage = "Timeout must be between 5 seconds and 1 hour")]
        public int TimeoutSeconds { get; set; }

        public bool Enabled { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(Guid? workspaceId)
    {
        var result = await LoadWorkspaceContextAsync(workspaceId, "Checks");
        if (result != null)
        {
            return result;
        }

        Input.WorkspaceId = WorkspaceId;
        Input.TimeoutSeconds = _globalSettings.DefaultCheckTimeoutSeconds;
        PopulateCheckTypes();
        return Page();
    }

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

        var checkId = await _checkCommandService.CreateCheckAsync(
            Input.WorkspaceId,
            Input.Name,
            Input.Description,
            Input.CheckType,
            Input.Schedule,
            Input.TimeoutSeconds,
            configuration,
            Input.Enabled,
            User.Identity?.Name ?? "System");

        TempData["SuccessMessage"] = $"Check '{Input.Name}' created successfully.";

        return RedirectToPage("Index", new { workspaceId = Input.WorkspaceId });
    }

    private void PopulateCheckTypes()
    {
        CheckTypes = SharedConstants.CheckTypes.AllCheckTypes.Select(ct => new SelectListItem
        {
            Value = ct,
            Text = SharedConstants.CheckTypes.GetFullDisplayName(ct)
        }).ToList();
    }
}
