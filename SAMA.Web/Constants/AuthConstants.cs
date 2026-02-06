namespace SAMA.Web.Constants;

/// <summary>
/// Authorization constants for roles and policies.
/// </summary>
public static class AuthConstants
{
    /// <summary>
    /// Global administrator role with full system access.
    /// </summary>
    public const string AdminRole = "Admin";

    /// <summary>
    /// Workspace editor role (workspace-scoped, assigned via UserWorkspaces table).
    /// Can create/edit checks and alerts within assigned workspaces.
    /// </summary>
    public const string EditorRole = "Editor";

    /// <summary>
    /// Workspace viewer role (workspace-scoped, assigned via UserWorkspaces table).
    /// Read-only access to assigned workspaces.
    /// </summary>
    public const string ViewerRole = "Viewer";

    /// <summary>
    /// Source for manually assigned workspace access (persistent).
    /// </summary>
    public const string ManualSource = "Manual";

    /// <summary>
    /// Source for OIDC-provisioned workspace access (recalculated on login).
    /// </summary>
    public const string OidcSource = "OIDC";

    /// <summary>
    /// Source for LDAP/Active Directory-provisioned workspace access (recalculated on login).
    /// </summary>
    public const string LdapSource = "LDAP";
}
