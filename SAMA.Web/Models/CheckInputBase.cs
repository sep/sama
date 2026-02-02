using SAMA.Shared.Constants;

namespace SAMA.Web.Models;

/// <summary>
/// Base class for check input models.
/// Contains configuration fields for all check types.
/// Specific properties are used based on CheckType.
/// </summary>
public class CheckInputBase
{
    public virtual string CheckType { get; set; } = string.Empty;


    // HTTP/HTTPS check properties
    public string? HttpUrl { get; set; }

    public string? HttpMethod { get; set; } = CheckDefaults.HttpMethod;

    public string? HttpHeaders { get; set; }

    public string? HttpBody { get; set; }

    public string? HttpExpectedStatusCodes { get; set; } = CheckDefaults.HttpExpectedStatusCodes;

    public string? HttpContentValidation { get; set; }

    public bool HttpFollowRedirects { get; set; } = CheckDefaults.HttpFollowRedirects;

    public bool HttpAllowInvalidSsl { get; set; } = CheckDefaults.HttpAllowInvalidSsl;

    public int? HttpResponseTimeWarnThresholdMs { get; set; }


    // TCP check properties
    public string? TcpHost { get; set; }

    public int? TcpPort { get; set; }

    public int? TcpConnectionTimeWarnThresholdMs { get; set; }


    // Ping check properties
    public string? PingHost { get; set; }

    public int? PingPacketCount { get; set; } = CheckDefaults.PingPacketCount;

    public int? PingPacketLossThresholdPercent { get; set; } = CheckDefaults.PingPacketLossThresholdPercent;


    // DNS check properties
    public string? DnsHostname { get; set; }

    public string? DnsRecordType { get; set; } = CheckDefaults.DnsRecordType;

    public string? DnsExpectedValues { get; set; }


    // TLS certificate check properties
    public string? TlsUrl { get; set; }

    public int? TlsDaysBeforeExpiryWarning { get; set; } = CheckDefaults.TlsDaysBeforeExpiryWarning;

    public string? TlsCustomCaCertificate { get; set; }


    // Script check properties
    public string? ScriptPath { get; set; }

    public string? ScriptArguments { get; set; }

    public int? ScriptExpectedExitCode { get; set; } = CheckDefaults.ScriptExpectedExitCode;

    public string? ScriptContent { get; set; }
}
