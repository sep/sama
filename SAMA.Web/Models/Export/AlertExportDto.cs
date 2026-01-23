namespace SAMA.Web.Models.Export;

/// <summary>
/// Export DTO for an alert configuration.
/// References notification channels by their export IDs.
/// </summary>
public class AlertExportDto
{
    public required string Name { get; set; }

    public bool TriggerOnWarn { get; set; }

    public bool TriggerOnDown { get; set; }

    public int FailureThreshold { get; set; }

    public bool SendRecoveryNotification { get; set; }

    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets export IDs of notification channels to notify when this alert triggers.
    /// These match the ExportId property of NotificationChannelExportDto within the same workspace.
    /// </summary>
    public List<string> NotificationChannelExportIds { get; set; } = [];
}
