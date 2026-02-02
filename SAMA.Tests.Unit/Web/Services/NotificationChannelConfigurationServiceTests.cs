using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SAMA.Shared.Constants;
using SAMA.Web.Constants;
using SAMA.Web.Models;
using SAMA.Web.Services;

namespace SAMA.Tests.Unit.Web.Services;

[TestClass]
public class NotificationChannelConfigurationServiceTests
{
    private NotificationChannelConfigurationService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new NotificationChannelConfigurationService();
    }

    #region Email Channel Tests

    [TestMethod]
    public void BuildConfigurationShouldCreateEmailConfigurationWithAllRequiredFields()
    {
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Email,
            EmailSmtpHost = "smtp.example.com",
            EmailSmtpPort = 587,
            EmailUseSsl = true,
            EmailSmtpUsername = "user@example.com",
            EmailSmtpPassword = "password123",
            EmailFromAddress = "noreply@example.com",
            EmailRecipients = "admin@example.com, ops@example.com"
        };

        var config = _service.BuildConfiguration(input);

        Assert.AreEqual("smtp.example.com", config[ConfigurationKeys.Email.SmtpHost].GetString());
        Assert.AreEqual(587, config[ConfigurationKeys.Email.SmtpPort].GetInt32());
        Assert.IsTrue(config[ConfigurationKeys.Email.UseSsl].GetBoolean());
        Assert.AreEqual("user@example.com", config[ConfigurationKeys.Email.Username].GetString());
        Assert.AreEqual("password123", config[ConfigurationKeys.Email.Password].GetString());
        Assert.AreEqual("noreply@example.com", config[ConfigurationKeys.Email.FromAddress].GetString());
        CollectionAssert.AreEqual(
            new[] { "admin@example.com", "ops@example.com" },
            config[ConfigurationKeys.Email.Recipients].EnumerateArray().Select(e => e.GetString()).ToArray());
    }

    [TestMethod]
    public void BuildConfigurationShouldHandleEmailEmptyCredentials()
    {
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Email,
            EmailSmtpHost = "smtp.example.com",
            EmailSmtpPort = 25,
            EmailUseSsl = false,
            EmailSmtpUsername = null,
            EmailSmtpPassword = null,
            EmailFromAddress = "noreply@example.com",
            EmailRecipients = "admin@example.com"
        };

        var config = _service.BuildConfiguration(input);

        Assert.AreEqual("", config[ConfigurationKeys.Email.Username].GetString());
        Assert.AreEqual("", config[ConfigurationKeys.Email.Password].GetString());
    }

    [TestMethod]
    public void BuildConfigurationShouldTrimEmailRecipients()
    {
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Email,
            EmailSmtpHost = "smtp.example.com",
            EmailSmtpPort = 587,
            EmailFromAddress = "noreply@example.com",
            EmailRecipients = "  admin@example.com  ,  ops@example.com  ,  dev@example.com  "
        };

        var config = _service.BuildConfiguration(input);

        CollectionAssert.AreEqual(
            new[] { "admin@example.com", "ops@example.com", "dev@example.com" },
            config[ConfigurationKeys.Email.Recipients].EnumerateArray().Select(e => e.GetString()).ToArray());
    }

    [TestMethod]
    public void PopulateFromConfigurationShouldRestoreAllEmailFields()
    {
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Email,
            EmailSmtpHost = "smtp.example.com",
            EmailSmtpPort = 587,
            EmailUseSsl = true,
            EmailSmtpUsername = "user@example.com",
            EmailSmtpPassword = "secret",
            EmailFromAddress = "alerts@example.com",
            EmailRecipients = "admin@example.com, ops@example.com"
        };

        var config = _service.BuildConfiguration(input);
        var restored = new NotificationChannelInputBase { ChannelType = ChannelTypes.Email };
        _service.PopulateFromConfiguration(restored, config);

        Assert.AreEqual("smtp.example.com", restored.EmailSmtpHost);
        Assert.AreEqual(587, restored.EmailSmtpPort);
        Assert.IsTrue(restored.EmailUseSsl);
        Assert.AreEqual("user@example.com", restored.EmailSmtpUsername);
        Assert.AreEqual("secret", restored.EmailSmtpPassword);
        Assert.AreEqual("alerts@example.com", restored.EmailFromAddress);
        Assert.AreEqual("admin@example.com, ops@example.com", restored.EmailRecipients);
    }

    [TestMethod]
    public void PopulateFromConfigurationShouldUseEmailDefaultForMissingSsl()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.Email.SmtpHost] = JsonSerializer.SerializeToElement("smtp.example.com"),
            [ConfigurationKeys.Email.SmtpPort] = JsonSerializer.SerializeToElement(587),
            [ConfigurationKeys.Email.FromAddress] = JsonSerializer.SerializeToElement("test@example.com"),
            [ConfigurationKeys.Email.Recipients] = JsonSerializer.SerializeToElement(new[] { "admin@example.com" })
        };

        var input = new NotificationChannelInputBase { ChannelType = ChannelTypes.Email };
        _service.PopulateFromConfiguration(input, config);

        Assert.IsFalse(input.EmailUseSsl);
    }

    [TestMethod]
    public void ValidateConfigurationShouldAcceptValidEmailConfiguration()
    {
        var modelState = new ModelStateDictionary();
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Email,
            EmailSmtpHost = "smtp.example.com",
            EmailSmtpPort = 587,
            EmailFromAddress = "noreply@example.com",
            EmailRecipients = "admin@example.com"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsTrue(modelState.IsValid);
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectEmailMissingSmtpHost()
    {
        var modelState = new ModelStateDictionary();
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Email,
            EmailSmtpHost = null,
            EmailSmtpPort = 587,
            EmailFromAddress = "noreply@example.com",
            EmailRecipients = "admin@example.com"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.EmailSmtpHost)));
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectEmailInvalidPort()
    {
        var modelState = new ModelStateDictionary();
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Email,
            EmailSmtpHost = "smtp.example.com",
            EmailSmtpPort = 0,
            EmailFromAddress = "noreply@example.com",
            EmailRecipients = "admin@example.com"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.EmailSmtpPort)));
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectEmailMissingFromAddress()
    {
        var modelState = new ModelStateDictionary();
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Email,
            EmailSmtpHost = "smtp.example.com",
            EmailSmtpPort = 587,
            EmailFromAddress = null,
            EmailRecipients = "admin@example.com"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.EmailFromAddress)));
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectEmailMissingRecipients()
    {
        var modelState = new ModelStateDictionary();
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Email,
            EmailSmtpHost = "smtp.example.com",
            EmailSmtpPort = 587,
            EmailFromAddress = "noreply@example.com",
            EmailRecipients = null
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.EmailRecipients)));
    }

    #endregion

    #region Slack Channel Tests

    [TestMethod]
    public void BuildConfigurationShouldCreateSlackConfiguration()
    {
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Slack,
            SlackWebhookUrl = "https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXX"
        };

        var config = _service.BuildConfiguration(input);

        Assert.AreEqual(
            "https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXX",
            config[ConfigurationKeys.Webhook.WebhookUrl].GetString());
    }

    [TestMethod]
    public void PopulateFromConfigurationShouldRestoreSlackFields()
    {
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Slack,
            SlackWebhookUrl = "https://hooks.slack.com/services/TEST"
        };

        var config = _service.BuildConfiguration(input);
        var restored = new NotificationChannelInputBase { ChannelType = ChannelTypes.Slack };
        _service.PopulateFromConfiguration(restored, config);

        Assert.AreEqual("https://hooks.slack.com/services/TEST", restored.SlackWebhookUrl);
    }

    [TestMethod]
    public void ValidateConfigurationShouldAcceptValidSlackConfiguration()
    {
        var modelState = new ModelStateDictionary();
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Slack,
            SlackWebhookUrl = "https://hooks.slack.com/services/TEST"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsTrue(modelState.IsValid);
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectSlackMissingWebhookUrl()
    {
        var modelState = new ModelStateDictionary();
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Slack,
            SlackWebhookUrl = null
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.SlackWebhookUrl)));
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectSlackInvalidWebhookUrl()
    {
        var modelState = new ModelStateDictionary();
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Slack,
            SlackWebhookUrl = "not-a-valid-url"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.SlackWebhookUrl)));
    }

    #endregion

    #region Teams Channel Tests

    [TestMethod]
    public void BuildConfigurationShouldCreateTeamsConfiguration()
    {
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Teams,
            TeamsWebhookUrl = "https://outlook.office.com/webhook/test"
        };

        var config = _service.BuildConfiguration(input);

        Assert.AreEqual(
            "https://outlook.office.com/webhook/test",
            config[ConfigurationKeys.Webhook.WebhookUrl].GetString());
    }

    [TestMethod]
    public void PopulateFromConfigurationShouldRestoreTeamsFields()
    {
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Teams,
            TeamsWebhookUrl = "https://outlook.office.com/webhook/test"
        };

        var config = _service.BuildConfiguration(input);
        var restored = new NotificationChannelInputBase { ChannelType = ChannelTypes.Teams };
        _service.PopulateFromConfiguration(restored, config);

        Assert.AreEqual("https://outlook.office.com/webhook/test", restored.TeamsWebhookUrl);
    }

    [TestMethod]
    public void ValidateConfigurationShouldAcceptValidTeamsConfiguration()
    {
        var modelState = new ModelStateDictionary();
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Teams,
            TeamsWebhookUrl = "https://outlook.office.com/webhook/test"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsTrue(modelState.IsValid);
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectTeamsMissingWebhookUrl()
    {
        var modelState = new ModelStateDictionary();
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Teams,
            TeamsWebhookUrl = null
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.TeamsWebhookUrl)));
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectTeamsInvalidWebhookUrl()
    {
        var modelState = new ModelStateDictionary();
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Teams,
            TeamsWebhookUrl = "invalid-url"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.TeamsWebhookUrl)));
    }

    #endregion

    #region Discord Channel Tests

    [TestMethod]
    public void BuildConfigurationShouldCreateDiscordConfiguration()
    {
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Discord,
            DiscordWebhookUrl = "https://discord.com/api/webhooks/123456789/abcdefg"
        };

        var config = _service.BuildConfiguration(input);

        Assert.AreEqual(
            "https://discord.com/api/webhooks/123456789/abcdefg",
            config[ConfigurationKeys.Webhook.WebhookUrl].GetString());
    }

    [TestMethod]
    public void PopulateFromConfigurationShouldRestoreDiscordFields()
    {
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Discord,
            DiscordWebhookUrl = "https://discord.com/api/webhooks/TEST"
        };

        var config = _service.BuildConfiguration(input);
        var restored = new NotificationChannelInputBase { ChannelType = ChannelTypes.Discord };
        _service.PopulateFromConfiguration(restored, config);

        Assert.AreEqual("https://discord.com/api/webhooks/TEST", restored.DiscordWebhookUrl);
    }

    [TestMethod]
    public void ValidateConfigurationShouldAcceptValidDiscordConfiguration()
    {
        var modelState = new ModelStateDictionary();
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Discord,
            DiscordWebhookUrl = "https://discord.com/api/webhooks/TEST"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsTrue(modelState.IsValid);
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectDiscordMissingWebhookUrl()
    {
        var modelState = new ModelStateDictionary();
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Discord,
            DiscordWebhookUrl = null
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.DiscordWebhookUrl)));
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectDiscordInvalidWebhookUrl()
    {
        var modelState = new ModelStateDictionary();
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Discord,
            DiscordWebhookUrl = "not-a-url"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.DiscordWebhookUrl)));
    }

    #endregion

    #region Script Channel Tests

    [TestMethod]
    public void BuildConfigurationShouldCreateScriptConfiguration()
    {
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Script,
            ScriptPath = "/usr/local/bin/notify.sh",
            ScriptArguments = "--level critical --channel alerts"
        };

        var config = _service.BuildConfiguration(input);

        Assert.AreEqual("/usr/local/bin/notify.sh", config[ConfigurationKeys.Script.Path].GetString());
        Assert.AreEqual("--level critical --channel alerts", config[ConfigurationKeys.Script.Arguments].GetString());
    }

    [TestMethod]
    public void BuildConfigurationShouldHandleScriptEmptyArguments()
    {
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Script,
            ScriptPath = "/usr/local/bin/notify.sh",
            ScriptArguments = null
        };

        var config = _service.BuildConfiguration(input);

        Assert.AreEqual("", config[ConfigurationKeys.Script.Arguments].GetString());
    }

    [TestMethod]
    public void PopulateFromConfigurationShouldRestoreScriptFields()
    {
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Script,
            ScriptPath = "/opt/scripts/alert.py",
            ScriptArguments = "--verbose"
        };

        var config = _service.BuildConfiguration(input);
        var restored = new NotificationChannelInputBase { ChannelType = ChannelTypes.Script };
        _service.PopulateFromConfiguration(restored, config);

        Assert.AreEqual("/opt/scripts/alert.py", restored.ScriptPath);
        Assert.AreEqual("--verbose", restored.ScriptArguments);
    }

    [TestMethod]
    public void ValidateConfigurationShouldAcceptValidScriptConfiguration()
    {
        var modelState = new ModelStateDictionary();
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Script,
            ScriptPath = "/usr/local/bin/notify.sh"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsTrue(modelState.IsValid);
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectScriptMissingPath()
    {
        var modelState = new ModelStateDictionary();
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Script,
            ScriptPath = null
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.ScriptPath)));
    }

    #endregion

    #region Round-Trip Tests

    [TestMethod]
    public void EmailChannelShouldRoundTripAllFieldsCorrectly()
    {
        var original = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Email,
            EmailSmtpHost = "smtp.gmail.com",
            EmailSmtpPort = 587,
            EmailUseSsl = true,
            EmailSmtpUsername = "alerts@example.com",
            EmailSmtpPassword = "SuperSecret123!",
            EmailFromAddress = "alerts@example.com",
            EmailRecipients = "admin@example.com, ops@example.com, dev@example.com"
        };

        var config = _service.BuildConfiguration(original);
        var restored = new NotificationChannelInputBase { ChannelType = ChannelTypes.Email };
        _service.PopulateFromConfiguration(restored, config);

        Assert.AreEqual(original.EmailSmtpHost, restored.EmailSmtpHost);
        Assert.AreEqual(original.EmailSmtpPort, restored.EmailSmtpPort);
        Assert.AreEqual(original.EmailUseSsl, restored.EmailUseSsl);
        Assert.AreEqual(original.EmailSmtpUsername, restored.EmailSmtpUsername);
        Assert.AreEqual(original.EmailSmtpPassword, restored.EmailSmtpPassword);
        Assert.AreEqual(original.EmailFromAddress, restored.EmailFromAddress);
        Assert.AreEqual(original.EmailRecipients, restored.EmailRecipients);
    }

    [TestMethod]
    public void SlackChannelShouldRoundTripAllFieldsCorrectly()
    {
        var original = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Slack,
            SlackWebhookUrl = "https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXX"
        };

        var config = _service.BuildConfiguration(original);
        var restored = new NotificationChannelInputBase { ChannelType = ChannelTypes.Slack };
        _service.PopulateFromConfiguration(restored, config);

        Assert.AreEqual(original.SlackWebhookUrl, restored.SlackWebhookUrl);
    }

    [TestMethod]
    public void TeamsChannelShouldRoundTripAllFieldsCorrectly()
    {
        var original = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Teams,
            TeamsWebhookUrl = "https://outlook.office.com/webhook/abc123"
        };

        var config = _service.BuildConfiguration(original);
        var restored = new NotificationChannelInputBase { ChannelType = ChannelTypes.Teams };
        _service.PopulateFromConfiguration(restored, config);

        Assert.AreEqual(original.TeamsWebhookUrl, restored.TeamsWebhookUrl);
    }

    [TestMethod]
    public void DiscordChannelShouldRoundTripAllFieldsCorrectly()
    {
        var original = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Discord,
            DiscordWebhookUrl = "https://discord.com/api/webhooks/123456789/TokenHere"
        };

        var config = _service.BuildConfiguration(original);
        var restored = new NotificationChannelInputBase { ChannelType = ChannelTypes.Discord };
        _service.PopulateFromConfiguration(restored, config);

        Assert.AreEqual(original.DiscordWebhookUrl, restored.DiscordWebhookUrl);
    }

    [TestMethod]
    public void ScriptChannelShouldRoundTripAllFieldsCorrectly()
    {
        var original = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Script,
            ScriptPath = "/usr/local/bin/custom-notify.sh",
            ScriptArguments = "--json --priority high"
        };

        var config = _service.BuildConfiguration(original);
        var restored = new NotificationChannelInputBase { ChannelType = ChannelTypes.Script };
        _service.PopulateFromConfiguration(restored, config);

        Assert.AreEqual(original.ScriptPath, restored.ScriptPath);
        Assert.AreEqual(original.ScriptArguments, restored.ScriptArguments);
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void BuildConfigurationShouldReturnEmptyDictionaryForUnknownChannelType()
    {
        var input = new NotificationChannelInputBase
        {
            ChannelType = "UnknownType"
        };

        var config = _service.BuildConfiguration(input);

        Assert.IsEmpty(config);
    }

    [TestMethod]
    public void PopulateFromConfigurationShouldHandleEmptyConfiguration()
    {
        var input = new NotificationChannelInputBase { ChannelType = ChannelTypes.Email };
        var config = new Dictionary<string, JsonElement>();

        _service.PopulateFromConfiguration(input, config);

        Assert.IsNull(input.EmailSmtpHost);
        Assert.IsNull(input.EmailSmtpPort);
    }

    [TestMethod]
    public void ValidateConfigurationShouldNotAddErrorsForUnknownChannelType()
    {
        var modelState = new ModelStateDictionary();
        var input = new NotificationChannelInputBase
        {
            ChannelType = "UnknownType"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsTrue(modelState.IsValid);
    }

    [TestMethod]
    public void ValidateConfigurationShouldAcceptScriptWithInlineContentAndValidPlaceholder()
    {
        var modelState = new ModelStateDictionary();
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Script,
            ScriptPath = "bash",
            ScriptArguments = ChannelDefaults.ScriptFilePlaceholder,
            ScriptContent = "echo $SAMA_CHECK_NAME"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsTrue(modelState.IsValid);
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectScriptInlineContentMissingPlaceholder()
    {
        var modelState = new ModelStateDictionary();
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Script,
            ScriptPath = "bash",
            ScriptArguments = "--flag value",
            ScriptContent = "echo $SAMA_CHECK_NAME"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.ScriptArguments)));
    }

    [TestMethod]
    public void ValidateConfigurationShouldRejectScriptInlineContentWithNoArguments()
    {
        var modelState = new ModelStateDictionary();
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Script,
            ScriptPath = "python3",
            ScriptContent = "print('hello')"
        };

        _service.ValidateConfiguration(modelState, input);

        Assert.IsFalse(modelState.IsValid);
        Assert.IsTrue(modelState.ContainsKey(nameof(input.ScriptArguments)));
    }

    [TestMethod]
    public void BuildConfigurationShouldIncludeScriptContentWhenProvided()
    {
        var input = new NotificationChannelInputBase
        {
            ChannelType = ChannelTypes.Script,
            ScriptPath = "bash",
            ScriptArguments = ChannelDefaults.ScriptFilePlaceholder,
            ScriptContent = "#!/bin/bash\necho $SAMA_CHECK_NAME"
        };

        var config = _service.BuildConfiguration(input);

        Assert.IsTrue(config.ContainsKey(ConfigurationKeys.Script.Content));
        Assert.AreEqual("#!/bin/bash\necho $SAMA_CHECK_NAME", config[ConfigurationKeys.Script.Content].GetString());
    }

    [TestMethod]
    public void PopulateFromConfigurationShouldRestoreScriptContent()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.Script.Path] = JsonSerializer.SerializeToElement("python3"),
            [ConfigurationKeys.Script.Arguments] = JsonSerializer.SerializeToElement($"-u {ChannelDefaults.ScriptFilePlaceholder}"),
            [ConfigurationKeys.Script.Content] = JsonSerializer.SerializeToElement("print('hello')")
        };

        var input = new NotificationChannelInputBase { ChannelType = ChannelTypes.Script };
        _service.PopulateFromConfiguration(input, config);

        Assert.AreEqual("print('hello')", input.ScriptContent);
    }

    #endregion
}
