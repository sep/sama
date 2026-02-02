using Microsoft.EntityFrameworkCore;
using SAMA.Data;
using SAMA.Data.Entities;

namespace SAMA.Web.Services.Commands;

public class WorkspaceCommandService(SamaDbContext _dbContext, ILogger<WorkspaceCommandService> _logger)
{
    public virtual async Task<Guid> CreateWorkspaceAsync(
        string name,
        string? description,
        string? dashboardMessage,
        bool isPublic,
        string performedBy,
        CancellationToken cancellationToken = default)
    {
        var workspace = new Workspace
        {
            Name = name,
            Description = description,
            DashboardMessage = dashboardMessage,
            IsPublic = isPublic
        };

        _dbContext.Workspaces.Add(workspace);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {User} created workspace {WorkspaceName} (Id: {WorkspaceId})",
            performedBy,
            workspace.Name,
            workspace.Id);

        return workspace.Id;
    }

    public virtual async Task<bool> UpdateWorkspaceAsync(
        Guid workspaceId,
        string name,
        string? description,
        string? dashboardMessage,
        bool isPublic,
        string performedBy,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _dbContext.Workspaces
            .FirstOrDefaultAsync(w => w.Id == workspaceId, cancellationToken);

        if (workspace == null)
        {
            return false;
        }

        workspace.Name = name;
        workspace.Description = description;
        workspace.DashboardMessage = dashboardMessage;
        workspace.IsPublic = isPublic;
        workspace.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {User} updated workspace {WorkspaceName} (Id: {WorkspaceId})",
            performedBy,
            workspace.Name,
            workspace.Id);

        return true;
    }

    public virtual async Task<bool> DeleteWorkspaceAsync(
        Guid workspaceId,
        string performedBy,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _dbContext.Workspaces
            .FirstOrDefaultAsync(w => w.Id == workspaceId, cancellationToken);

        if (workspace == null)
        {
            return false;
        }

        var workspaceName = workspace.Name;

        _dbContext.Workspaces.Remove(workspace);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {User} deleted workspace {WorkspaceName} (Id: {WorkspaceId})",
            performedBy,
            workspaceName,
            workspaceId);

        return true;
    }
}
