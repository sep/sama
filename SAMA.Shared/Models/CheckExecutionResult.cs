namespace SAMA.Shared.Models;

public class CheckExecutionResult
{
    public required string Status { get; set; }

    public int? ResponseTimeMs { get; set; }

    public int? StatusCode { get; set; }

    public string? ErrorMessage { get; set; }

    public string? StandardOutput { get; set; }

    public string? StandardError { get; set; }

    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
}
