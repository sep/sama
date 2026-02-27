namespace SAMA.Web.Models;

public class UserWorkspaceAssignmentViewModel
{
    public Guid WorkspaceId { get; set; }

    public string WorkspaceName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;
}
