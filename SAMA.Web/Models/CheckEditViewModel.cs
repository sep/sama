namespace SAMA.Web.Models;

public class CheckEditViewModel
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

    public Dictionary<string, System.Text.Json.JsonElement> ConfigurationJson { get; set; } = [];
}
