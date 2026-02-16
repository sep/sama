namespace SAMA.Web.Services;

public class LdapLoginResult
{
    public bool Succeeded { get; private init; }

    public string? ErrorMessage { get; private init; }

    public string? UserDn { get; private init; }

    public string? Email { get; private init; }

    public string? DisplayName { get; private init; }

    public List<string> Groups { get; private init; } = [];

    public List<string> Warnings { get; private init; } = [];

    public static LdapLoginResult Success(string userDn, string email, string displayName, List<string> groups, List<string>? warnings = null)
    {
        return new LdapLoginResult
        {
            Succeeded = true,
            UserDn = userDn,
            Email = email,
            DisplayName = displayName,
            Groups = groups,
            Warnings = warnings ?? [],
        };
    }

    public static LdapLoginResult Fail(string error)
    {
        return new LdapLoginResult
        {
            Succeeded = false,
            ErrorMessage = error,
        };
    }
}
