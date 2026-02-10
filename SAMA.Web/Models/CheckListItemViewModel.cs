namespace SAMA.Web.Models;

public class CheckListItemViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string CheckType { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public string Schedule { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string? LastStatus { get; set; }

    public DateTimeOffset? LastCheckedAt { get; set; }

    public int? LastResponseTimeMs { get; set; }

    public string? LastErrorMessage { get; set; }

    public int AlertCount { get; set; }
}
