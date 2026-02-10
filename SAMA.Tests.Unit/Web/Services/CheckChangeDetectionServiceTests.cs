using System.Text.Json;
using SAMA.Data.Entities;
using SAMA.Shared.Constants;
using SAMA.Web.Services;

namespace SAMA.Tests.Unit.Web.Services;

[TestClass]
public class CheckChangeDetectionServiceTests
{
    private CheckChangeDetectionService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new CheckChangeDetectionService();
    }

    [TestMethod]
    public void DetectChangesShouldDetectNameChange()
    {
        var oldCheck = CreateCheck("Old Name");
        var changes = _service.DetectChanges(
            oldCheck,
            "New Name",
            oldCheck.Description,
            oldCheck.CheckType,
            oldCheck.Schedule,
            oldCheck.TimeoutSeconds,
            oldCheck.ConfigurationJson,
            oldCheck.Enabled);

        Assert.IsTrue(changes.ContainsKey("Name"));
        Assert.IsTrue(changes.ContainsKey("Updated At"));
        Assert.HasCount(2, changes);
    }

    [TestMethod]
    public void DetectChangesShouldDetectDescriptionChange()
    {
        var oldCheck = CreateCheck("Test", description: "Old description");
        var changes = _service.DetectChanges(
            oldCheck,
            oldCheck.Name,
            "New description",
            oldCheck.CheckType,
            oldCheck.Schedule,
            oldCheck.TimeoutSeconds,
            oldCheck.ConfigurationJson,
            oldCheck.Enabled);

        Assert.IsTrue(changes.ContainsKey("Description"));
        Assert.IsTrue(changes.ContainsKey("Updated At"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectCheckTypeChange()
    {
        var oldCheck = CreateCheck("Test", checkType: CheckTypes.Http);
        var changes = _service.DetectChanges(
            oldCheck,
            oldCheck.Name,
            oldCheck.Description,
            CheckTypes.Tcp,
            oldCheck.Schedule,
            oldCheck.TimeoutSeconds,
            new Dictionary<string, JsonElement>(),
            oldCheck.Enabled);

        Assert.IsTrue(changes.ContainsKey("Check Type"));
        Assert.IsTrue(changes.ContainsKey("Updated At"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectScheduleChange()
    {
        var oldCheck = CreateCheck("Test", schedule: "60");
        var changes = _service.DetectChanges(
            oldCheck,
            oldCheck.Name,
            oldCheck.Description,
            oldCheck.CheckType,
            "120",
            oldCheck.TimeoutSeconds,
            oldCheck.ConfigurationJson,
            oldCheck.Enabled);

        Assert.IsTrue(changes.ContainsKey("Schedule"));
        Assert.IsTrue(changes.ContainsKey("Updated At"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectTimeoutChange()
    {
        var oldCheck = CreateCheck("Test", timeoutSeconds: 30);
        var changes = _service.DetectChanges(
            oldCheck,
            oldCheck.Name,
            oldCheck.Description,
            oldCheck.CheckType,
            oldCheck.Schedule,
            45,
            oldCheck.ConfigurationJson,
            oldCheck.Enabled);

        Assert.IsTrue(changes.ContainsKey("Timeout"));
        Assert.IsTrue(changes.ContainsKey("Updated At"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectEnabledChange()
    {
        var oldCheck = CreateCheck("Test", enabled: true);
        var changes = _service.DetectChanges(
            oldCheck,
            oldCheck.Name,
            oldCheck.Description,
            oldCheck.CheckType,
            oldCheck.Schedule,
            oldCheck.TimeoutSeconds,
            oldCheck.ConfigurationJson,
            false);

        Assert.IsTrue(changes.ContainsKey("Enabled"));
        Assert.IsTrue(changes.ContainsKey("Updated At"));
    }

    [TestMethod]
    public void DetectChangesShouldAlwaysIncludeUpdatedAt()
    {
        var oldCheck = CreateCheck("Test");
        var changes = _service.DetectChanges(
            oldCheck,
            oldCheck.Name,
            oldCheck.Description,
            oldCheck.CheckType,
            oldCheck.Schedule,
            oldCheck.TimeoutSeconds,
            oldCheck.ConfigurationJson,
            oldCheck.Enabled);

        Assert.IsTrue(changes.ContainsKey("Updated At"));
        Assert.HasCount(1, changes);
    }

    [TestMethod]
    public void DetectChangesShouldDetectHttpUrlChange()
    {
        var oldConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://old.com"),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200 })
        };
        var newConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://new.com"),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200 })
        };
        var oldCheck = CreateCheck("Test", checkType: CheckTypes.Http, config: oldConfig);

        var changes = _service.DetectChanges(
            oldCheck,
            oldCheck.Name,
            oldCheck.Description,
            oldCheck.CheckType,
            oldCheck.Schedule,
            oldCheck.TimeoutSeconds,
            newConfig,
            oldCheck.Enabled);

        Assert.IsTrue(changes.ContainsKey("URL"));
        Assert.IsTrue(changes.ContainsKey("Updated At"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectHttpMethodChange()
    {
        var oldConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement("GET")
        };
        var newConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement("POST")
        };
        var oldCheck = CreateCheck("Test", checkType: CheckTypes.Http, config: oldConfig);

        var changes = _service.DetectChanges(
            oldCheck,
            oldCheck.Name,
            oldCheck.Description,
            oldCheck.CheckType,
            oldCheck.Schedule,
            oldCheck.TimeoutSeconds,
            newConfig,
            oldCheck.Enabled);

        Assert.IsTrue(changes.ContainsKey("Method"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectHttpStatusCodesChange()
    {
        var oldConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200 })
        };
        var newConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200, 201 })
        };
        var oldCheck = CreateCheck("Test", checkType: CheckTypes.Http, config: oldConfig);

        var changes = _service.DetectChanges(
            oldCheck,
            oldCheck.Name,
            oldCheck.Description,
            oldCheck.CheckType,
            oldCheck.Schedule,
            oldCheck.TimeoutSeconds,
            newConfig,
            oldCheck.Enabled);

        Assert.IsTrue(changes.ContainsKey("Expected Status Codes"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectTcpHostChange()
    {
        var oldConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TcpCheck.Host] = JsonSerializer.SerializeToElement("old.example.com"),
            [ConfigurationKeys.TcpCheck.Port] = JsonSerializer.SerializeToElement(443)
        };
        var newConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TcpCheck.Host] = JsonSerializer.SerializeToElement("new.example.com"),
            [ConfigurationKeys.TcpCheck.Port] = JsonSerializer.SerializeToElement(443)
        };
        var oldCheck = CreateCheck("Test", checkType: CheckTypes.Tcp, config: oldConfig);

        var changes = _service.DetectChanges(
            oldCheck,
            oldCheck.Name,
            oldCheck.Description,
            oldCheck.CheckType,
            oldCheck.Schedule,
            oldCheck.TimeoutSeconds,
            newConfig,
            oldCheck.Enabled);

        Assert.IsTrue(changes.ContainsKey("Host"));
        Assert.IsFalse(changes.ContainsKey("Port"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectTcpPortChange()
    {
        var oldConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TcpCheck.Host] = JsonSerializer.SerializeToElement("example.com"),
            [ConfigurationKeys.TcpCheck.Port] = JsonSerializer.SerializeToElement(80)
        };
        var newConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TcpCheck.Host] = JsonSerializer.SerializeToElement("example.com"),
            [ConfigurationKeys.TcpCheck.Port] = JsonSerializer.SerializeToElement(443)
        };
        var oldCheck = CreateCheck("Test", checkType: CheckTypes.Tcp, config: oldConfig);

        var changes = _service.DetectChanges(
            oldCheck,
            oldCheck.Name,
            oldCheck.Description,
            oldCheck.CheckType,
            oldCheck.Schedule,
            oldCheck.TimeoutSeconds,
            newConfig,
            oldCheck.Enabled);

        Assert.IsTrue(changes.ContainsKey("Port"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectAddedConfigurationField()
    {
        var oldConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com")
        };
        var newConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com"),
            [ConfigurationKeys.HttpCheck.Headers] = JsonSerializer.SerializeToElement("Authorization: Bearer token")
        };
        var oldCheck = CreateCheck("Test", checkType: CheckTypes.Http, config: oldConfig);

        var changes = _service.DetectChanges(
            oldCheck,
            oldCheck.Name,
            oldCheck.Description,
            oldCheck.CheckType,
            oldCheck.Schedule,
            oldCheck.TimeoutSeconds,
            newConfig,
            oldCheck.Enabled);

        Assert.IsTrue(changes.ContainsKey("Headers"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectRemovedConfigurationField()
    {
        var oldConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com"),
            [ConfigurationKeys.HttpCheck.Headers] = JsonSerializer.SerializeToElement("Authorization: Bearer token")
        };
        var newConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com")
        };
        var oldCheck = CreateCheck("Test", checkType: CheckTypes.Http, config: oldConfig);

        var changes = _service.DetectChanges(
            oldCheck,
            oldCheck.Name,
            oldCheck.Description,
            oldCheck.CheckType,
            oldCheck.Schedule,
            oldCheck.TimeoutSeconds,
            newConfig,
            oldCheck.Enabled);

        Assert.IsTrue(changes.ContainsKey("Headers"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectPingPacketCountChange()
    {
        var oldConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.PingCheck.Host] = JsonSerializer.SerializeToElement("example.com"),
            [ConfigurationKeys.PingCheck.PacketCount] = JsonSerializer.SerializeToElement(4)
        };
        var newConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.PingCheck.Host] = JsonSerializer.SerializeToElement("example.com"),
            [ConfigurationKeys.PingCheck.PacketCount] = JsonSerializer.SerializeToElement(10)
        };
        var oldCheck = CreateCheck("Test", checkType: CheckTypes.Ping, config: oldConfig);

        var changes = _service.DetectChanges(
            oldCheck,
            oldCheck.Name,
            oldCheck.Description,
            oldCheck.CheckType,
            oldCheck.Schedule,
            oldCheck.TimeoutSeconds,
            newConfig,
            oldCheck.Enabled);

        Assert.IsTrue(changes.ContainsKey("Packet Count"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectDnsRecordTypeChange()
    {
        var oldConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.DnsCheck.Hostname] = JsonSerializer.SerializeToElement("example.com"),
            [ConfigurationKeys.DnsCheck.RecordType] = JsonSerializer.SerializeToElement("A")
        };
        var newConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.DnsCheck.Hostname] = JsonSerializer.SerializeToElement("example.com"),
            [ConfigurationKeys.DnsCheck.RecordType] = JsonSerializer.SerializeToElement("AAAA")
        };
        var oldCheck = CreateCheck("Test", checkType: CheckTypes.Dns, config: oldConfig);

        var changes = _service.DetectChanges(
            oldCheck,
            oldCheck.Name,
            oldCheck.Description,
            oldCheck.CheckType,
            oldCheck.Schedule,
            oldCheck.TimeoutSeconds,
            newConfig,
            oldCheck.Enabled);

        Assert.IsTrue(changes.ContainsKey("Record Type"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectTlsDaysBeforeExpiryChange()
    {
        var oldConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.Url] = JsonSerializer.SerializeToElement("https://example.com"),
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(30)
        };
        var newConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.Url] = JsonSerializer.SerializeToElement("https://example.com"),
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(14)
        };
        var oldCheck = CreateCheck("Test", checkType: CheckTypes.Tls, config: oldConfig);

        var changes = _service.DetectChanges(
            oldCheck,
            oldCheck.Name,
            oldCheck.Description,
            oldCheck.CheckType,
            oldCheck.Schedule,
            oldCheck.TimeoutSeconds,
            newConfig,
            oldCheck.Enabled);

        Assert.IsTrue(changes.ContainsKey("Days Before Expiry Warning"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectScriptPathChange()
    {
        var oldConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.ScriptCheck.Path] = JsonSerializer.SerializeToElement("/old/path/script.sh")
        };
        var newConfig = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.ScriptCheck.Path] = JsonSerializer.SerializeToElement("/new/path/script.sh")
        };
        var oldCheck = CreateCheck("Test", checkType: CheckTypes.Script, config: oldConfig);

        var changes = _service.DetectChanges(
            oldCheck,
            oldCheck.Name,
            oldCheck.Description,
            oldCheck.CheckType,
            oldCheck.Schedule,
            oldCheck.TimeoutSeconds,
            newConfig,
            oldCheck.Enabled);

        Assert.IsTrue(changes.ContainsKey("Path"));
    }

    [TestMethod]
    public void DetectChangesShouldHandleMultipleChanges()
    {
        var oldCheck = CreateCheck("Old Name", "Old description", CheckTypes.Http, "60", 30, true);
        var changes = _service.DetectChanges(
            oldCheck,
            "New Name",
            "New description",
            CheckTypes.Http,
            "120",
            45,
            oldCheck.ConfigurationJson,
            false);

        Assert.IsTrue(changes.ContainsKey("Name"));
        Assert.IsTrue(changes.ContainsKey("Description"));
        Assert.IsTrue(changes.ContainsKey("Schedule"));
        Assert.IsTrue(changes.ContainsKey("Timeout"));
        Assert.IsTrue(changes.ContainsKey("Enabled"));
        Assert.IsTrue(changes.ContainsKey("Updated At"));
        Assert.HasCount(6, changes);
    }

    [TestMethod]
    public void DetectChangesShouldNotDetectChangesWhenNothingChanged()
    {
        var oldCheck = CreateCheck("Test");
        var changes = _service.DetectChanges(
            oldCheck,
            oldCheck.Name,
            oldCheck.Description,
            oldCheck.CheckType,
            oldCheck.Schedule,
            oldCheck.TimeoutSeconds,
            oldCheck.ConfigurationJson,
            oldCheck.Enabled);

        Assert.HasCount(1, changes);
        Assert.IsTrue(changes.ContainsKey("Updated At"));
    }

    private Check CreateCheck(
        string name,
        string? description = null,
        string checkType = CheckTypes.Http,
        string schedule = "60",
        int timeoutSeconds = 30,
        bool enabled = true,
        Dictionary<string, JsonElement>? config = null)
    {
        return new Check
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = name,
            Description = description,
            CheckType = checkType,
            ConfigurationJson = config ?? new Dictionary<string, JsonElement>(),
            Schedule = schedule,
            TimeoutSeconds = timeoutSeconds,
            Enabled = enabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
