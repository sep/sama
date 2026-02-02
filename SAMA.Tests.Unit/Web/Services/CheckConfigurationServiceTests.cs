using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SAMA.Shared.Constants;
using SAMA.Web.Models;
using SAMA.Web.Services;

namespace SAMA.Tests.Unit.Web.Services;

[TestClass]
public class CheckConfigurationServiceTests
{
    private CheckConfigurationService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new CheckConfigurationService();
    }

    #region HTTP Check Tests

    [TestMethod]
    public void BuildConfigurationShouldCreateHttpConfigurationWithAllRequiredFields()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Http,
            HttpUrl = "https://example.com",
            HttpMethod = "GET",
            HttpExpectedStatusCodes = "200, 201",
            HttpFollowRedirects = true,
            HttpAllowInvalidSsl = false
        };

        var config = _service.BuildConfiguration(input);

        Assert.AreEqual("https://example.com", config[ConfigurationKeys.HttpCheck.Url].GetString());
        Assert.AreEqual("GET", config[ConfigurationKeys.HttpCheck.Method].GetString());
        CollectionAssert.AreEqual(new[] { 200, 201 }, config[ConfigurationKeys.HttpCheck.ExpectedStatusCodes].EnumerateArray().Select(e => e.GetInt32()).ToArray());
        Assert.IsTrue(config[ConfigurationKeys.HttpCheck.FollowRedirects].GetBoolean());
        Assert.IsFalse(config[ConfigurationKeys.HttpCheck.AllowInvalidSsl].GetBoolean());
    }

    [TestMethod]
    public void BuildConfigurationShouldHandleHttpOptionalFields()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Http,
            HttpUrl = "https://example.com",
            HttpExpectedStatusCodes = "200",
            HttpHeaders = "Authorization: Bearer token\r\nContent-Type: application/json",
            HttpBody = "{\"test\":\"data\"}",
            HttpContentValidation = "success",
            HttpResponseTimeWarnThresholdMs = 2000
        };

        var config = _service.BuildConfiguration(input);

        Assert.AreEqual("Authorization: Bearer token\r\nContent-Type: application/json", config[ConfigurationKeys.HttpCheck.Headers].GetString());
        Assert.AreEqual("{\"test\":\"data\"}", config[ConfigurationKeys.HttpCheck.Body].GetString());
        Assert.AreEqual("success", config[ConfigurationKeys.HttpCheck.ContentValidation].GetString());
        Assert.AreEqual(2000, config[ConfigurationKeys.HttpCheck.ResponseTimeWarnThresholdMs].GetInt32());
    }

    [TestMethod]
    public void BuildConfigurationShouldExcludeHttpEmptyOptionalFields()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Http,
            HttpUrl = "https://example.com",
            HttpExpectedStatusCodes = "200",
            HttpHeaders = null,
            HttpBody = "",
            HttpContentValidation = "   ",
            HttpResponseTimeWarnThresholdMs = null
        };

        var config = _service.BuildConfiguration(input);

        Assert.IsFalse(config.ContainsKey(ConfigurationKeys.HttpCheck.Headers));
        Assert.IsFalse(config.ContainsKey(ConfigurationKeys.HttpCheck.Body));
        Assert.IsFalse(config.ContainsKey(ConfigurationKeys.HttpCheck.ContentValidation));
        Assert.IsFalse(config.ContainsKey(ConfigurationKeys.HttpCheck.ResponseTimeWarnThresholdMs));
    }

    [TestMethod]
    public void BuildConfigurationShouldParseHttpStatusCodesWithExtraSpaces()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Http,
            HttpUrl = "https://example.com",
            HttpExpectedStatusCodes = "  200  ,  201  ,  204  "
        };

        var config = _service.BuildConfiguration(input);

        CollectionAssert.AreEqual(new[] { 200, 201, 204 }, config[ConfigurationKeys.HttpCheck.ExpectedStatusCodes].EnumerateArray().Select(e => e.GetInt32()).ToArray());
    }

    [TestMethod]
    public void BuildConfigurationShouldUseHttpDefaultMethod()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Http,
            HttpUrl = "https://example.com",
            HttpExpectedStatusCodes = "200",
            HttpMethod = null
        };

        var config = _service.BuildConfiguration(input);

        Assert.AreEqual(CheckDefaults.HttpMethod, config[ConfigurationKeys.HttpCheck.Method].GetString());
    }

    [TestMethod]
    public void PopulateFromConfigurationShouldRestoreAllHttpFields()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Http,
            HttpUrl = "https://example.com",
            HttpMethod = "POST",
            HttpExpectedStatusCodes = "200, 201, 204",
            HttpHeaders = "Authorization: Bearer token",
            HttpBody = "{\"key\":\"value\"}",
            HttpContentValidation = "success",
            HttpFollowRedirects = false,
            HttpAllowInvalidSsl = true,
            HttpResponseTimeWarnThresholdMs = 1500
        };

        var config = _service.BuildConfiguration(input);
        var restored = new CheckInputBase { CheckType = CheckTypes.Http };
        _service.PopulateFromConfiguration(restored, config);

        Assert.AreEqual("https://example.com", restored.HttpUrl);
        Assert.AreEqual("POST", restored.HttpMethod);
        Assert.AreEqual("200, 201, 204", restored.HttpExpectedStatusCodes);
        Assert.AreEqual("Authorization: Bearer token", restored.HttpHeaders);
        Assert.AreEqual("{\"key\":\"value\"}", restored.HttpBody);
        Assert.AreEqual("success", restored.HttpContentValidation);
        Assert.IsFalse(restored.HttpFollowRedirects);
        Assert.IsTrue(restored.HttpAllowInvalidSsl);
        Assert.AreEqual(1500, restored.HttpResponseTimeWarnThresholdMs);
    }

    [TestMethod]
    public void PopulateFromConfigurationShouldHandleHttpMissingOptionalFields()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com"),
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement("GET"),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200 }),
            [ConfigurationKeys.HttpCheck.FollowRedirects] = JsonSerializer.SerializeToElement(true),
            [ConfigurationKeys.HttpCheck.AllowInvalidSsl] = JsonSerializer.SerializeToElement(false)
        };

        var input = new CheckInputBase { CheckType = CheckTypes.Http };
        _service.PopulateFromConfiguration(input, config);

        Assert.IsNull(input.HttpHeaders);
        Assert.IsNull(input.HttpBody);
        Assert.IsNull(input.HttpContentValidation);
        Assert.IsNull(input.HttpResponseTimeWarnThresholdMs);
    }

    [TestMethod]
    public void PopulateFromConfigurationShouldUseHttpDefaultsForMissingBooleans()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com"),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200 })
        };

        var input = new CheckInputBase { CheckType = CheckTypes.Http };
        _service.PopulateFromConfiguration(input, config);

        Assert.AreEqual(CheckDefaults.HttpFollowRedirects, input.HttpFollowRedirects);
        Assert.AreEqual(CheckDefaults.HttpAllowInvalidSsl, input.HttpAllowInvalidSsl);
    }

    [TestMethod]
    public void ValidateConfigurationShouldAcceptValidHttpConfiguration()
    {
        var modelState = new ModelStateDictionary();
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Http,
            HttpUrl = "https://example.com",
            HttpExpectedStatusCodes = "200, 201"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsTrue(modelState.IsValid);
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectHttpMissingUrl()
    {
        var modelState = new ModelStateDictionary();
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Http,
            HttpUrl = null,
            HttpExpectedStatusCodes = "200"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.HttpUrl)));
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectHttpInvalidUrl()
    {
        var modelState = new ModelStateDictionary();
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Http,
            HttpUrl = "not-a-valid-url",
            HttpExpectedStatusCodes = "200"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.HttpUrl)));
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectHttpMissingStatusCodes()
    {
        var modelState = new ModelStateDictionary();
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Http,
            HttpUrl = "https://example.com",
            HttpExpectedStatusCodes = null
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.HttpExpectedStatusCodes)));
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectHttpInvalidResponseTimeThreshold()
    {
        var modelState = new ModelStateDictionary();
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Http,
            HttpUrl = "https://example.com",
            HttpExpectedStatusCodes = "200",
            HttpResponseTimeWarnThresholdMs = 0
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.HttpResponseTimeWarnThresholdMs)));
    }

    #endregion

    #region TCP Check Tests

    [TestMethod]
    public void BuildConfigurationShouldCreateTcpConfiguration()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Tcp,
            TcpHost = "example.com",
            TcpPort = 443,
            TcpConnectionTimeWarnThresholdMs = 1000
        };

        var config = _service.BuildConfiguration(input);

        Assert.AreEqual("example.com", config[ConfigurationKeys.TcpCheck.Host].GetString());
        Assert.AreEqual(443, config[ConfigurationKeys.TcpCheck.Port].GetInt32());
        Assert.AreEqual(1000, config[ConfigurationKeys.TcpCheck.ConnectionTimeWarnThresholdMs].GetInt32());
    }

    [TestMethod]
    public void BuildConfigurationShouldExcludeTcpOptionalThreshold()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Tcp,
            TcpHost = "example.com",
            TcpPort = 443,
            TcpConnectionTimeWarnThresholdMs = null
        };

        var config = _service.BuildConfiguration(input);

        Assert.IsFalse(config.ContainsKey(ConfigurationKeys.TcpCheck.ConnectionTimeWarnThresholdMs));
    }

    [TestMethod]
    public void PopulateFromConfigurationShouldRestoreTcpFields()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Tcp,
            TcpHost = "example.com",
            TcpPort = 8080,
            TcpConnectionTimeWarnThresholdMs = 500
        };

        var config = _service.BuildConfiguration(input);
        var restored = new CheckInputBase { CheckType = CheckTypes.Tcp };
        _service.PopulateFromConfiguration(restored, config);

        Assert.AreEqual("example.com", restored.TcpHost);
        Assert.AreEqual(8080, restored.TcpPort);
        Assert.AreEqual(500, restored.TcpConnectionTimeWarnThresholdMs);
    }

    [TestMethod]
    public void ValidateConfigurationShouldAcceptValidTcpConfiguration()
    {
        var modelState = new ModelStateDictionary();
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Tcp,
            TcpHost = "example.com",
            TcpPort = 443
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsTrue(modelState.IsValid);
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectTcpInvalidPort()
    {
        var modelState = new ModelStateDictionary();
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Tcp,
            TcpHost = "example.com",
            TcpPort = 70000
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.TcpPort)));
    }

    #endregion

    #region Ping Check Tests

    [TestMethod]
    public void BuildConfigurationShouldCreatePingConfiguration()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Ping,
            PingHost = "example.com",
            PingPacketCount = 4,
            PingPacketLossThresholdPercent = 50
        };

        var config = _service.BuildConfiguration(input);

        Assert.AreEqual("example.com", config[ConfigurationKeys.PingCheck.Host].GetString());
        Assert.AreEqual(4, config[ConfigurationKeys.PingCheck.PacketCount].GetInt32());
        Assert.AreEqual(50, config[ConfigurationKeys.PingCheck.PacketLossThresholdPercent].GetInt32());
    }

    [TestMethod]
    public void BuildConfigurationShouldUsePingDefaults()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Ping,
            PingHost = "example.com",
            PingPacketCount = null,
            PingPacketLossThresholdPercent = null
        };

        var config = _service.BuildConfiguration(input);

        Assert.AreEqual(CheckDefaults.PingPacketCount, config[ConfigurationKeys.PingCheck.PacketCount].GetInt32());
        Assert.AreEqual(CheckDefaults.PingPacketLossThresholdPercent, config[ConfigurationKeys.PingCheck.PacketLossThresholdPercent].GetInt32());
    }

    [TestMethod]
    public void PopulateFromConfigurationShouldRestorePingFields()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Ping,
            PingHost = "example.com",
            PingPacketCount = 10,
            PingPacketLossThresholdPercent = 25
        };

        var config = _service.BuildConfiguration(input);
        var restored = new CheckInputBase { CheckType = CheckTypes.Ping };
        _service.PopulateFromConfiguration(restored, config);

        Assert.AreEqual("example.com", restored.PingHost);
        Assert.AreEqual(10, restored.PingPacketCount);
        Assert.AreEqual(25, restored.PingPacketLossThresholdPercent);
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectPingInvalidThreshold()
    {
        var modelState = new ModelStateDictionary();
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Ping,
            PingHost = "example.com",
            PingPacketCount = 4,
            PingPacketLossThresholdPercent = 150
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.PingPacketLossThresholdPercent)));
    }

    #endregion

    #region DNS Check Tests

    [TestMethod]
    public void BuildConfigurationShouldCreateDnsConfiguration()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Dns,
            DnsHostname = "example.com",
            DnsRecordType = "A",
            DnsExpectedValues = "1.1.1.1\n1.0.0.1"
        };

        var config = _service.BuildConfiguration(input);

        Assert.AreEqual("example.com", config[ConfigurationKeys.DnsCheck.Hostname].GetString());
        Assert.AreEqual("A", config[ConfigurationKeys.DnsCheck.RecordType].GetString());
        CollectionAssert.AreEqual(new[] { "1.1.1.1", "1.0.0.1" }, config[ConfigurationKeys.DnsCheck.ExpectedValues].EnumerateArray().Select(e => e.GetString()).ToArray());
    }

    [TestMethod]
    public void BuildConfigurationShouldExcludeDnsEmptyExpectedValues()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Dns,
            DnsHostname = "example.com",
            DnsRecordType = "A",
            DnsExpectedValues = null
        };

        var config = _service.BuildConfiguration(input);

        Assert.IsFalse(config.ContainsKey(ConfigurationKeys.DnsCheck.ExpectedValues));
    }

    [TestMethod]
    public void PopulateFromConfigurationShouldRestoreDnsFields()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Dns,
            DnsHostname = "example.com",
            DnsRecordType = "AAAA",
            DnsExpectedValues = "2001:0db8::1\n2001:0db8::2"
        };

        var config = _service.BuildConfiguration(input);
        var restored = new CheckInputBase { CheckType = CheckTypes.Dns };
        _service.PopulateFromConfiguration(restored, config);

        Assert.AreEqual("example.com", restored.DnsHostname);
        Assert.AreEqual("AAAA", restored.DnsRecordType);
        Assert.AreEqual("2001:0db8::1\n2001:0db8::2", restored.DnsExpectedValues);
    }

    #endregion

    #region TLS Check Tests

    [TestMethod]
    public void BuildConfigurationShouldCreateTlsConfiguration()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Tls,
            TlsUrl = "https://example.com",
            TlsDaysBeforeExpiryWarning = 14,
            TlsCustomCaCertificate = "-----BEGIN CERTIFICATE-----\nMIIC..."
        };

        var config = _service.BuildConfiguration(input);

        Assert.AreEqual("https://example.com", config[ConfigurationKeys.TlsCheck.Url].GetString());
        Assert.AreEqual(14, config[ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning].GetInt32());
        Assert.StartsWith("-----BEGIN CERTIFICATE-----", config[ConfigurationKeys.TlsCheck.CustomCaCertificate].GetString());
    }

    [TestMethod]
    public void BuildConfigurationShouldExcludeTlsOptionalCaCertificate()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Tls,
            TlsUrl = "https://example.com",
            TlsDaysBeforeExpiryWarning = 30,
            TlsCustomCaCertificate = null
        };

        var config = _service.BuildConfiguration(input);

        Assert.IsFalse(config.ContainsKey(ConfigurationKeys.TlsCheck.CustomCaCertificate));
    }

    [TestMethod]
    public void PopulateFromConfigurationShouldRestoreTlsFields()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Tls,
            TlsUrl = "https://example.com",
            TlsDaysBeforeExpiryWarning = 7,
            TlsCustomCaCertificate = "-----BEGIN CERTIFICATE-----\nMIIC..."
        };

        var config = _service.BuildConfiguration(input);
        var restored = new CheckInputBase { CheckType = CheckTypes.Tls };
        _service.PopulateFromConfiguration(restored, config);

        Assert.AreEqual("https://example.com", restored.TlsUrl);
        Assert.AreEqual(7, restored.TlsDaysBeforeExpiryWarning);
        Assert.AreEqual("-----BEGIN CERTIFICATE-----\nMIIC...", restored.TlsCustomCaCertificate);
    }

    #endregion

    #region Script Check Tests

    [TestMethod]
    public void BuildConfigurationShouldCreateScriptConfiguration()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Script,
            ScriptPath = "/usr/local/bin/check.sh",
            ScriptArguments = "--verbose --timeout=10",
            ScriptExpectedExitCode = 0
        };

        var config = _service.BuildConfiguration(input);

        Assert.AreEqual("/usr/local/bin/check.sh", config[ConfigurationKeys.ScriptCheck.Path].GetString());
        Assert.AreEqual("--verbose --timeout=10", config[ConfigurationKeys.ScriptCheck.Arguments].GetString());
        Assert.AreEqual(0, config[ConfigurationKeys.ScriptCheck.ExpectedExitCode].GetInt32());
    }

    [TestMethod]
    public void BuildConfigurationShouldExcludeScriptEmptyArguments()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Script,
            ScriptPath = "/usr/local/bin/check.sh",
            ScriptArguments = null,
            ScriptExpectedExitCode = 0
        };

        var config = _service.BuildConfiguration(input);

        Assert.IsFalse(config.ContainsKey(ConfigurationKeys.ScriptCheck.Arguments));
    }

    [TestMethod]
    public void PopulateFromConfigurationShouldRestoreScriptFields()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Script,
            ScriptPath = "/opt/scripts/monitor.py",
            ScriptArguments = "--check-all",
            ScriptExpectedExitCode = 0
        };

        var config = _service.BuildConfiguration(input);
        var restored = new CheckInputBase { CheckType = CheckTypes.Script };
        _service.PopulateFromConfiguration(restored, config);

        Assert.AreEqual("/opt/scripts/monitor.py", restored.ScriptPath);
        Assert.AreEqual("--check-all", restored.ScriptArguments);
        Assert.AreEqual(0, restored.ScriptExpectedExitCode);
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectScriptMissingPath()
    {
        var modelState = new ModelStateDictionary();
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Script,
            ScriptPath = null
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.ScriptPath)));
    }

    [TestMethod]
    public void ValidateConfigurationShouldAcceptScriptWithNonZeroExpectedExitCode()
    {
        var modelState = new ModelStateDictionary();
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Script,
            ScriptPath = "/path/to/script.sh",
            ScriptExpectedExitCode = 42
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsTrue(modelState.IsValid);
    }

    [TestMethod]
    public void BuildConfigurationShouldUseDefaultExpectedExitCode()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Script,
            ScriptPath = "/usr/local/bin/check.sh",
            ScriptExpectedExitCode = null
        };

        var config = _service.BuildConfiguration(input);

        Assert.AreEqual(CheckDefaults.ScriptExpectedExitCode, config[ConfigurationKeys.ScriptCheck.ExpectedExitCode].GetInt32());
    }

    [TestMethod]
    public void ScriptCheckShouldRoundTripAllFieldsCorrectly()
    {
        var original = new CheckInputBase
        {
            CheckType = CheckTypes.Script,
            ScriptPath = "/opt/monitoring/health-check.py",
            ScriptArguments = "--mode production --timeout 30",
            ScriptExpectedExitCode = 0
        };

        var config = _service.BuildConfiguration(original);
        var restored = new CheckInputBase { CheckType = CheckTypes.Script };
        _service.PopulateFromConfiguration(restored, config);

        Assert.AreEqual(original.ScriptPath, restored.ScriptPath);
        Assert.AreEqual(original.ScriptArguments, restored.ScriptArguments);
        Assert.AreEqual(original.ScriptExpectedExitCode, restored.ScriptExpectedExitCode);
    }

    #endregion

    #region Round-Trip Tests

    [TestMethod]
    public void HttpCheckShouldRoundTripAllFieldsCorrectly()
    {
        var original = new CheckInputBase
        {
            CheckType = CheckTypes.Http,
            HttpUrl = "https://api.example.com/v2/status",
            HttpMethod = "POST",
            HttpExpectedStatusCodes = "200, 201, 202",
            HttpHeaders = "Authorization: Bearer token123\r\nX-Custom: value",
            HttpBody = "{\"query\":{\"status\":\"active\"}}",
            HttpContentValidation = "\"status\":\"ok\"",
            HttpFollowRedirects = false,
            HttpAllowInvalidSsl = true,
            HttpResponseTimeWarnThresholdMs = 3000
        };

        var config = _service.BuildConfiguration(original);
        var restored = new CheckInputBase { CheckType = CheckTypes.Http };
        _service.PopulateFromConfiguration(restored, config);

        Assert.AreEqual(original.HttpUrl, restored.HttpUrl);
        Assert.AreEqual(original.HttpMethod, restored.HttpMethod);
        Assert.AreEqual(original.HttpExpectedStatusCodes, restored.HttpExpectedStatusCodes);
        Assert.AreEqual(original.HttpHeaders, restored.HttpHeaders);
        Assert.AreEqual(original.HttpBody, restored.HttpBody);
        Assert.AreEqual(original.HttpContentValidation, restored.HttpContentValidation);
        Assert.AreEqual(original.HttpFollowRedirects, restored.HttpFollowRedirects);
        Assert.AreEqual(original.HttpAllowInvalidSsl, restored.HttpAllowInvalidSsl);
        Assert.AreEqual(original.HttpResponseTimeWarnThresholdMs, restored.HttpResponseTimeWarnThresholdMs);
    }

    [TestMethod]
    public void TcpCheckShouldRoundTripAllFieldsCorrectly()
    {
        var original = new CheckInputBase
        {
            CheckType = CheckTypes.Tcp,
            TcpHost = "db.example.com",
            TcpPort = 5432,
            TcpConnectionTimeWarnThresholdMs = 750
        };

        var config = _service.BuildConfiguration(original);
        var restored = new CheckInputBase { CheckType = CheckTypes.Tcp };
        _service.PopulateFromConfiguration(restored, config);

        Assert.AreEqual(original.TcpHost, restored.TcpHost);
        Assert.AreEqual(original.TcpPort, restored.TcpPort);
        Assert.AreEqual(original.TcpConnectionTimeWarnThresholdMs, restored.TcpConnectionTimeWarnThresholdMs);
    }

    [TestMethod]
    public void DnsCheckShouldRoundTripAllFieldsCorrectly()
    {
        var original = new CheckInputBase
        {
            CheckType = CheckTypes.Dns,
            DnsHostname = "www.example.com",
            DnsRecordType = "CNAME",
            DnsExpectedValues = "example.com\nexample.net"
        };

        var config = _service.BuildConfiguration(original);
        var restored = new CheckInputBase { CheckType = CheckTypes.Dns };
        _service.PopulateFromConfiguration(restored, config);

        Assert.AreEqual(original.DnsHostname, restored.DnsHostname);
        Assert.AreEqual(original.DnsRecordType, restored.DnsRecordType);
        Assert.AreEqual(original.DnsExpectedValues, restored.DnsExpectedValues);
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void BuildConfigurationShouldReturnEmptyDictionaryForUnknownCheckType()
    {
        var input = new CheckInputBase
        {
            CheckType = "UnknownType"
        };

        var config = _service.BuildConfiguration(input);

        Assert.IsEmpty(config);
    }

    [TestMethod]
    public void PopulateFromConfigurationShouldHandleEmptyConfiguration()
    {
        var input = new CheckInputBase { CheckType = CheckTypes.Http };
        var config = new Dictionary<string, JsonElement>();

        _service.PopulateFromConfiguration(input, config);

        Assert.IsNull(input.HttpUrl);
        Assert.AreEqual(CheckDefaults.HttpMethod, input.HttpMethod);
    }

    [TestMethod]
    public void ValidateConfigurationShouldNotAddErrorsForValidScript()
    {
        var modelState = new ModelStateDictionary();
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Script,
            ScriptPath = "/valid/path/script.sh"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsTrue(modelState.IsValid);
    }

    [TestMethod]
    public void ValidateConfigurationShouldAcceptInlineScriptWithValidPlaceholder()
    {
        var modelState = new ModelStateDictionary();
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Script,
            ScriptPath = "bash",
            ScriptArguments = CheckDefaults.ScriptFilePlaceholder,
            ScriptContent = "echo 'test'"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsTrue(modelState.IsValid);
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectInlineScriptMissingPlaceholder()
    {
        var modelState = new ModelStateDictionary();
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Script,
            ScriptPath = "bash",
            ScriptArguments = "--some-flag",
            ScriptContent = "echo 'test'"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.ScriptArguments)));
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectInlineScriptWithNoArguments()
    {
        var modelState = new ModelStateDictionary();
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Script,
            ScriptPath = "bash",
            ScriptContent = "echo 'test'"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.ScriptArguments)));
    }

    [TestMethod]
    public void BuildConfigurationShouldIncludeScriptContent()
    {
        var input = new CheckInputBase
        {
            CheckType = CheckTypes.Script,
            ScriptPath = "python3",
            ScriptArguments = $"-u {CheckDefaults.ScriptFilePlaceholder}",
            ScriptContent = "print('hello')"
        };

        var config = _service.BuildConfiguration(input);

        Assert.IsTrue(config.ContainsKey(ConfigurationKeys.ScriptCheck.Content));
        Assert.AreEqual("print('hello')", config[ConfigurationKeys.ScriptCheck.Content].GetString());
    }

    [TestMethod]
    public void PopulateFromConfigurationShouldRestoreScriptContent()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.ScriptCheck.Path] = JsonSerializer.SerializeToElement("bash"),
            [ConfigurationKeys.ScriptCheck.Arguments] = JsonSerializer.SerializeToElement(CheckDefaults.ScriptFilePlaceholder),
            [ConfigurationKeys.ScriptCheck.Content] = JsonSerializer.SerializeToElement("echo 'test'"),
            [ConfigurationKeys.ScriptCheck.ExpectedExitCode] = JsonSerializer.SerializeToElement(0)
        };

        var input = new CheckInputBase { CheckType = CheckTypes.Script };
        _service.PopulateFromConfiguration(input, config);

        Assert.AreEqual("echo 'test'", input.ScriptContent);
    }

    #endregion
}
