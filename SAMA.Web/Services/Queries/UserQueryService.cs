using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Web.Constants;
using SAMA.Web.Models;

namespace SAMA.Web.Services.Queries;

public class UserQueryService(
    UserManager<ApplicationUser> _userManager,
    SamaDbContext _context)
{
    public virtual async Task<UserViewModel?> GetUserByIdAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return null;
        }

        var isAdmin = await _userManager.IsInRoleAsync(user, AuthConstants.AdminRole);
        var workspaceCount = await _context.UserWorkspaces
            .Where(uw => uw.UserId == userId)
            .CountAsync();
        var isExternalUser = await _context.UserLogins
            .AnyAsync(ul => ul.UserId == userId && ul.LoginProvider == AuthConstants.LdapSource);

        return new UserViewModel
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            CreatedAt = user.CreatedAt,
            IsAdmin = isAdmin,
            IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow,
            IsExternalUser = isExternalUser,
            WorkspaceCount = workspaceCount
        };
    }

    public virtual async Task<List<UserViewModel>> GetAllUsersAsync()
    {
        var users = await _userManager.Users
            .OrderBy(u => u.Email)
            .ToListAsync();

        var adminUserIds = (await _userManager.GetUsersInRoleAsync(AuthConstants.AdminRole))
            .Select(u => u.Id)
            .ToHashSet();

        var externalUserIds = await _context.UserLogins
            .Where(ul => ul.LoginProvider == AuthConstants.LdapSource)
            .Select(ul => ul.UserId)
            .Distinct()
            .ToListAsync();
        var externalUserIdSet = externalUserIds.ToHashSet();

        return users.Select(u => new UserViewModel
        {
            Id = u.Id,
            Email = u.Email ?? string.Empty,
            CreatedAt = u.CreatedAt,
            IsAdmin = adminUserIds.Contains(u.Id),
            IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow,
            IsExternalUser = externalUserIdSet.Contains(u.Id),
            WorkspaceCount = 0
        }).ToList();
    }

    public virtual async Task<List<WorkspaceAssignmentViewModel>> GetWorkspacesWithManualAssignmentStatusAsync(Guid? userId = null)
    {
        var allWorkspaces = await _context.Workspaces
            .OrderBy(w => w.Name)
            .Select(w => new { w.Id, w.Name })
            .ToListAsync();

        if (!userId.HasValue)
        {
            return allWorkspaces.Select(w => new WorkspaceAssignmentViewModel
            {
                WorkspaceId = w.Id,
                WorkspaceName = w.Name,
                Role = AuthConstants.ViewerRole,
                IsAssigned = false
            }).ToList();
        }

        var userWorkspaces = await _context.UserWorkspaces
            .Where(uw => uw.UserId == userId.Value && uw.Source == AuthConstants.ManualSource)
            .ToListAsync();

        return allWorkspaces.Select(w =>
        {
            var assignment = userWorkspaces.FirstOrDefault(uw => uw.WorkspaceId == w.Id);
            return new WorkspaceAssignmentViewModel
            {
                WorkspaceId = w.Id,
                WorkspaceName = w.Name,
                Role = assignment?.Role ?? AuthConstants.ViewerRole,
                IsAssigned = assignment != null
            };
        }).ToList();
    }
}
