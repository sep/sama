using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Web.Constants;

namespace SAMA.Web.Services;

public class GroupMappingSyncService(ILogger<GroupMappingSyncService> _logger)
{
    public async Task ApplyGroupMappingsAsync(
        SamaDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        List<string> groups,
        string source)
    {
        var mappings = await dbContext.WorkspaceGroupMappings
            .AsNoTracking()
            .Where(m => m.IdentityProvider == source)
            .ToListAsync();

        var normalizedGroups = NormalizeGroups(groups, source);

        var matchedMappings = mappings
            .Where(m => normalizedGroups.Contains(m.ExternalGroupId))
            .ToList();

        await SyncAdminRoleAsync(userManager, user, matchedMappings);
        await SyncWorkspaceAssignmentsAsync(dbContext, user, matchedMappings, source);
    }

    private static HashSet<string> NormalizeGroups(List<string> groups, string source)
    {
        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            normalized.Add(group);

            // For LDAP, also extract CN from full DN for flexible matching
            if (source == AuthConstants.LdapSource)
            {
                var cn = ExtractCnFromDn(group);
                if (cn != null)
                {
                    normalized.Add(cn);
                }
            }
        }

        return normalized;
    }

    internal static string? ExtractCnFromDn(string dn)
    {
        if (string.IsNullOrWhiteSpace(dn))
        {
            return null;
        }

        if (!dn.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var commaIndex = dn.IndexOf(',');
        return commaIndex > 3 ? dn[3..commaIndex] : dn[3..];
    }

    private async Task SyncAdminRoleAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        List<WorkspaceGroupMapping> matchedMappings)
    {
        var shouldBeAdmin = matchedMappings.Any(m => m.WorkspaceId == null && m.Role == AuthConstants.AdminRole);
        var isAdmin = await userManager.IsInRoleAsync(user, AuthConstants.AdminRole);

        if (shouldBeAdmin && !isAdmin)
        {
            await userManager.AddToRoleAsync(user, AuthConstants.AdminRole);
            _logger.LogInformation("Granted Admin role to user {Email} via group mapping", user.Email);
        }
        else if (!shouldBeAdmin && isAdmin)
        {
            await userManager.RemoveFromRoleAsync(user, AuthConstants.AdminRole);
            _logger.LogInformation("Revoked Admin role from user {Email} — no longer in an admin group", user.Email);
        }
    }

    private async Task SyncWorkspaceAssignmentsAsync(
        SamaDbContext dbContext,
        ApplicationUser user,
        List<WorkspaceGroupMapping> matchedMappings,
        string source)
    {
        var existingAssignments = await dbContext.UserWorkspaces
            .Where(uw => uw.UserId == user.Id && uw.Source == source)
            .ToListAsync();

        var desiredAssignments = matchedMappings
            .Where(m => m.WorkspaceId != null && m.Role != AuthConstants.AdminRole)
            .Select(m => (WorkspaceId: m.WorkspaceId!.Value, m.Role))
            .ToHashSet();

        var existingSet = existingAssignments
            .Select(a => (a.WorkspaceId, a.Role))
            .ToHashSet();

        var toRemove = existingAssignments
            .Where(a => !desiredAssignments.Contains((a.WorkspaceId, a.Role)))
            .ToList();
        dbContext.UserWorkspaces.RemoveRange(toRemove);

        var now = DateTimeOffset.UtcNow;
        foreach (var (workspaceId, role) in desiredAssignments.Where(d => !existingSet.Contains(d)))
        {
            dbContext.UserWorkspaces.Add(new UserWorkspace
            {
                UserId = user.Id,
                WorkspaceId = workspaceId,
                Role = role,
                Source = source,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        if (toRemove.Count > 0 || desiredAssignments.Except(existingSet).Any())
        {
            await dbContext.SaveChangesAsync();
        }
    }
}
