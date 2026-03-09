using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SAMA.Web.Authorization;
using SAMA.Web.Constants;
using SAMA.Web.Models;
using SAMA.Web.Pages.Shared;
using SAMA.Web.Services.Commands;
using SAMA.Web.Services.Queries;

namespace SAMA.Web.Pages.Alerts;

[RequireWorkspaceEditAccess]
public class CreateModel(WorkspaceQueryService _workspaceQueryService, ChannelQueryService _channelQueryService, CheckQueryService _checkQueryService, AlertCommandService _alertCommandService)
    : WorkspacePageModel(_workspaceQueryService)
{
    public Guid CheckId { get; set; }

    public string CheckName { get; set; } = string.Empty;

    public List<ChannelListItemViewModel> AvailableChannels { get; set; } = [];


    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public Guid CheckId { get; set; }

        [Required(ErrorMessage = "Alert rule name is required")]
        [StringLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
        public string Name { get; set; } = string.Empty;

        public bool TriggerOnWarn { get; set; } = false;

        public bool TriggerOnDown { get; set; } = true;

        [Required(ErrorMessage = "Failure threshold is required")]
        [Range(AlertConstants.MinFailureThreshold, AlertConstants.MaxFailureThreshold, ErrorMessage = "Threshold must be between {1} and {2}")]
        public int FailureThreshold { get; set; } = 1;

        public bool SendRecoveryNotification { get; set; } = true;

        public bool Enabled { get; set; } = true;

        public List<Guid> SelectedChannelIds { get; set; } = [];
    }

    public async Task<IActionResult> OnGetAsync(Guid? checkId)
    {
        if (!checkId.HasValue)
        {
            return RedirectToPage("/Workspaces/Index");
        }

        var check = await _checkQueryService.GetCheckBasicInfoAsync(checkId.Value);
        if (check == null)
        {
            return NotFound();
        }

        var result = await LoadWorkspaceContextAsync(check.WorkspaceId, "Checks");
        if (result != null)
        {
            return result;
        }

        CheckId = check.Id;
        CheckName = check.Name;
        Input.CheckId = check.Id;

        await PopulateAvailableChannelsAsync(check.WorkspaceId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var check = await _checkQueryService.GetCheckBasicInfoAsync(Input.CheckId);
        if (check == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            var result = await LoadWorkspaceContextAsync(check.WorkspaceId, "Checks");
            if (result != null)
            {
                return result;
            }

            CheckId = check.Id;
            CheckName = check.Name;

            await PopulateAvailableChannelsAsync(check.WorkspaceId);
            return Page();
        }

        if (!Input.TriggerOnWarn && !Input.TriggerOnDown)
        {
            ModelState.AddModelError(string.Empty, "Alert rule must trigger on at least Warn or Down status.");

            var result = await LoadWorkspaceContextAsync(check.WorkspaceId, "Checks");
            if (result != null)
            {
                return result;
            }

            CheckId = check.Id;
            CheckName = check.Name;

            await PopulateAvailableChannelsAsync(check.WorkspaceId);
            return Page();
        }

        var createResult = await _alertCommandService.CreateAlertAsync(
            Input.CheckId,
            Input.Name,
            Input.TriggerOnWarn,
            Input.TriggerOnDown,
            Input.FailureThreshold,
            Input.SendRecoveryNotification,
            Input.Enabled,
            Input.SelectedChannelIds,
            User.Identity?.Name ?? "System");

        if (!createResult.Success)
        {
            ModelState.AddModelError(string.Empty, createResult.ErrorMessage ?? "Failed to create alert rule.");

            var result = await LoadWorkspaceContextAsync(check.WorkspaceId, "Checks");
            if (result != null)
            {
                return result;
            }

            CheckId = check.Id;
            CheckName = check.Name;

            await PopulateAvailableChannelsAsync(check.WorkspaceId);
            return Page();
        }

        if (createResult.ShouldTriggerCheck)
        {
            var channelMessage = createResult.ChannelCount > 0
                ? $"{createResult.ChannelCount} channel(s)"
                : $"all {createResult.AllChannelsCount} workspace channel(s)";

            TempData["SuccessMessage"] = $"Alert rule '{Input.Name}' created successfully with {channelMessage}. Check is being executed immediately to send initial notification.";
        }
        else
        {
            var channelMessage = createResult.ChannelCount > 0
                ? $"{createResult.ChannelCount} channel(s)"
                : "all workspace channels";

            TempData["SuccessMessage"] = $"Alert rule '{Input.Name}' created successfully with {channelMessage}.";
        }

        return RedirectToPage("Index", new { checkId = Input.CheckId });
    }

    private async Task PopulateAvailableChannelsAsync(Guid workspaceId)
    {
        AvailableChannels = await _channelQueryService.GetChannelsForWorkspaceAsync(workspaceId);
    }
}
