namespace SAMA.Data.Entities;

public class Workspace
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public string? DashboardMessage { get; set; }

    public bool IsPublic { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }


    // Navigation properties
    public ICollection<UserWorkspace> UserWorkspaces { get; set; } = [];

    public ICollection<WorkspaceGroupMapping> WorkspaceGroupMappings { get; set; } = [];

    public ICollection<Check> Checks { get; set; } = [];

    public ICollection<NotificationChannel> NotificationChannels { get; set; } = [];
}
