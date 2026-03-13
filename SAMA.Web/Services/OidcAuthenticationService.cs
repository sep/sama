using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Web.Constants;

namespace SAMA.Web.Services;

public class OidcAuthenticationService(
    GlobalSettingsService _globalSettings,
    GroupMappingSyncService _groupMappingService,
    IServiceProvider _serviceProvider,
    ILogger<OidcAuthenticationService> _logger)
{
    public virtual bool IsOidcEnabled => _globalSettings.OidcEnabled
        && !string.IsNullOrWhiteSpace(_globalSettings.OidcAuthority)
        && !string.IsNullOrWhiteSpace(_globalSettings.OidcClientId);

    public async Task<ApplicationUser> ProvisionOrUpdateUserAsync(ClaimsPrincipal principal)
    {
        var emailClaimType = _globalSettings.OidcEmailClaimType;
        var email = principal.FindFirstValue(emailClaimType);

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException($"OIDC token does not contain a '{emailClaimType}' claim. Check the Email Claim Type setting or ensure the provider is configured to include this claim.");
        }

        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? email;

        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<SamaDbContext>();

        var user = await userManager.FindByEmailAsync(email);

        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            var createResult = await userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to create OIDC user {Email}: {Errors}", email, errors);
                throw new InvalidOperationException($"Failed to provision OIDC user: {errors}");
            }

            var loginResult = await userManager.AddLoginAsync(user, new UserLoginInfo(AuthConstants.OidcSource, subject, AuthConstants.OidcSource));
            if (!loginResult.Succeeded)
            {
                var loginErrors = string.Join(", ", loginResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to add OIDC login for new user {Email}: {Errors}", email, loginErrors);
                throw new InvalidOperationException($"Failed to link OIDC login: {loginErrors}");
            }

            _logger.LogInformation("JIT provisioned new user for OIDC login: {Email}", email);
        }
        else
        {
            var logins = await userManager.GetLoginsAsync(user);
            if (!logins.Any(l => l.LoginProvider == AuthConstants.OidcSource))
            {
                var loginResult = await userManager.AddLoginAsync(user, new UserLoginInfo(AuthConstants.OidcSource, subject, AuthConstants.OidcSource));
                if (!loginResult.Succeeded)
                {
                    var loginErrors = string.Join(", ", loginResult.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to add OIDC login for existing user {Email}: {Errors}", email, loginErrors);
                    throw new InvalidOperationException($"Failed to link OIDC login: {loginErrors}");
                }
            }
        }

        var groups = ExtractGroups(principal);
        await _groupMappingService.ApplyGroupMappingsAsync(dbContext, userManager, user, groups, AuthConstants.OidcSource);

        return user;
    }

    private List<string> ExtractGroups(ClaimsPrincipal principal)
    {
        var groupClaimType = _globalSettings.OidcGroupClaimType;
        if (string.IsNullOrWhiteSpace(groupClaimType))
        {
            return [];
        }

        return principal.FindAll(groupClaimType)
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();
    }
}
