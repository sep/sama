namespace SAMA.Web.Models.Export;

/// <summary>
/// Root export DTO containing encrypted configuration.
/// Schema version allows backward compatibility via migration logic.
/// </summary>
public class SamaExportDto
{
    /// <summary>
    /// Gets or sets the schema version for migration support. Increment when making breaking changes.
    /// </summary>
    public int SchemaVersion { get; set; } = 2;

    /// <summary>
    /// Gets or sets the SAMA version that generated this export.
    /// </summary>
    public string? ExportedFromVersion { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the export was created.
    /// </summary>
    public DateTimeOffset ExportedAt { get; set; }

    /// <summary>
    /// Gets or sets the encrypted workspaces data. Contains AES-GCM encrypted JSON of a list of <see cref="WorkspaceExportDto"/>.
    /// </summary>
    public string EncryptedData { get; set; } = string.Empty;
}
