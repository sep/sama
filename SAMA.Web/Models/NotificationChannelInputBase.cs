namespace SAMA.Web.Models;

/// <summary>
/// Base class for notification channel input models.
/// Contains configuration fields for all channel types.
/// Specific properties are used based on ChannelType.
/// </summary>
public class NotificationChannelInputBase
{
    public virtual string ChannelType { get; set; } = string.Empty;

    // Email configuration
    public string? EmailSmtpHost { get; set; }

    public int? EmailSmtpPort { get; set; }

    public bool EmailUseSsl { get; set; }

    public string? EmailSmtpUsername { get; set; }

    public string? EmailSmtpPassword { get; set; }

    public string? EmailFromAddress { get; set; }

    public string? EmailRecipients { get; set; }

    // Slack configuration
    public string? SlackWebhookUrl { get; set; }

    // Teams configuration
    public string? TeamsWebhookUrl { get; set; }

    // Discord configuration
    public string? DiscordWebhookUrl { get; set; }

    // Script configuration
    public string? ScriptPath { get; set; }

    public string? ScriptArguments { get; set; }

    public string? ScriptContent { get; set; }

    // EventGrid configuration
    public string? EventGridTopicEndpoint { get; set; }

    public string? EventGridAccessKey { get; set; }
}
