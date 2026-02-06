namespace SAMA.Data.Entities;

public class UserWorkspace
{
    public Guid UserId { get; set; }

    public Guid WorkspaceId { get; set; }

    public required string Role { get; set; } // Editor, Viewer

    public required string Source { get; set; } = "Manual"; // Manual, LDAP, OIDC

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }


    // Navigation properties
    public ApplicationUser User { get; set; } = null!;

    public Workspace Workspace { get; set; } = null!;
}
