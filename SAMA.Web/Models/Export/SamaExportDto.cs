namespace SAMA.Web.Models.Export;

/// <summary>
/// Root export DTO containing all exportable configuration.
/// Schema version allows backward compatibility via migration logic.
/// </summary>
public class SamaExportDto
{
    /// <summary>
    /// Gets or sets the schema version for migration support. Increment when making breaking changes.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Gets or sets the SAMA version that generated this export.
    /// </summary>
    public string? ExportedFromVersion { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the export was created.
    /// </summary>
    public DateTimeOffset ExportedAt { get; set; }

    /// <summary>
    /// Gets or sets the exported workspaces with their checks, channels, alerts, and subscriptions.
    /// </summary>
    public List<WorkspaceExportDto> Workspaces { get; set; } = [];
}
