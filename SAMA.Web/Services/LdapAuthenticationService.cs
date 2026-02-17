using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Novell.Directory.Ldap;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Web.Constants;

namespace SAMA.Web.Services;

public class LdapAuthenticationService(
    GlobalSettingsService _globalSettings,
    IServiceProvider _serviceProvider,
    ILogger<LdapAuthenticationService> _logger)
{
    public virtual bool IsLdapEnabled => _globalSettings.LdapEnabled
        && !string.IsNullOrWhiteSpace(_globalSettings.LdapHost);

    public virtual async Task<LdapLoginResult> AuthenticateAsync(string username, string password, bool showDetailedErrors = false)
    {
        if (!IsLdapEnabled)
        {
            return LdapLoginResult.Fail(showDetailedErrors
                ? "LDAP is not enabled or host is not configured."
                : "LDAP is not configured.");
        }

        var host = _globalSettings.LdapHost;
        var port = _globalSettings.LdapPort;
        var useSsl = _globalSettings.LdapUseSsl;
        var useStartTls = !useSsl && _globalSettings.LdapUseStartTls;
        var bindDn = _globalSettings.LdapBindDn;
        var bindPassword = _globalSettings.LdapBindPassword;
        var bindTemplate = _globalSettings.LdapBindTemplate;
        var searchBase = _globalSettings.LdapSearchBase;
        var searchFilter = _globalSettings.LdapSearchFilter;
        var customRootCa = _globalSettings.LdapCustomRootCa;
        var useDirectBind = !string.IsNullOrWhiteSpace(bindTemplate);

        string? userDn;
        string? email;
        string? displayName;
        var memberOfGroups = new List<string>();

        if (useDirectBind)
        {
            userDn = ResolveBindDn(username, bindTemplate);

            LdapConnection? connection = null;
            try
            {
                connection = CreateConnection(useSsl, customRootCa);
                await connection.ConnectAsync(host, port);
            }
            catch (Exception ex)
            {
                connection?.Dispose();
                _logger.LogWarning(ex, "LDAP connection failed");
                return LdapLoginResult.Fail(showDetailedErrors
                    ? $"Connection to {host}:{port} failed: {FormatException(ex)}"
                    : "Invalid login attempt.");
            }

            try
            {
                if (useStartTls)
                {
                    await connection.StartTlsAsync();
                }
            }
            catch (Exception ex)
            {
                connection.Dispose();
                _logger.LogWarning(ex, "LDAP StartTLS failed");
                return LdapLoginResult.Fail(showDetailedErrors
                    ? $"StartTLS failed on {host}:{port}: {FormatException(ex)}"
                    : "Invalid login attempt.");
            }

            try
            {
                await connection.BindAsync(userDn, password);

                if (string.IsNullOrWhiteSpace(searchBase))
                {
                    _logger.LogWarning("LDAP direct bind succeeded but Search Base is not configured");
                    return LdapLoginResult.Fail(showDetailedErrors
                        ? "Direct bind succeeded but User Search Base DN is not configured. It is required to resolve user attributes."
                        : "LDAP is not fully configured. Contact your administrator.");
                }

                // Search for the real user entry to get the actual DN and attributes.
                // This is important when the bind template uses UPN (user@domain) or DOMAIN\user format,
                // which are not real DNs and can't be read directly.
                var filter = string.Format(searchFilter, EscapeLdapFilter(username));
                var searchResults = await connection.SearchAsync(
                    searchBase,
                    LdapConnection.ScopeSub,
                    filter,
                    ["dn", "mail", "displayName", "cn", "userPrincipalName", "memberOf"],
                    false);

                var entries = new List<LdapEntry>();
                while (await searchResults.HasMoreAsync())
                {
                    try
                    {
                        entries.Add(await searchResults.NextAsync());
                    }
                    catch (LdapReferralException)
                    {
                        // Ignore referrals
                    }
                }

                if (entries.Count == 0)
                {
                    _logger.LogWarning("LDAP direct bind succeeded but user not found in search for {Username}", username);
                    return LdapLoginResult.Fail(showDetailedErrors
                        ? $"Bind succeeded but user not found. Search base: '{searchBase}', filter: '{filter}'"
                        : "Invalid login attempt.");
                }

                var entry = entries[0];
                userDn = entry.Dn;
                email = GetAttribute(entry, "mail")
                    ?? GetAttribute(entry, "userPrincipalName");
                displayName = GetAttribute(entry, "displayName")
                    ?? GetAttribute(entry, "cn")
                    ?? username;
                memberOfGroups = GetMultiValuedAttribute(entry, "memberOf");

                if (string.IsNullOrWhiteSpace(email))
                {
                    _logger.LogWarning("LDAP user {Username} has no mail or userPrincipalName attribute", username);
                    return LdapLoginResult.Fail(showDetailedErrors
                        ? $"User '{userDn}' has no mail or userPrincipalName attribute in the directory."
                        : "Invalid login attempt.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LDAP direct bind failed for user {Username}", username);
                return LdapLoginResult.Fail(showDetailedErrors
                    ? $"Direct bind failed for DN '{userDn}': {FormatException(ex)}"
                    : "Invalid login attempt.");
            }
            finally
            {
                connection.Dispose();
            }
        }
        else
        {
            LdapConnection? connection = null;
            try
            {
                connection = CreateConnection(useSsl, customRootCa);
                await connection.ConnectAsync(host, port);
            }
            catch (Exception ex)
            {
                connection?.Dispose();
                _logger.LogWarning(ex, "LDAP connection failed");
                return LdapLoginResult.Fail(showDetailedErrors
                    ? $"Connection to {host}:{port} failed: {FormatException(ex)}"
                    : "LDAP authentication failed. Please try again.");
            }

            try
            {
                if (useStartTls)
                {
                    await connection.StartTlsAsync();
                }
            }
            catch (Exception ex)
            {
                connection.Dispose();
                _logger.LogWarning(ex, "LDAP StartTLS failed");
                return LdapLoginResult.Fail(showDetailedErrors
                    ? $"StartTLS failed on {host}:{port}: {FormatException(ex)}"
                    : "LDAP authentication failed. Please try again.");
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(bindDn))
                {
                    await connection.BindAsync(bindDn, bindPassword);
                }
            }
            catch (Exception ex)
            {
                connection.Dispose();
                _logger.LogWarning(ex, "LDAP service account bind failed");
                return LdapLoginResult.Fail(showDetailedErrors
                    ? $"Service account bind failed for DN '{bindDn}': {FormatException(ex)}"
                    : "LDAP authentication failed. Please try again.");
            }

            try
            {
                var filter = string.Format(searchFilter, EscapeLdapFilter(username));
                var searchResults = await connection.SearchAsync(
                    searchBase,
                    LdapConnection.ScopeSub,
                    filter,
                    ["dn", "mail", "displayName", "cn", "sAMAccountName", "userPrincipalName", "memberOf"],
                    false);

                var entries = new List<LdapEntry>();
                while (await searchResults.HasMoreAsync())
                {
                    try
                    {
                        entries.Add(await searchResults.NextAsync());
                    }
                    catch (LdapReferralException)
                    {
                        // Ignore referrals
                    }
                }

                if (entries.Count == 0)
                {
                    _logger.LogWarning("LDAP user not found for username: {Username}", username);
                    return LdapLoginResult.Fail(showDetailedErrors
                        ? $"User not found. Search base: '{searchBase}', filter: '{filter}'"
                        : "Invalid login attempt.");
                }

                var entry = entries[0];
                userDn = entry.Dn;
                email = GetAttribute(entry, "mail")
                    ?? GetAttribute(entry, "userPrincipalName");
                displayName = GetAttribute(entry, "displayName")
                    ?? GetAttribute(entry, "cn")
                    ?? username;

                if (string.IsNullOrWhiteSpace(email))
                {
                    _logger.LogWarning("LDAP user {Username} has no mail or userPrincipalName attribute", username);
                    return LdapLoginResult.Fail(showDetailedErrors
                        ? $"User '{userDn}' has no mail or userPrincipalName attribute in the directory."
                        : "LDAP authentication failed. User has no email address configured in the directory.");
                }
                memberOfGroups = GetMultiValuedAttribute(entry, "memberOf");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LDAP search failed for user {Username}", username);
                return LdapLoginResult.Fail(showDetailedErrors
                    ? $"User search failed (base: '{searchBase}'): {FormatException(ex)}"
                    : "LDAP authentication failed. Please try again.");
            }
            finally
            {
                connection.Dispose();
            }

            try
            {
                using var userConnection = CreateConnection(useSsl, customRootCa);
                await userConnection.ConnectAsync(host, port);

                if (useStartTls)
                {
                    await userConnection.StartTlsAsync();
                }

                await userConnection.BindAsync(userDn, password);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LDAP bind failed for user {Username}", username);
                return LdapLoginResult.Fail(showDetailedErrors
                    ? $"User bind failed for DN '{userDn}': {FormatException(ex)}"
                    : "Invalid login attempt.");
            }
        }

        // If group search base is configured, use only explicit group search;
        // otherwise fall back to memberOf attribute from the user entry
        var hasGroupSearchBase = !string.IsNullOrWhiteSpace(_globalSettings.LdapGroupSearchBase);
        var groups = new List<string>();
        var warnings = new List<string>();

        if (hasGroupSearchBase)
        {
            try
            {
                // Use service account if available, otherwise use the authenticated user's credentials
                var groupSearchBindDn = !string.IsNullOrWhiteSpace(bindDn) ? bindDn : userDn;
                var groupSearchBindPassword = !string.IsNullOrWhiteSpace(bindDn) ? bindPassword : password;
                groups = await GetGroupMembershipsAsync(host, port, useSsl, useStartTls, customRootCa, groupSearchBindDn, groupSearchBindPassword, userDn);
            }
            catch (LdapException ex)
            {
                _logger.LogWarning(ex, "LDAP group search failed for user {Username}", username);
                warnings.Add($"Group search failed: {FormatException(ex)}");
            }
        }
        else
        {
            groups = memberOfGroups;
        }

        _logger.LogInformation("LDAP authentication successful for user {Username} ({Email})", username, email);

        return LdapLoginResult.Success(userDn, email, displayName, groups, warnings);
    }

    public virtual async Task<ApplicationUser> ProvisionOrUpdateUserAsync(LdapLoginResult ldapResult)
    {
        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<SamaDbContext>();

        // Find existing user by email
        var user = await userManager.FindByEmailAsync(ldapResult.Email!);

        if (user == null)
        {
            // JIT provisioning: create new user
            user = new ApplicationUser
            {
                UserName = ldapResult.Email,
                Email = ldapResult.Email,
                EmailConfirmed = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            var createResult = await userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to create LDAP user {Email}: {Errors}", ldapResult.Email, errors);
                throw new InvalidOperationException($"Failed to provision LDAP user: {errors}");
            }

            await userManager.AddLoginAsync(user, new UserLoginInfo(AuthConstants.LdapSource, ldapResult.UserDn!, AuthConstants.LdapSource));
            _logger.LogInformation("JIT provisioned new user for LDAP login: {Email}", ldapResult.Email);
        }
        else
        {
            // Ensure existing user has the LDAP external login recorded
            var logins = await userManager.GetLoginsAsync(user);
            if (!logins.Any(l => l.LoginProvider == AuthConstants.LdapSource))
            {
                await userManager.AddLoginAsync(user, new UserLoginInfo(AuthConstants.LdapSource, ldapResult.UserDn!, AuthConstants.LdapSource));
            }
        }

        // Apply group mappings
        await ApplyGroupMappingsAsync(dbContext, userManager, user, ldapResult.Groups);

        return user;
    }

    private static LdapConnection CreateConnection(bool useSsl, string? customRootCaPem = null)
    {
        var hasCustomCa = !string.IsNullOrWhiteSpace(customRootCaPem);

        if (hasCustomCa)
        {
            var options = new LdapConnectionOptions();
            if (useSsl)
            {
                options.UseSsl();
            }

            options.ConfigureRemoteCertificateValidationCallback(
                (sender, certificate, chain, sslPolicyErrors) =>
                    ValidateWithCustomCa(certificate, chain, sslPolicyErrors, customRootCaPem!));

            return new LdapConnection(options);
        }

        var connection = new LdapConnection();

        if (useSsl)
        {
            connection.SecureSocketLayer = true;
        }

        return connection;
    }

    internal static bool ValidateWithCustomCa(
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors,
        string customRootCaPem)
    {
        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            return true;
        }

        // Only handle chain trust errors — throw for other errors so details propagate
        var remainingErrors = sslPolicyErrors & ~SslPolicyErrors.RemoteCertificateChainErrors;
        if (remainingErrors != SslPolicyErrors.None)
        {
            throw new AuthenticationException($"TLS certificate validation failed: {remainingErrors}");
        }

        if (certificate == null || chain == null)
        {
            throw new AuthenticationException("TLS certificate validation failed: certificate or chain is null.");
        }

        using var certToValidate = new X509Certificate2(certificate);
        using var customCaCert = X509Certificate2.CreateFromPem(customRootCaPem);

        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(customCaCert);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

        return chain.Build(certToValidate);
    }


    private static string? GetAttribute(LdapEntry entry, string attributeName)
    {
        return entry.GetStringValueOrDefault(attributeName);
    }

    private static List<string> GetMultiValuedAttribute(LdapEntry entry, string attributeName)
    {
        try
        {
            var attribute = entry.GetOrDefault(attributeName);
            if (attribute == null)
            {
                return [];
            }

            return [.. attribute.StringValueArray];
        }
        catch
        {
            return [];
        }
    }

    internal static string? ExtractCnFromDn(string dn)
    {
        if (string.IsNullOrWhiteSpace(dn))
        {
            return null;
        }

        // Parse "CN=GroupName,OU=Groups,DC=example,DC=com" -> "GroupName"
        if (!dn.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var commaIndex = dn.IndexOf(',');
        return commaIndex > 3 ? dn[3..commaIndex] : dn[3..];
    }

    private static string FormatException(Exception ex)
    {
        if (ex is LdapException ldapEx)
        {
            var parts = new List<string> { ldapEx.Message };

            if (ldapEx.ResultCode != 0)
            {
                parts.Add($"Result code: {ldapEx.ResultCode}");
            }

            if (!string.IsNullOrWhiteSpace(ldapEx.LdapErrorMessage))
            {
                parts.Add($"Server message: {ldapEx.LdapErrorMessage.Trim()}");
            }

            if (ldapEx.InnerException != null && ldapEx.InnerException.Message != ldapEx.Message)
            {
                parts.Add($"Detail: {ldapEx.InnerException.Message}");
            }

            return string.Join(" | ", parts);
        }

        if (ex.InnerException != null)
        {
            return $"{ex.Message} ({ex.InnerException.Message})";
        }

        return ex.Message;
    }

    internal static string ResolveBindDn(string username, string bindTemplate)
    {
        // If the input looks like an email, use it directly for binding
        // (AD accepts UPN as a bind identity); otherwise apply the template
        return username.Contains('@')
            ? username
            : string.Format(bindTemplate, EscapeLdapFilter(username));
    }

    private static string EscapeLdapFilter(string input)
    {
        return input
            .Replace("\\", "\\5c")
            .Replace("*", "\\2a")
            .Replace("(", "\\28")
            .Replace(")", "\\29")
            .Replace("\0", "\\00");
    }

    private async Task ApplyGroupMappingsAsync(
        SamaDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        List<string> ldapGroups)
    {
        var mappings = await dbContext.WorkspaceGroupMappings
            .AsNoTracking()
            .Where(m => m.IdentityProvider == AuthConstants.LdapSource)
            .ToListAsync();

        // Expand raw DNs to include extracted CNs for flexible matching
        var expandedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in ldapGroups)
        {
            expandedGroups.Add(group);
            var cn = ExtractCnFromDn(group);
            if (cn != null)
            {
                expandedGroups.Add(cn);
            }
        }

        var matchedMappings = mappings
            .Where(m => expandedGroups.Contains(m.ExternalGroupId))
            .ToList();

        // Sync admin role: grant or revoke based on current LDAP groups
        var shouldBeAdmin = matchedMappings.Any(m => m.WorkspaceId == null && m.Role == AuthConstants.AdminRole);
        var isAdmin = await userManager.IsInRoleAsync(user, AuthConstants.AdminRole);

        if (shouldBeAdmin && !isAdmin)
        {
            await userManager.AddToRoleAsync(user, AuthConstants.AdminRole);
            _logger.LogInformation("Granted Admin role to user {Email} via LDAP group mapping", user.Email);
        }
        else if (!shouldBeAdmin && isAdmin)
        {
            await userManager.RemoveFromRoleAsync(user, AuthConstants.AdminRole);
            _logger.LogInformation("Revoked Admin role from user {Email} — no longer in an LDAP admin group", user.Email);
        }

        // Diff-based workspace assignment sync
        var existingAssignments = await dbContext.UserWorkspaces
            .Where(uw => uw.UserId == user.Id && uw.Source == AuthConstants.LdapSource)
            .ToListAsync();

        var desiredAssignments = matchedMappings
            .Where(m => m.WorkspaceId != null && m.Role != AuthConstants.AdminRole)
            .Select(m => (WorkspaceId: m.WorkspaceId!.Value, m.Role))
            .ToHashSet();

        var existingSet = existingAssignments
            .Select(a => (a.WorkspaceId, a.Role))
            .ToHashSet();

        // Remove assignments that are no longer matched
        var toRemove = existingAssignments
            .Where(a => !desiredAssignments.Contains((a.WorkspaceId, a.Role)))
            .ToList();
        dbContext.UserWorkspaces.RemoveRange(toRemove);

        // Add new assignments that don't exist yet
        var now = DateTimeOffset.UtcNow;
        foreach (var (workspaceId, role) in desiredAssignments.Where(d => !existingSet.Contains(d)))
        {
            dbContext.UserWorkspaces.Add(new UserWorkspace
            {
                UserId = user.Id,
                WorkspaceId = workspaceId,
                Role = role,
                Source = AuthConstants.LdapSource,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        if (toRemove.Count > 0 || desiredAssignments.Except(existingSet).Any())
        {
            await dbContext.SaveChangesAsync();
        }
    }

    private async Task<List<string>> GetGroupMembershipsAsync(
        string host,
        int port,
        bool useSsl,
        bool useStartTls,
        string customRootCa,
        string bindDn,
        string bindPassword,
        string userDn)
    {
        var groups = new List<string>();
        var groupSearchBase = _globalSettings.LdapGroupSearchBase;
        var groupSearchFilter = _globalSettings.LdapGroupSearchFilter;

        if (string.IsNullOrWhiteSpace(groupSearchBase))
        {
            return groups;
        }

        using var connection = CreateConnection(useSsl, customRootCa);

        await connection.ConnectAsync(host, port);

        if (useStartTls)
        {
            await connection.StartTlsAsync();
        }

        if (!string.IsNullOrWhiteSpace(bindDn))
        {
            await connection.BindAsync(bindDn, bindPassword);
        }

        var filter = string.Format(groupSearchFilter, EscapeLdapFilter(userDn));
        var searchResults = await connection.SearchAsync(
            groupSearchBase,
            LdapConnection.ScopeSub,
            filter,
            ["dn", "cn"],
            false);

        while (await searchResults.HasMoreAsync())
        {
            try
            {
                groups.Add((await searchResults.NextAsync()).Dn);
            }
            catch (LdapReferralException)
            {
                // Ignore referrals
            }
        }

        _logger.LogDebug("Found {GroupCount} LDAP groups for user {UserDn}", groups.Count, userDn);

        return groups;
    }
}
