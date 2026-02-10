namespace SAMA.Web.Models;

public class CheckDetailsViewModel
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    public string WorkspaceName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string CheckType { get; set; } = string.Empty;

    public string Schedule { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; }

    public bool Enabled { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int ResultCount { get; set; }

    public int AlertCount { get; set; }

    public string? LastStatus { get; set; }

    public DateTimeOffset? LastCheckedAt { get; set; }

    public string? LastErrorMessage { get; set; }

    public Dictionary<string, object> MaskedConfiguration { get; set; } = [];

    public List<AlertInfo> Alerts { get; set; } = [];

    public class AlertInfo
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool TriggerOnWarn { get; set; }

        public bool TriggerOnDown { get; set; }

        public int FailureThreshold { get; set; }

        public bool SendRecoveryNotification { get; set; }

        public bool Enabled { get; set; }

        public int ChannelCount { get; set; }
    }
}
