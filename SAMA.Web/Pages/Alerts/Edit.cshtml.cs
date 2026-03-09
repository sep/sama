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
public class EditModel(WorkspaceQueryService _workspaceQueryService, ChannelQueryService _channelQueryService, CheckQueryService _checkQueryService, AlertQueryService _alertQueryService, AlertCommandService _alertCommandService)
    : WorkspacePageModel(_workspaceQueryService)
{
    public Guid CheckId { get; set; }

    public string CheckName { get; set; } = string.Empty;

    public List<ChannelListItemViewModel> AvailableChannels { get; set; } = [];


    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public Guid Id { get; set; }

        public Guid CheckId { get; set; }

        [Required(ErrorMessage = "Alert rule name is required")]
        [StringLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
        public string Name { get; set; } = string.Empty;

        public bool TriggerOnWarn { get; set; }

        public bool TriggerOnDown { get; set; }

        [Required(ErrorMessage = "Failure threshold is required")]
        [Range(AlertConstants.MinFailureThreshold, AlertConstants.MaxFailureThreshold, ErrorMessage = "Threshold must be between {1} and {2}")]
        public int FailureThreshold { get; set; }

        public bool SendRecoveryNotification { get; set; }

        public bool Enabled { get; set; }

        public List<Guid> SelectedChannelIds { get; set; } = [];
    }

    public async Task<IActionResult> OnGetAsync(Guid? id)
    {
        if (!id.HasValue)
        {
            return NotFound();
        }

        var alert = await _alertQueryService.GetAlertForEditAsync(id.Value);
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

        Input = new InputModel
        {
            Id = alert.Id,
            CheckId = alert.CheckId,
            Name = alert.Name,
            TriggerOnWarn = alert.TriggerOnWarn,
            TriggerOnDown = alert.TriggerOnDown,
            FailureThreshold = alert.FailureThreshold,
            SendRecoveryNotification = alert.SendRecoveryNotification,
            Enabled = alert.Enabled,
            SelectedChannelIds = alert.SelectedChannelIds
        };

        await PopulateAvailableChannelsAsync(WorkspaceId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            var check = await _checkQueryService.GetCheckBasicInfoAsync(Input.CheckId);
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

            await PopulateAvailableChannelsAsync(check.WorkspaceId);
            return Page();
        }

        if (!Input.TriggerOnWarn && !Input.TriggerOnDown)
        {
            ModelState.AddModelError(string.Empty, "Alert rule must trigger on at least Warn or Down status.");

            var check = await _checkQueryService.GetCheckBasicInfoAsync(Input.CheckId);
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

            await PopulateAvailableChannelsAsync(check.WorkspaceId);
            return Page();
        }

        var updateResult = await _alertCommandService.UpdateAlertAsync(
            Input.Id,
            Input.Name,
            Input.TriggerOnWarn,
            Input.TriggerOnDown,
            Input.FailureThreshold,
            Input.SendRecoveryNotification,
            Input.Enabled,
            Input.SelectedChannelIds,
            User.Identity?.Name ?? "System");

        if (!updateResult.Success)
        {
            ModelState.AddModelError(string.Empty, updateResult.ErrorMessage ?? "Failed to update alert rule.");

            var check = await _checkQueryService.GetCheckBasicInfoAsync(Input.CheckId);
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

            await PopulateAvailableChannelsAsync(check.WorkspaceId);
            return Page();
        }

        if (updateResult.ShouldTriggerCheck)
        {
            var channelMessage = updateResult.ChannelCount > 0
                ? $"{updateResult.ChannelCount} channel(s)"
                : $"all {updateResult.AllChannelsCount} workspace channel(s)";

            TempData["SuccessMessage"] = $"Alert rule '{Input.Name}' updated successfully with {channelMessage}. Check is being executed immediately to send initial notification.";
        }
        else
        {
            var channelMessage = updateResult.ChannelCount > 0
                ? $"{updateResult.ChannelCount} channel(s)"
                : "all workspace channels";

            TempData["SuccessMessage"] = $"Alert rule '{Input.Name}' updated successfully with {channelMessage}.";
        }

        return RedirectToPage("Index", new { checkId = Input.CheckId });
    }

    private async Task PopulateAvailableChannelsAsync(Guid workspaceId)
    {
        AvailableChannels = await _channelQueryService.GetChannelsForWorkspaceAsync(workspaceId);
    }
}
