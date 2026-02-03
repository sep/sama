namespace SAMA.Web.Models;

public record ScriptOutputEntry(
    Guid CheckResultId,
    string? StandardOutput,
    string? StandardError,
    DateTimeOffset Timestamp);
