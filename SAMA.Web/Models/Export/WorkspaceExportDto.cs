namespace SAMA.Web.Models.Export;

/// <summary>
/// Export DTO for a workspace and its contained entities.
/// </summary>
public class WorkspaceExportDto
{
    public required string Name { get; set; }

    public string? Description { get; set; }

    public bool IsPublic { get; set; }

    public List<CheckExportDto> Checks { get; set; } = [];

    public List<NotificationChannelExportDto> NotificationChannels { get; set; } = [];
}
