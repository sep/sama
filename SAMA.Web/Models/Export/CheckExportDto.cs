using System.Text.Json;

namespace SAMA.Web.Models.Export;

/// <summary>
/// Export DTO for a monitoring check.
/// </summary>
public class CheckExportDto
{
    public required string Name { get; set; }

    public string? Description { get; set; }

    public required string CheckType { get; set; }

    /// <summary>
    /// Gets or sets check configuration as a dictionary. Contains plaintext values (decrypted on export).
    /// </summary>
    public Dictionary<string, JsonElement> Configuration { get; set; } = [];

    public string Schedule { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; }

    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets alerts associated with this check.
    /// </summary>
    public List<AlertExportDto> Alerts { get; set; } = [];
}
