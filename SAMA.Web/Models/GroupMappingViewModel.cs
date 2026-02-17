namespace SAMA.Web.Models;

public class GroupMappingViewModel
{
    public Guid Id { get; set; }

    public Guid? WorkspaceId { get; set; }

    public string? WorkspaceName { get; set; }

    public required string IdentityProvider { get; set; }

    public required string ExternalGroupId { get; set; }

    public required string Role { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
