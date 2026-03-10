using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SAMA.Data.Entities;
using SAMA.Web.Constants;
using SAMA.Web.Services;

namespace SAMA.Web.Pages.Account;

[AllowAnonymous]
public class LogoutModel(
    SignInManager<ApplicationUser> signInManager,
    OidcAuthenticationService oidcService,
    ILogger<LogoutModel> logger)
    : PageModel
{
    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        await signInManager.SignOutAsync();
        logger.LogInformation("User logged out");

        // If OIDC is enabled, also sign out from the OIDC provider
        if (oidcService.IsOidcEnabled)
        {
            await HttpContext.SignOutAsync(AuthConstants.OidcSource);
        }

        if (returnUrl != null)
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToPage("/Index");
    }
}
