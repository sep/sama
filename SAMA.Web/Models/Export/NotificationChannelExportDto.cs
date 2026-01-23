using System.Text.Json;

namespace SAMA.Web.Models.Export;

/// <summary>
/// Export DTO for a notification channel.
/// </summary>
public class NotificationChannelExportDto
{
    /// <summary>
    /// Gets or sets a unique identifier within the export file, used for alert-to-channel references.
    /// This is NOT the database ID; it's a synthetic identifier scoped to this export.
    /// </summary>
    public required string ExportId { get; set; }

    public required string Name { get; set; }

    public required string ChannelType { get; set; }

    /// <summary>
    /// Gets or sets channel configuration as a dictionary. Contains plaintext values (decrypted on export).
    /// </summary>
    public Dictionary<string, JsonElement> Configuration { get; set; } = [];

    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets event type subscriptions for this channel (e.g., "CheckCreated", "CheckStatusChanged").
    /// </summary>
    public List<string> EventSubscriptions { get; set; } = [];
}
