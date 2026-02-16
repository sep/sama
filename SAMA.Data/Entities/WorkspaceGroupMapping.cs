namespace SAMA.Data.Entities;

public class WorkspaceGroupMapping
{
    public Guid Id { get; set; }

    public Guid? WorkspaceId { get; set; } // NULL for global Admin mappings

    public required string IdentityProvider { get; set; } // LDAP, Azure, Okta, Auth0, Generic-OIDC

    public required string ExternalGroupId { get; set; } // Group name/DN/ID from IdP

    public required string Role { get; set; } // Admin, Editor, Viewer

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }


    // Navigation properties
    public Workspace? Workspace { get; set; }
}
