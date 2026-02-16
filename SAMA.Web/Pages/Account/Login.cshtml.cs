using System.ComponentModel.DataAnnotations;
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
public class LoginModel(
    SignInManager<ApplicationUser> signInManager,
    LdapAuthenticationService ldapService,
    ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public bool LdapEnabled => ldapService.IsLdapEnabled;

    public class InputModel
    {
        [Required(ErrorMessage = "Email or username is required")]
        [Display(Name = "Email or Username")]
        public string EmailOrUsername { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        // Clear the existing external cookie to ensure a clean login process
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var identifier = Input.EmailOrUsername.Trim();

        // If LDAP is enabled, try LDAP authentication first
        if (ldapService.IsLdapEnabled)
        {
            var ldapResult = await ldapService.AuthenticateAsync(identifier, Input.Password);

            if (ldapResult.Succeeded)
            {
                try
                {
                    var user = await ldapService.ProvisionOrUpdateUserAsync(ldapResult);
                    await signInManager.SignInAsync(user, Input.RememberMe);
                    logger.LogInformation("User {Email} logged in via LDAP", ldapResult.Email);
                    return LocalRedirect(returnUrl);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during LDAP login for user {Identifier}", identifier);
                    ModelState.AddModelError(string.Empty, "An error occurred during login. Please try again.");
                    return Page();
                }
            }
        }

        // Fall back to local password authentication
        // Block local login for LDAP-sourced users
        var localUser = await signInManager.UserManager.FindByEmailAsync(identifier);
        if (localUser != null)
        {
            var logins = await signInManager.UserManager.GetLoginsAsync(localUser);
            if (logins.Any(l => l.LoginProvider == AuthConstants.LdapSource))
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }
        }

        var result = await signInManager.PasswordSignInAsync(identifier, Input.Password, Input.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            logger.LogInformation("User {Email} logged in", identifier);
            return LocalRedirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            logger.LogWarning("Login attempt for locked out account: {Email}", identifier);
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return Page();
    }
}
