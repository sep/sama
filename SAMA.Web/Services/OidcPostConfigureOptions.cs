using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using SAMA.Web.Constants;

namespace SAMA.Web.Services;

public class OidcPostConfigureOptions(GlobalSettingsService _globalSettings) : IPostConfigureOptions<OpenIdConnectOptions>
{
    public void PostConfigure(string? name, OpenIdConnectOptions options)
    {
        if (name != AuthConstants.OidcSource)
        {
            return;
        }

        if (!_globalSettings.OidcEnabled)
        {
            // When disabled, set authority to a placeholder so the handler doesn't crash,
            // but it will never be challenged
            options.Authority = "https://localhost";
            options.ClientId = "disabled";
            options.MetadataAddress = string.Empty;
            options.Configuration = new OpenIdConnectConfiguration();
            return;
        }

        options.Authority = _globalSettings.OidcAuthority;
        options.ClientId = _globalSettings.OidcClientId;
        options.ClientSecret = _globalSettings.OidcClientSecret;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.CallbackPath = "/signin-oidc";
        options.SignedOutCallbackPath = "/signout-callback-oidc";
        options.SaveTokens = true;
        options.MapInboundClaims = false;

        options.Scope.Clear();
        foreach (var scope in _globalSettings.OidcScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            options.Scope.Add(scope);
        }

        options.TokenValidationParameters.NameClaimType = "name";
    }
}
