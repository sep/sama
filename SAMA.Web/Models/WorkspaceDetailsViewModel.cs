namespace SAMA.Web.Models;

public class WorkspaceDetailsViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? DashboardMessage { get; set; }

    public bool IsPublic { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int CheckCount { get; set; }

    public int NotificationChannelCount { get; set; }

    public int UserCount { get; set; }
}
