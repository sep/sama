using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SAMA.Web.Constants;
using SAMA.Web.Models;
using SAMA.Web.Services.Commands;
using SAMA.Web.Services.Queries;

namespace SAMA.Web.Pages.Admin.Users;

[Authorize(Roles = AuthConstants.AdminRole)]
public class ResetPasswordModel(
    UserQueryService _userQueryService,
    UserCommandService _userCommandService)
    : PageModel
{
    public string UserEmail { get; set; } = string.Empty;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public Guid UserId { get; set; }

        [Required(ErrorMessage = "New password is required")]
        [StringLength(100, MinimumLength = 14, ErrorMessage = "Password must be at least 14 characters")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password confirmation is required")]
        [Compare(nameof(NewPassword), ErrorMessage = "Password and confirmation password do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(Guid? id)
    {
        if (!id.HasValue)
        {
            return NotFound();
        }

        var user = await _userQueryService.GetUserByIdAsync(id.Value);
        if (user == null)
        {
            return NotFound();
        }

        if (user.IsExternalUser)
        {
            return RedirectToPage("Details", new { id });
        }

        UserEmail = user.Email;
        Input.UserId = user.Id;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var existingUser = await _userQueryService.GetUserByIdAsync(Input.UserId);
        if (existingUser?.IsExternalUser == true)
        {
            return RedirectToPage("Details", new { id = Input.UserId });
        }

        if (!ModelState.IsValid)
        {
            if (existingUser != null)
            {
                UserEmail = existingUser.Email;
            }
            return Page();
        }

        try
        {
            await _userCommandService.ResetPasswordAsync(
                Input.UserId,
                Input.NewPassword,
                User.Identity?.Name ?? "System");

            TempData["SuccessMessage"] = "Password has been reset successfully.";
            return RedirectToPage("Details", new { id = Input.UserId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);

            if (existingUser != null)
            {
                UserEmail = existingUser.Email;
            }

            return Page();
        }
    }
}
