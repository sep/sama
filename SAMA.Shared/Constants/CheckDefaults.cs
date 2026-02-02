namespace SAMA.Shared.Constants;

/// <summary>
/// Default values for check configuration fields.
/// Provides a single source of truth for all check defaults.
/// </summary>
public static class CheckDefaults
{
    /// <summary>
    /// Default HTTP method for HTTP checks.
    /// </summary>
    public const string HttpMethod = "GET";

    /// <summary>
    /// Default expected HTTP status codes.
    /// </summary>
    public const string HttpExpectedStatusCodes = "200";

    /// <summary>
    /// Default setting for following HTTP redirects.
    /// </summary>
    public const bool HttpFollowRedirects = true;

    /// <summary>
    /// Default setting for allowing invalid SSL certificates in HTTP checks.
    /// </summary>
    public const bool HttpAllowInvalidSsl = false;

    /// <summary>
    /// Default number of ping packets to send.
    /// </summary>
    public const int PingPacketCount = 4;

    /// <summary>
    /// Default packet loss threshold percentage for Warn status.
    /// </summary>
    public const int PingPacketLossThresholdPercent = 50;

    /// <summary>
    /// Default DNS record type to query.
    /// </summary>
    public const string DnsRecordType = "A";

    /// <summary>
    /// Default days before TLS certificate expiry to trigger Warn status.
    /// </summary>
    public const int TlsDaysBeforeExpiryWarning = 30;

    /// <summary>
    /// Default expected exit code for script checks.
    /// </summary>
    public const int ScriptExpectedExitCode = 0;

    /// <summary>
    /// Placeholder in script arguments that gets replaced with the temp file path
    /// when using inline script content.
    /// </summary>
    public const string ScriptFilePlaceholder = "{SCRIPT_FILE}";

    /// <summary>
    /// Default timeout for all checks (in seconds).
    /// </summary>
    public const int CheckTimeoutSeconds = 30;
}
