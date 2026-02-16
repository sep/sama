namespace SAMA.Web.Models;

public class UserViewModel
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsAdmin { get; set; }

    public bool IsLockedOut { get; set; }

    public bool IsExternalUser { get; set; }

    public int WorkspaceCount { get; set; }
}
