namespace SAMA.Web.Constants;

/// <summary>
/// Default values for notification channel configuration fields.
/// </summary>
public static class ChannelDefaults
{
    /// <summary>
    /// Placeholder in script arguments that gets replaced with the temp file path
    /// when using inline script content. PowerShell, bash, python, etc. can all
    /// use this to receive the script file path.
    /// </summary>
    public const string ScriptFilePlaceholder = "{SCRIPT_FILE}";
}
