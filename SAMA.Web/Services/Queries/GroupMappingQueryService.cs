using Microsoft.EntityFrameworkCore;
using SAMA.Data;
using SAMA.Web.Models;

namespace SAMA.Web.Services.Queries;

public class GroupMappingQueryService(SamaDbContext _dbContext)
{
    public async Task<List<GroupMappingViewModel>> GetAllMappingsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.WorkspaceGroupMappings
            .AsNoTracking()
            .Include(m => m.Workspace)
            .OrderBy(m => m.Workspace!.Name)
            .ThenBy(m => m.ExternalGroupId)
            .Select(m => new GroupMappingViewModel
            {
                Id = m.Id,
                WorkspaceId = m.WorkspaceId,
                WorkspaceName = m.Workspace != null ? m.Workspace.Name : null,
                IdentityProvider = m.IdentityProvider,
                ExternalGroupId = m.ExternalGroupId,
                Role = m.Role,
                CreatedAt = m.CreatedAt,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> MappingExistsAsync(
        Guid? workspaceId,
        string identityProvider,
        string externalGroupId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.WorkspaceGroupMappings
            .AnyAsync(
                m => m.WorkspaceId == workspaceId
                    && m.IdentityProvider == identityProvider
                    && m.ExternalGroupId == externalGroupId.Trim(),
                cancellationToken);
    }
}
