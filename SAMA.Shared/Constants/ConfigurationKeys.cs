namespace SAMA.Shared.Constants;

/// <summary>
/// Configuration key constants for notification channels and checks.
/// Provides consistent keys for configuration JSON dictionaries.
/// </summary>
public static class ConfigurationKeys
{
    /// <summary>
    /// Common configuration keys used across multiple check types.
    /// </summary>
    public static class Common
    {
        public const string TimeoutSeconds = "TimeoutSeconds";
    }

    /// <summary>
    /// Configuration keys for Email (SMTP) channels.
    /// </summary>
    public static class Email
    {
        public const string SmtpHost = "SmtpHost";
        public const string SmtpPort = "SmtpPort";
        public const string UseSsl = "UseSsl";
        public const string Username = "Username";
        public const string Password = "Password";
        public const string FromAddress = "FromAddress";
        public const string Recipients = "Recipients";
    }

    /// <summary>
    /// Configuration keys for webhook-based channels (Slack, Teams, Discord).
    /// </summary>
    public static class Webhook
    {
        public const string WebhookUrl = "WebhookUrl";
    }

    /// <summary>
    /// Configuration keys for Script channels.
    /// </summary>
    public static class Script
    {
        public const string Path = "Path";
        public const string Arguments = "Arguments";
        public const string Content = "Content";
    }

    /// <summary>
    /// Configuration keys for Azure Event Grid channels.
    /// </summary>
    public static class EventGrid
    {
        public const string TopicEndpoint = "TopicEndpoint";
        public const string AccessKey = "AccessKey";
    }

    /// <summary>
    /// Configuration keys for HTTP/HTTPS checks.
    /// </summary>
    public static class HttpCheck
    {
        public const string Url = "Url";
        public const string Method = "Method";
        public const string Headers = "Headers";
        public const string Body = "Body";
        public const string ExpectedStatusCodes = "ExpectedStatusCodes";
        public const string ContentValidation = "ContentValidation";
        public const string FollowRedirects = "FollowRedirects";
        public const string AllowInvalidSsl = "AllowInvalidSsl";
        public const string ResponseTimeWarnThresholdMs = "ResponseTimeWarnThresholdMs";
    }

    /// <summary>
    /// Configuration keys for TCP port checks.
    /// </summary>
    public static class TcpCheck
    {
        public const string Host = "Host";
        public const string Port = "Port";
        public const string ConnectionTimeWarnThresholdMs = "ConnectionTimeWarnThresholdMs";
    }

    /// <summary>
    /// Configuration keys for ICMP ping checks.
    /// </summary>
    public static class PingCheck
    {
        public const string Host = "Host";
        public const string PacketCount = "PacketCount";
        public const string PacketLossThresholdPercent = "PacketLossThresholdPercent";
    }

    /// <summary>
    /// Configuration keys for DNS resolution checks.
    /// </summary>
    public static class DnsCheck
    {
        public const string Hostname = "Hostname";
        public const string RecordType = "RecordType";
        public const string ExpectedValues = "ExpectedValues";
    }

    /// <summary>
    /// Configuration keys for TLS certificate checks.
    /// </summary>
    public static class TlsCheck
    {
        public const string Url = "Url";
        public const string DaysBeforeExpiryWarning = "DaysBeforeExpiryWarning";
        public const string CustomCaCertificate = "CustomCaCertificate";
    }

    /// <summary>
    /// Configuration keys for Script execution checks.
    /// </summary>
    public static class ScriptCheck
    {
        public const string Path = "Path";
        public const string Arguments = "Arguments";
        public const string ExpectedExitCode = "ExpectedExitCode";
        public const string Content = "Content";
    }
}
