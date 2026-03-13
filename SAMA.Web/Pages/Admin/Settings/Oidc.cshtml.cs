using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SAMA.Web.Constants;
using SAMA.Web.Services;

namespace SAMA.Web.Pages.Admin.Settings;

[Authorize(Roles = AuthConstants.AdminRole)]
public class OidcModel(
    GlobalSettingsService _globalSettings,
    IOptionsMonitorCache<OpenIdConnectOptions> _oidcOptionsCache,
    ILogger<OidcModel> _logger) : PageModel
{
    [BindProperty]
    public OidcInputModel OidcInput { get; set; } = new();

    public bool HasExistingClientSecret { get; set; }

    public class OidcInputModel
    {
        [Display(Name = "Enable OIDC")]
        public bool Enabled { get; set; }

        [Display(Name = "Authority URL")]
        public string Authority { get; set; } = string.Empty;

        [Display(Name = "Client ID")]
        public string ClientId { get; set; } = string.Empty;

        [Display(Name = "Client Secret")]
        [DataType(DataType.Password)]
        public string ClientSecret { get; set; } = string.Empty;

        [Display(Name = "Scopes")]
        public string Scopes { get; set; } = "openid profile email";

        [Display(Name = "Email Claim Type")]
        public string EmailClaimType { get; set; } = "email";

        [Display(Name = "Group Claim Type")]
        public string GroupClaimType { get; set; } = "groups";

        [Display(Name = "Provider Name")]
        public string ProviderName { get; set; } = "OIDC";
    }

    public void OnGet()
    {
        LoadCurrentSettings();
    }

    public IActionResult OnPost()
    {
        if (OidcInput.Enabled)
        {
            if (string.IsNullOrWhiteSpace(OidcInput.Authority))
            {
                TempData["OidcError"] = "Authority URL is required when OIDC is enabled.";
                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(OidcInput.ClientId))
            {
                TempData["OidcError"] = "Client ID is required when OIDC is enabled.";
                return RedirectToPage();
            }

            if (!Uri.TryCreate(OidcInput.Authority, UriKind.Absolute, out var authorityUri)
                || (authorityUri.Scheme != "https" && authorityUri.Scheme != "http"))
            {
                TempData["OidcError"] = "Authority URL must be a valid HTTP or HTTPS URL.";
                return RedirectToPage();
            }
        }

        try
        {
            _globalSettings.OidcEnabled = OidcInput.Enabled;
            _globalSettings.OidcAuthority = OidcInput.Authority.TrimEnd('/');
            _globalSettings.OidcClientId = OidcInput.ClientId;
            _globalSettings.OidcScopes = string.IsNullOrWhiteSpace(OidcInput.Scopes)
                ? "openid profile email"
                : OidcInput.Scopes;
            _globalSettings.OidcEmailClaimType = string.IsNullOrWhiteSpace(OidcInput.EmailClaimType)
                ? "email"
                : OidcInput.EmailClaimType;
            _globalSettings.OidcGroupClaimType = string.IsNullOrWhiteSpace(OidcInput.GroupClaimType)
                ? "groups"
                : OidcInput.GroupClaimType;
            _globalSettings.OidcProviderName = string.IsNullOrWhiteSpace(OidcInput.ProviderName)
                ? "OIDC"
                : OidcInput.ProviderName;

            if (!string.IsNullOrEmpty(OidcInput.ClientSecret))
            {
                _globalSettings.OidcClientSecret = OidcInput.ClientSecret;
            }
            else if (Request.Form["ClearClientSecret"].ToString() == "true")
            {
                _globalSettings.OidcClientSecret = string.Empty;
            }

            _logger.LogInformation("OIDC settings updated by {User}", User.Identity?.Name ?? "Unknown");

            _globalSettings.ClearCache();
            _oidcOptionsCache.TryRemove(AuthConstants.OidcSource);
            TempData["OidcSuccess"] = "OIDC settings saved successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating OIDC settings");
            TempData["OidcError"] = "An error occurred while saving OIDC settings.";
        }

        return RedirectToPage();
    }

    private void LoadCurrentSettings()
    {
        OidcInput.Enabled = _globalSettings.OidcEnabled;
        OidcInput.Authority = _globalSettings.OidcAuthority;
        OidcInput.ClientId = _globalSettings.OidcClientId;
        OidcInput.Scopes = _globalSettings.OidcScopes;
        OidcInput.EmailClaimType = _globalSettings.OidcEmailClaimType;
        OidcInput.GroupClaimType = _globalSettings.OidcGroupClaimType;
        OidcInput.ProviderName = _globalSettings.OidcProviderName;
        HasExistingClientSecret = !string.IsNullOrEmpty(_globalSettings.OidcClientSecret);
    }
}
