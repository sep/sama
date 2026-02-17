using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SAMA.Web.Constants;
using SAMA.Web.Services;

namespace SAMA.Web.Pages.Admin.Settings;

[Authorize(Roles = AuthConstants.AdminRole)]
public class LdapModel(
    GlobalSettingsService _globalSettings,
    LdapAuthenticationService _ldapService,
    ILogger<LdapModel> _logger) : PageModel
{
    [BindProperty]
    public LdapInputModel LdapInput { get; set; } = new();

    [BindProperty]
    public TestLoginInputModel TestLoginInput { get; set; } = new();

    public bool HasExistingBindPassword { get; set; }

    public class TestLoginInputModel
    {
        [Required]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Password")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }

    public class LdapInputModel
    {
        [Display(Name = "Enable LDAP")]
        public bool Enabled { get; set; }

        [Display(Name = "LDAP Server Host")]
        public string Host { get; set; } = string.Empty;

        [Display(Name = "Port")]
        [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
        public int Port { get; set; } = 389;

        [Display(Name = "Use Implicit SSL/TLS")]
        public bool UseSsl { get; set; }

        [Display(Name = "Use StartTLS")]
        public bool UseStartTls { get; set; }

        [Display(Name = "Bind DN")]
        public string BindDn { get; set; } = string.Empty;

        [Display(Name = "Bind Password")]
        [DataType(DataType.Password)]
        public string BindPassword { get; set; } = string.Empty;

        [Display(Name = "Bind Template")]
        public string BindTemplate { get; set; } = string.Empty;

        [Display(Name = "User Search Base DN")]
        public string SearchBase { get; set; } = string.Empty;

        [Display(Name = "User Search Filter")]
        public string SearchFilter { get; set; } = "(&(objectClass=user)(|(sAMAccountName={0})(userPrincipalName={0})))";

        [Display(Name = "Group Search Base DN")]
        public string GroupSearchBase { get; set; } = string.Empty;

        [Display(Name = "Group Search Filter")]
        public string GroupSearchFilter { get; set; } = "(&(objectClass=group)(member={0}))";

        [Display(Name = "Custom Root CA Certificate")]
        public string CustomRootCa { get; set; } = string.Empty;
    }

    private static bool IsValidLdapFilterFormat(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter) || !filter.Contains("{0}"))
        {
            return false;
        }

        try
        {
            // Test the format string with a placeholder value to ensure it's valid
            // This will throw FormatException for various format string issues:
            // - Additional placeholders like {1}, {2}, etc. when only one argument is provided
            // - Invalid format specifiers for the given argument type
            _ = string.Format(filter, "test");
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public void OnGet()
    {
        LoadCurrentSettings();
    }

    public IActionResult OnPost()
    {
        if (LdapInput.Enabled && string.IsNullOrWhiteSpace(LdapInput.SearchBase))
        {
            TempData["LdapError"] = "User Search Base DN is required when LDAP is enabled.";
            return RedirectToPage();
        }

        // Validate SearchFilter
        if (!IsValidLdapFilterFormat(LdapInput.SearchFilter))
        {
            TempData["LdapError"] = "User Search Filter is invalid. It must be non-empty and contain at least one {0} placeholder for the username.";
            return RedirectToPage();
        }

        // Validate GroupSearchFilter if GroupSearchBase is configured
        if (!string.IsNullOrWhiteSpace(LdapInput.GroupSearchBase) && !IsValidLdapFilterFormat(LdapInput.GroupSearchFilter))
        {
            TempData["LdapError"] = "Group Search Filter is invalid. It must be non-empty and contain at least one {0} placeholder for the user DN.";
            return RedirectToPage();
        }

        try
        {
            _globalSettings.LdapEnabled = LdapInput.Enabled;
            _globalSettings.LdapHost = LdapInput.Host;
            _globalSettings.LdapPort = LdapInput.Port;
            _globalSettings.LdapUseSsl = LdapInput.UseSsl;
            _globalSettings.LdapUseStartTls = LdapInput.UseStartTls;
            _globalSettings.LdapBindDn = LdapInput.BindDn;
            _globalSettings.LdapBindTemplate = LdapInput.BindTemplate;
            _globalSettings.LdapSearchBase = LdapInput.SearchBase;
            _globalSettings.LdapSearchFilter = LdapInput.SearchFilter;
            _globalSettings.LdapGroupSearchBase = LdapInput.GroupSearchBase;
            _globalSettings.LdapGroupSearchFilter = LdapInput.GroupSearchFilter;

            if (!string.IsNullOrEmpty(LdapInput.BindPassword))
            {
                _globalSettings.LdapBindPassword = LdapInput.BindPassword;
            }
            else if (Request.Form["ClearBindPassword"].ToString() == "true")
            {
                _globalSettings.LdapBindPassword = string.Empty;
            }

            _globalSettings.LdapCustomRootCa = LdapInput.CustomRootCa;

            _logger.LogInformation(
                "LDAP settings updated by {User}",
                User.Identity?.Name ?? "Unknown");

            _globalSettings.ClearCache();
            TempData["LdapSuccess"] = "LDAP settings saved successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating LDAP settings");
            TempData["LdapError"] = "An error occurred while saving LDAP settings.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTestLoginAsync()
    {
        ModelState.Clear();

        if (string.IsNullOrWhiteSpace(TestLoginInput.Username) || string.IsNullOrWhiteSpace(TestLoginInput.Password))
        {
            TempData["LdapTestSuccess"] = false;
            TempData["LdapTestMessage"] = "Username and password are required.";
            return RedirectToPage();
        }

        var result = await _ldapService.AuthenticateAsync(TestLoginInput.Username, TestLoginInput.Password, showDetailedErrors: true);

        TempData["LdapTestSuccess"] = result.Succeeded;

        if (result.Succeeded)
        {
            TempData["LdapTestMessage"] = $"Login successful. DN: \"{result.UserDn}\", Email: \"{result.Email}\", Display Name: \"{result.DisplayName}\".";
            if (result.Groups is { Count: > 0 })
            {
                TempData["LdapTestGroups"] = string.Join("\n", result.Groups);
            }
            else
            {
                TempData["LdapTestGroups"] = string.Empty;
            }

            if (result.Warnings is { Count: > 0 })
            {
                TempData["LdapTestWarnings"] = string.Join("\n", result.Warnings);
            }
        }
        else
        {
            TempData["LdapTestMessage"] = result.ErrorMessage ?? "Authentication failed.";
        }

        return RedirectToPage();
    }

    private void LoadCurrentSettings()
    {
        LdapInput.Enabled = _globalSettings.LdapEnabled;
        LdapInput.Host = _globalSettings.LdapHost;
        LdapInput.Port = _globalSettings.LdapPort;
        LdapInput.UseSsl = _globalSettings.LdapUseSsl;
        LdapInput.UseStartTls = _globalSettings.LdapUseStartTls;
        LdapInput.BindDn = _globalSettings.LdapBindDn;
        LdapInput.BindTemplate = _globalSettings.LdapBindTemplate;
        LdapInput.SearchBase = _globalSettings.LdapSearchBase;
        LdapInput.SearchFilter = _globalSettings.LdapSearchFilter;
        LdapInput.GroupSearchBase = _globalSettings.LdapGroupSearchBase;
        LdapInput.GroupSearchFilter = _globalSettings.LdapGroupSearchFilter;
        HasExistingBindPassword = !string.IsNullOrEmpty(_globalSettings.LdapBindPassword);
        LdapInput.CustomRootCa = _globalSettings.LdapCustomRootCa;
    }
}
