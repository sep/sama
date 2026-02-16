using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Shared.Constants;
using SAMA.Web.Constants;
using SAMA.Web.Models;
using SAMA.Web.Services;
using SAMA.Web.Services.NotificationChannels;
using SAMA.Web.Wrappers;

namespace SAMA.Tests.Unit.Web.Services.NotificationChannels;

[TestClass]
public class EmailChannelHandlerTests
{
    private ISmtpClient _mockSmtpClient = null!;
    private SmtpClientFactory _mockFactory = null!;
    private GlobalSettingsService _mockGlobalSettings = null!;
    private ILogger<EmailChannelHandler> _logger = null!;
    private EmailChannelHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockSmtpClient = Substitute.For<ISmtpClient>();
        _mockFactory = Substitute.For<SmtpClientFactory>();
        _mockFactory.CreateClient().Returns(_mockSmtpClient);
        _mockGlobalSettings = Substitute.For<GlobalSettingsService>(null, null, null, null);
        _mockGlobalSettings.NotificationTimeoutSeconds.Returns(30);
        _logger = Substitute.For<ILogger<EmailChannelHandler>>();
        _handler = new EmailChannelHandler(_mockFactory, _mockGlobalSettings, _logger);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldFailWhenSmtpHostNotConfigured()
    {
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            Name = "Test Email",
            ChannelType = ChannelTypes.Email,
            ConfigurationJson = new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.Email.SmtpPort] = JsonSerializer.SerializeToElement(587),
                [ConfigurationKeys.Email.FromAddress] = JsonSerializer.SerializeToElement("from@example.com"),
                [ConfigurationKeys.Email.Recipients] = JsonSerializer.SerializeToElement(new[] { "to@example.com" })
            },
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var context = new StatusAlertContext
        {
            CheckName = "Test Check",
            CheckId = Guid.NewGuid(),
            Status = CheckStatuses.Down,
            ErrorMessage = "Connection failed",
            ResponseTimeMs = null,
            Timestamp = DateTimeOffset.UtcNow,
            WorkspaceName = "Test Workspace",
            IsRecovery = false,
            ConsecutiveFailures = 1
        };

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("SMTP host not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldFailWhenSmtpPortNotConfigured()
    {
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            Name = "Test Email",
            ChannelType = ChannelTypes.Email,
            ConfigurationJson = new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.Email.SmtpHost] = JsonSerializer.SerializeToElement("smtp.example.com"),
                [ConfigurationKeys.Email.FromAddress] = JsonSerializer.SerializeToElement("from@example.com"),
                [ConfigurationKeys.Email.Recipients] = JsonSerializer.SerializeToElement(new[] { "to@example.com" })
            },
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var context = new StatusAlertContext
        {
            CheckName = "Test Check",
            CheckId = Guid.NewGuid(),
            Status = CheckStatuses.Down,
            ErrorMessage = "Connection failed",
            ResponseTimeMs = null,
            Timestamp = DateTimeOffset.UtcNow,
            WorkspaceName = "Test Workspace",
            IsRecovery = false,
            ConsecutiveFailures = 1
        };

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("SMTP port not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldFailWhenFromAddressNotConfigured()
    {
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            Name = "Test Email",
            ChannelType = ChannelTypes.Email,
            ConfigurationJson = new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.Email.SmtpHost] = JsonSerializer.SerializeToElement("smtp.example.com"),
                [ConfigurationKeys.Email.SmtpPort] = JsonSerializer.SerializeToElement(587),
                [ConfigurationKeys.Email.Recipients] = JsonSerializer.SerializeToElement(new[] { "to@example.com" })
            },
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var context = new StatusAlertContext
        {
            CheckName = "Test Check",
            CheckId = Guid.NewGuid(),
            Status = CheckStatuses.Down,
            ErrorMessage = "Connection failed",
            ResponseTimeMs = null,
            Timestamp = DateTimeOffset.UtcNow,
            WorkspaceName = "Test Workspace",
            IsRecovery = false,
            ConsecutiveFailures = 1
        };

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("From address not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldFailWhenRecipientsNotConfigured()
    {
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            Name = "Test Email",
            ChannelType = ChannelTypes.Email,
            ConfigurationJson = new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.Email.SmtpHost] = JsonSerializer.SerializeToElement("smtp.example.com"),
                [ConfigurationKeys.Email.SmtpPort] = JsonSerializer.SerializeToElement(587),
                [ConfigurationKeys.Email.FromAddress] = JsonSerializer.SerializeToElement("from@example.com")
            },
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var context = new StatusAlertContext
        {
            CheckName = "Test Check",
            CheckId = Guid.NewGuid(),
            Status = CheckStatuses.Down,
            ErrorMessage = "Connection failed",
            ResponseTimeMs = null,
            Timestamp = DateTimeOffset.UtcNow,
            WorkspaceName = "Test Workspace",
            IsRecovery = false,
            ConsecutiveFailures = 1
        };

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Recipients not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldFailWhenRecipientsListIsEmpty()
    {
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            Name = "Test Email",
            ChannelType = ChannelTypes.Email,
            ConfigurationJson = new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.Email.SmtpHost] = JsonSerializer.SerializeToElement("smtp.example.com"),
                [ConfigurationKeys.Email.SmtpPort] = JsonSerializer.SerializeToElement(587),
                [ConfigurationKeys.Email.FromAddress] = JsonSerializer.SerializeToElement("from@example.com"),
                [ConfigurationKeys.Email.Recipients] = JsonSerializer.SerializeToElement(Array.Empty<string>())
            },
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var context = new StatusAlertContext
        {
            CheckName = "Test Check",
            CheckId = Guid.NewGuid(),
            Status = CheckStatuses.Down,
            ErrorMessage = "Connection failed",
            ResponseTimeMs = null,
            Timestamp = DateTimeOffset.UtcNow,
            WorkspaceName = "Test Workspace",
            IsRecovery = false,
            ConsecutiveFailures = 1
        };

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("No valid recipients configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldReturnSuccessWhenEmailSentSuccessfully()
    {
        var channel = CreateChannel("smtp.example.com", 587, "from@example.com", ["to@example.com"]);
        var context = CreateContext(status: CheckStatuses.Down, isRecovery: false);

        _mockSmtpClient.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _mockSmtpClient.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(string.Empty));
        _mockSmtpClient.DisconnectAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ErrorMessage);
        await _mockSmtpClient.Received(1).ConnectAsync(
            "smtp.example.com",
            587,
            SecureSocketOptions.StartTlsWhenAvailable,
            Arg.Any<CancellationToken>());
        await _mockSmtpClient.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>());
        await _mockSmtpClient.Received(1).DisconnectAsync(true, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldUseSslOnConnectWhenUseSslIsTrue()
    {
        var channel = CreateChannel("smtp.example.com", 465, "from@example.com", ["to@example.com"], useSsl: true);
        var context = CreateContext();

        _mockSmtpClient.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _mockSmtpClient.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(string.Empty));
        _mockSmtpClient.DisconnectAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsTrue(result.Success);
        await _mockSmtpClient.Received(1).ConnectAsync(
            "smtp.example.com",
            465,
            SecureSocketOptions.SslOnConnect,
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldAuthenticateWhenCredentialsProvided()
    {
        var channel = CreateChannel("smtp.example.com", 587, "from@example.com", ["to@example.com"], username: "user", password: "pass");
        var context = CreateContext();

        _mockSmtpClient.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _mockSmtpClient.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _mockSmtpClient.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(string.Empty));
        _mockSmtpClient.DisconnectAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsTrue(result.Success);
        await _mockSmtpClient.Received(1).AuthenticateAsync("user", "pass", Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldNotAuthenticateWhenNoCredentials()
    {
        var channel = CreateChannel("smtp.example.com", 587, "from@example.com", ["to@example.com"]);
        var context = CreateContext();

        _mockSmtpClient.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _mockSmtpClient.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(string.Empty));
        _mockSmtpClient.DisconnectAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsTrue(result.Success);
        await _mockSmtpClient.DidNotReceive().AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldHandleAuthenticationException()
    {
        var channel = CreateChannel("smtp.example.com", 587, "from@example.com", ["to@example.com"], username: "user", password: "wrongpass");
        var context = CreateContext();

        _mockSmtpClient.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _mockSmtpClient.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new AuthenticationException("Invalid credentials")));

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.Contains("SMTP authentication failed", result.ErrorMessage!);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldHandleSmtpCommandException()
    {
        var channel = CreateChannel("smtp.example.com", 587, "from@example.com", ["to@example.com"]);
        var context = CreateContext();

        _mockSmtpClient.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _mockSmtpClient.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.MailboxUnavailable, "Mailbox full")));

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.Contains("SMTP error", result.ErrorMessage!);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldHandleSmtpProtocolException()
    {
        var channel = CreateChannel("smtp.example.com", 587, "from@example.com", ["to@example.com"]);
        var context = CreateContext();

        _mockSmtpClient.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new SmtpProtocolException("Protocol error")));

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.Contains("SMTP protocol error", result.ErrorMessage!);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldHandleSocketException()
    {
        var channel = CreateChannel("smtp.example.com", 587, "from@example.com", ["to@example.com"]);
        var context = CreateContext();

        _mockSmtpClient.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new System.Net.Sockets.SocketException()));

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.Contains("Network error", result.ErrorMessage!);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldHandleCancellation()
    {
        var channel = CreateChannel("smtp.example.com", 587, "from@example.com", ["to@example.com"]);
        var context = CreateContext();

        using var cts = new CancellationTokenSource();

        _mockSmtpClient.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(3);
                await Task.Delay(10000, ct);
            });

        cts.CancelAfter(TimeSpan.FromMilliseconds(10));

        var result = await _handler.SendStatusAlertAsync(channel, context, cts.Token);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Request cancelled", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldHandleTimeout()
    {
        _mockGlobalSettings.NotificationTimeoutSeconds.Returns(1);

        var channel = CreateChannel("smtp.example.com", 587, "from@example.com", ["to@example.com"]);
        var context = CreateContext();

        _mockSmtpClient.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(3);
                await Task.Delay(60000, ct);
            });

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("timeout", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldFailWhenSmtpHostNotConfigured()
    {
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            Name = "Test Email",
            ChannelType = ChannelTypes.Email,
            ConfigurationJson = new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.Email.SmtpPort] = JsonSerializer.SerializeToElement(587),
                [ConfigurationKeys.Email.FromAddress] = JsonSerializer.SerializeToElement("from@example.com"),
                [ConfigurationKeys.Email.Recipients] = JsonSerializer.SerializeToElement(new[] { "to@example.com" })
            },
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var context = new LifecycleEventContext
        {
            EventType = EventTypes.CheckCreated,
            CheckId = Guid.NewGuid(),
            CheckName = "New Check",
            CheckType = CheckTypes.Http,
            WorkspaceName = "Test Workspace",
            Timestamp = DateTimeOffset.UtcNow,
            PerformedBy = "admin@example.com"
        };

        var result = await _handler.SendLifecycleEventAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("SMTP host not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldReturnSuccessWhenEmailSentSuccessfully()
    {
        var channel = CreateChannel("smtp.example.com", 587, "from@example.com", ["to@example.com"]);
        var context = CreateLifecycleContext(EventTypes.CheckCreated);

        _mockSmtpClient.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _mockSmtpClient.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(string.Empty));
        _mockSmtpClient.DisconnectAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _handler.SendLifecycleEventAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ErrorMessage);
        await _mockSmtpClient.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldFailWhenSmtpHostNotConfigured()
    {
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            Name = "Test Email",
            ChannelType = ChannelTypes.Email,
            ConfigurationJson = new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.Email.SmtpPort] = JsonSerializer.SerializeToElement(587),
                [ConfigurationKeys.Email.FromAddress] = JsonSerializer.SerializeToElement("from@example.com"),
                [ConfigurationKeys.Email.Recipients] = JsonSerializer.SerializeToElement(new[] { "to@example.com" })
            },
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var context = CreateStatusChangeEventContext();

        var result = await _handler.SendStatusChangeEventAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("SMTP host not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldReturnSuccessWhenEmailSentSuccessfully()
    {
        var channel = CreateChannel("smtp.example.com", 587, "from@example.com", ["to@example.com"]);
        var context = CreateStatusChangeEventContext();

        _mockSmtpClient.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _mockSmtpClient.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(string.Empty));
        _mockSmtpClient.DisconnectAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _handler.SendStatusChangeEventAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ErrorMessage);
        await _mockSmtpClient.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldIncludeCorrectSubjectAndBody()
    {
        var channel = CreateChannel("smtp.example.com", 587, "from@example.com", ["to@example.com"]);
        var context = CreateStatusChangeEventContext(
            checkName: "API Health Check",
            previousStatus: CheckStatuses.Up,
            newStatus: CheckStatuses.Down,
            responseTimeMs: 2500,
            errorMessage: "Connection timeout",
            workspaceName: "Production");

        string? capturedSubject = null;
        string? capturedBody = null;

        _mockSmtpClient.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _mockSmtpClient.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var message = callInfo.ArgAt<MimeMessage>(0);
                capturedSubject = message.Subject;
                capturedBody = ((TextPart)message.Body).Text;
                return Task.FromResult(string.Empty);
            });
        _mockSmtpClient.DisconnectAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _handler.SendStatusChangeEventAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(capturedSubject);
        Assert.Contains("API Health Check", capturedSubject);
        Assert.Contains("status changed", capturedSubject);

        Assert.IsNotNull(capturedBody);
        Assert.Contains("API Health Check", capturedBody);
        Assert.Contains("Up", capturedBody);
        Assert.Contains("Down", capturedBody);
        Assert.Contains("2500ms", capturedBody);
        Assert.Contains("Connection timeout", capturedBody);
        Assert.Contains("Production", capturedBody);
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldHandleSmtpCommandException()
    {
        var channel = CreateChannel("smtp.example.com", 587, "from@example.com", ["to@example.com"]);
        var context = CreateStatusChangeEventContext();

        _mockSmtpClient.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _mockSmtpClient.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.MailboxUnavailable, "Mailbox full")));

        var result = await _handler.SendStatusChangeEventAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.Contains("SMTP error", result.ErrorMessage!);
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldHandleCancellation()
    {
        var channel = CreateChannel("smtp.example.com", 587, "from@example.com", ["to@example.com"]);
        var context = CreateStatusChangeEventContext();

        using var cts = new CancellationTokenSource();

        _mockSmtpClient.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(3);
                await Task.Delay(10000, ct);
            });

        cts.CancelAfter(TimeSpan.FromMilliseconds(10));

        var result = await _handler.SendStatusChangeEventAsync(channel, context, cts.Token);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Request cancelled", result.ErrorMessage);
    }

    private static NotificationChannel CreateChannel(
        string smtpHost,
        int smtpPort,
        string fromAddress,
        string[] recipients,
        bool useSsl = false,
        string? username = null,
        string? password = null)
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.Email.SmtpHost] = JsonSerializer.SerializeToElement(smtpHost),
            [ConfigurationKeys.Email.SmtpPort] = JsonSerializer.SerializeToElement(smtpPort),
            [ConfigurationKeys.Email.FromAddress] = JsonSerializer.SerializeToElement(fromAddress),
            [ConfigurationKeys.Email.Recipients] = JsonSerializer.SerializeToElement(recipients),
            [ConfigurationKeys.Email.UseSsl] = JsonSerializer.SerializeToElement(useSsl)
        };

        if (!string.IsNullOrWhiteSpace(username))
        {
            config[ConfigurationKeys.Email.Username] = JsonSerializer.SerializeToElement(username);
        }

        if (!string.IsNullOrWhiteSpace(password))
        {
            config[ConfigurationKeys.Email.Password] = JsonSerializer.SerializeToElement(password);
        }

        return new NotificationChannel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "Test Email Channel",
            ChannelType = ChannelTypes.Email,
            ConfigurationJson = config,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static StatusAlertContext CreateContext(
        string checkName = "Test Check",
        string status = CheckStatuses.Down,
        string? errorMessage = null,
        int? responseTimeMs = null,
        string workspaceName = "Test Workspace",
        bool isRecovery = false,
        int consecutiveFailures = 1)
    {
        return new StatusAlertContext
        {
            CheckName = checkName,
            CheckId = Guid.NewGuid(),
            Status = status,
            ErrorMessage = errorMessage,
            ResponseTimeMs = responseTimeMs,
            Timestamp = DateTimeOffset.UtcNow,
            WorkspaceName = workspaceName,
            IsRecovery = isRecovery,
            ConsecutiveFailures = consecutiveFailures
        };
    }

    private static LifecycleEventContext CreateLifecycleContext(
        string eventType = EventTypes.CheckCreated,
        string checkName = "Test Check",
        string checkType = CheckTypes.Http,
        string workspaceName = "Test Workspace",
        string performedBy = "test@example.com",
        Dictionary<string, object>? configurationChanges = null)
    {
        return new LifecycleEventContext
        {
            EventType = eventType,
            CheckId = Guid.NewGuid(),
            CheckName = checkName,
            CheckType = checkType,
            WorkspaceName = workspaceName,
            Timestamp = DateTimeOffset.UtcNow,
            PerformedBy = performedBy,
            ConfigurationChanges = configurationChanges
        };
    }

    private static StatusChangeEventContext CreateStatusChangeEventContext(
        string checkName = "Test Check",
        string workspaceName = "Test Workspace",
        string previousStatus = CheckStatuses.Up,
        string newStatus = CheckStatuses.Down,
        int? responseTimeMs = null,
        string? errorMessage = null)
    {
        return new StatusChangeEventContext
        {
            CheckId = Guid.NewGuid(),
            CheckName = checkName,
            WorkspaceName = workspaceName,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ResponseTimeMs = responseTimeMs,
            ErrorMessage = errorMessage,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
