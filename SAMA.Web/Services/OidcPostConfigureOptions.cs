using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
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

        if (!_globalSettings.OidcEnabled
            || string.IsNullOrWhiteSpace(_globalSettings.OidcAuthority)
            || string.IsNullOrWhiteSpace(_globalSettings.OidcClientId))
        {
            // When disabled or misconfigured, set authority to a placeholder so the handler doesn't crash,
            // but it will never be challenged
            options.Authority = "https://localhost";
            options.ClientId = "disabled";
            options.MetadataAddress = string.Empty;
            options.Configuration = new OpenIdConnectConfiguration();
            return;
        }

        var authority = _globalSettings.OidcAuthority;
        var isHttpAuthority = authority.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

        // Clear placeholder configuration and rebuild a real ConfigurationManager from Authority.
        // Microsoft's built-in PostConfigure already ran and won't re-create the manager,
        // so we must build it ourselves.
        var metadataAddress = authority.TrimEnd('/') + "/.well-known/openid-configuration";
        options.Configuration = null;
        options.MetadataAddress = metadataAddress;
        options.RequireHttpsMetadata = !isHttpAuthority;
        var httpClient = options.Backchannel;
        var retriever = new HttpDocumentRetriever(httpClient) { RequireHttps = !isHttpAuthority };
        options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            retriever);

        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.Authority = authority;
        options.ClientId = _globalSettings.OidcClientId;
        options.ClientSecret = _globalSettings.OidcClientSecret;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.CallbackPath = "/signin-oidc";
        options.MapInboundClaims = false;

        options.Scope.Clear();
        foreach (var scope in _globalSettings.OidcScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            options.Scope.Add(scope);
        }

        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.ValidAudience = _globalSettings.OidcClientId;
    }
}
