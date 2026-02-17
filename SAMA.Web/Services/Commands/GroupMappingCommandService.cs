using Microsoft.EntityFrameworkCore;
using SAMA.Data;
using SAMA.Data.Entities;

namespace SAMA.Web.Services.Commands;

public class GroupMappingCommandService(SamaDbContext _dbContext, ILogger<GroupMappingCommandService> _logger)
{
    public async Task<Guid> CreateMappingAsync(
        Guid? workspaceId,
        string identityProvider,
        string externalGroupId,
        string role,
        string performedBy,
        CancellationToken cancellationToken = default)
    {
        var mapping = new WorkspaceGroupMapping
        {
            WorkspaceId = workspaceId,
            IdentityProvider = identityProvider,
            ExternalGroupId = externalGroupId.Trim(),
            Role = role,
        };

        _dbContext.WorkspaceGroupMappings.Add(mapping);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {User} created group mapping: {GroupId} -> {Role} for workspace {WorkspaceId}",
            performedBy,
            externalGroupId,
            role,
            workspaceId?.ToString() ?? "Global Admin");

        return mapping.Id;
    }

    public async Task<bool> DeleteMappingAsync(
        Guid mappingId,
        string performedBy,
        CancellationToken cancellationToken = default)
    {
        var mapping = await _dbContext.WorkspaceGroupMappings
            .FirstOrDefaultAsync(m => m.Id == mappingId, cancellationToken);

        if (mapping == null)
        {
            return false;
        }

        _dbContext.WorkspaceGroupMappings.Remove(mapping);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {User} deleted group mapping {MappingId}: {GroupId} -> {Role}",
            performedBy,
            mappingId,
            mapping.ExternalGroupId,
            mapping.Role);

        return true;
    }
}
