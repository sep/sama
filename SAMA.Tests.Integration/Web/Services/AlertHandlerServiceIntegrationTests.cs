using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Shared.Constants;
using SAMA.Shared.Models;
using SAMA.Web.Models;
using SAMA.Web.Services;
using SAMA.Web.Services.NotificationChannels;

namespace SAMA.Tests.Integration.Web.Services;

[TestClass]
public class AlertHandlerServiceIntegrationTests : IntegrationTestBase
{
    private static INotificationChannelHandler _sharedMockHandler = null!;

    private AlertHandlerService _service = null!;
    private EventSubscriptionService _mockEventSubscriptionService = null!;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _sharedMockHandler = Substitute.For<INotificationChannelHandler>();
        services.AddKeyedSingleton("TestChannel1", _sharedMockHandler);
        services.AddKeyedSingleton("TestChannel2", _sharedMockHandler);
    }

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();

        _mockEventSubscriptionService = Substitute.For<EventSubscriptionService>(null, null, null);

        var logger = Substitute.For<ILogger<AlertHandlerService>>();
        _service = new AlertHandlerService(DbContext, ServiceProvider, _mockEventSubscriptionService, logger);
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldNotSendAlertWhenNoAlertsConfigured()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));
        var result = CreateCheckExecutionResult(CheckStatuses.Down);

        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.DidNotReceive().SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldNotSendAlertWhenCheckNotFound()
    {
        var nonExistentCheckId = Guid.NewGuid();
        var result = CreateCheckExecutionResult(CheckStatuses.Down);

        await _service.ProcessCheckResultAsync(nonExistentCheckId, result);

        await _sharedMockHandler.DidNotReceive().SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldNotSendAlertWhenAlertDisabled()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id);
        var alert = await CreateAlertAsync(check.Id, enabled: false);
        await AddChannelToAlertAsync(alert.Id, channel.Id);

        DbContext.ChangeTracker.Clear();

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));
        var result = CreateCheckExecutionResult(CheckStatuses.Down);

        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.DidNotReceive().SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldSendAlertOnFirstDownStatus()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id);
        var alert = await CreateAlertAsync(check.Id, triggerOnDown: true, failureThreshold: 1);
        await AddChannelToAlertAsync(alert.Id, channel.Id);

        await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Up, success: true);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel { Success = true, SentAt = DateTimeOffset.UtcNow });

        var result = CreateCheckExecutionResult(CheckStatuses.Down, "Connection refused");

        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.Received(1).SendStatusAlertAsync(
            Arg.Is<NotificationChannel>(nc => nc.Id == channel.Id),
            Arg.Is<StatusAlertContext>(ctx =>
                ctx.CheckId == check.Id &&
                ctx.Status == CheckStatuses.Down &&
                ctx.ConsecutiveFailures == 1 &&
                !ctx.IsRecovery),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldRespectFailureThreshold()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id);
        var alert = await CreateAlertAsync(check.Id, triggerOnDown: true, failureThreshold: 3);
        await AddChannelToAlertAsync(alert.Id, channel.Id);

        await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Up, success: true);

        DbContext.ChangeTracker.Clear();

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel { Success = true, SentAt = DateTimeOffset.UtcNow });

        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-3));
        await _service.ProcessCheckResultAsync(check.Id, result);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-2));
        await _service.ProcessCheckResultAsync(check.Id, result);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.Received(1).SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Is<StatusAlertContext>(ctx => ctx.ConsecutiveFailures == 3),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldNotSendAlertWhenBelowThreshold()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id);
        var alert = await CreateAlertAsync(check.Id, triggerOnDown: true, failureThreshold: 3);
        await AddChannelToAlertAsync(alert.Id, channel.Id);

        await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Up, success: true);

        DbContext.ChangeTracker.Clear();

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-2));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));

        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.DidNotReceive().SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldResetConsecutiveFailuresOnUpStatus()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id);
        var alert = await CreateAlertAsync(check.Id, triggerOnDown: true, failureThreshold: 3);
        await AddChannelToAlertAsync(alert.Id, channel.Id);

        await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Up, success: true);

        DbContext.ChangeTracker.Clear();

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-4));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-3));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, DateTimeOffset.UtcNow.AddMinutes(-2));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));

        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.DidNotReceive().SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldSendRecoveryNotification()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id);
        var alert = await CreateAlertAsync(check.Id, sendRecoveryNotification: true);
        await AddChannelToAlertAsync(alert.Id, channel.Id);

        await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Down, success: true);

        DbContext.ChangeTracker.Clear();

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel { Success = true, SentAt = DateTimeOffset.UtcNow });

        var result = CreateCheckExecutionResult(CheckStatuses.Up);
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.Received(1).SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Is<StatusAlertContext>(ctx =>
                ctx.Status == CheckStatuses.Up &&
                ctx.IsRecovery),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldNotSendRecoveryWhenDisabled()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id);
        var alert = await CreateAlertAsync(check.Id, sendRecoveryNotification: false);
        await AddChannelToAlertAsync(alert.Id, channel.Id);

        await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Down, success: true);

        DbContext.ChangeTracker.Clear();

        var result = CreateCheckExecutionResult(CheckStatuses.Up);
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.DidNotReceive().SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldSendToMultipleChannels()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel1 = await CreateNotificationChannelAsync(workspace.Id, "Channel 1");
        var channel2 = await CreateNotificationChannelAsync(workspace.Id, "Channel 2");
        var alert = await CreateAlertAsync(check.Id);
        await AddChannelToAlertAsync(alert.Id, channel1.Id);
        await AddChannelToAlertAsync(alert.Id, channel2.Id);

        await CreateAlertHistoryAsync(alert.Id, channel1.Id, CheckStatuses.Up, success: true);
        await CreateAlertHistoryAsync(alert.Id, channel2.Id, CheckStatuses.Up, success: true);

        DbContext.ChangeTracker.Clear();

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel { Success = true, SentAt = DateTimeOffset.UtcNow });

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));
        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.Received(2).SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldNotTriggerOnWarnWhenDisabled()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id);
        var alert = await CreateAlertAsync(check.Id, triggerOnWarn: false);
        await AddChannelToAlertAsync(alert.Id, channel.Id);

        await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Up, success: true);

        DbContext.ChangeTracker.Clear();

        var result = CreateCheckExecutionResult(CheckStatuses.Warn);
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.DidNotReceive().SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldNotTriggerOnDownWhenDisabled()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id);
        var alert = await CreateAlertAsync(check.Id, triggerOnDown: false);
        await AddChannelToAlertAsync(alert.Id, channel.Id);

        await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Up, success: true);

        DbContext.ChangeTracker.Clear();

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));
        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.DidNotReceive().SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldTriggerOnWarnWhenEnabled()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id);
        var alert = await CreateAlertAsync(check.Id, triggerOnWarn: true, failureThreshold: 1);
        await AddChannelToAlertAsync(alert.Id, channel.Id);

        await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Up, success: true);

        DbContext.ChangeTracker.Clear();

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel { Success = true, SentAt = DateTimeOffset.UtcNow });

        await CreateCheckResultAsync(check.Id, CheckStatuses.Warn, DateTimeOffset.UtcNow.AddMinutes(-1));
        var result = CreateCheckExecutionResult(CheckStatuses.Warn);
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.Received(1).SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Is<StatusAlertContext>(ctx => ctx.Status == CheckStatuses.Warn),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldRecordSuccessfulAlertInHistory()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id);
        var alert = await CreateAlertAsync(check.Id);
        await AddChannelToAlertAsync(alert.Id, channel.Id);

        await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Up, success: true);

        DbContext.ChangeTracker.Clear();

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel { Success = true, SentAt = DateTimeOffset.UtcNow });

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));
        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        var history = DbContext.AlertHistories
            .Where(h => h.AlertId == alert.Id && h.NotificationChannelId == channel.Id)
            .ToList();

        Assert.HasCount(2, history);
        var latestHistory = history.OrderByDescending(h => h.SentAt).First();
        Assert.IsTrue(latestHistory.Success);
        Assert.AreEqual(CheckStatuses.Down, latestHistory.Status);
        Assert.IsNull(latestHistory.ErrorMessage);
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldRecordFailedAlertInHistory()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id);
        var alert = await CreateAlertAsync(check.Id);
        await AddChannelToAlertAsync(alert.Id, channel.Id);

        await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Up, success: true);

        DbContext.ChangeTracker.Clear();

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel
            {
                Success = false,
                ErrorMessage = "SMTP connection failed",
                SentAt = DateTimeOffset.UtcNow
            });

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));
        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        var history = DbContext.AlertHistories
            .Where(h => h.AlertId == alert.Id)
            .ToList();

        Assert.HasCount(2, history);
        var latestHistory = history.OrderByDescending(h => h.SentAt).First();
        Assert.IsFalse(latestHistory.Success);
        Assert.AreEqual("SMTP connection failed", latestHistory.ErrorMessage);
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldNotSendDuplicateAlertForSameStatus()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id);
        var alert = await CreateAlertAsync(check.Id, failureThreshold: 1);
        await AddChannelToAlertAsync(alert.Id, channel.Id);

        await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Down, success: true);

        DbContext.ChangeTracker.Clear();

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel { Success = true, SentAt = DateTimeOffset.UtcNow });

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));
        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.DidNotReceive().SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldSendAlertAfterConfigurationUpdate()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id);
        var alert = await CreateAlertAsync(check.Id);
        await AddChannelToAlertAsync(alert.Id, channel.Id);

        await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Down, success: true, sentAt: DateTimeOffset.UtcNow.AddHours(-2));

        alert.UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1);
        await DbContext.SaveChangesAsync();

        DbContext.ChangeTracker.Clear();

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel { Success = true, SentAt = DateTimeOffset.UtcNow });

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));
        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.Received(1).SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldSkipDisabledChannels()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var disabledChannel = await CreateNotificationChannelAsync(workspace.Id, "Disabled", enabled: false);
        var enabledChannel = await CreateNotificationChannelAsync(workspace.Id, "Enabled", enabled: true);
        var alert = await CreateAlertAsync(check.Id);
        await AddChannelToAlertAsync(alert.Id, disabledChannel.Id);
        await AddChannelToAlertAsync(alert.Id, enabledChannel.Id);

        await CreateAlertHistoryAsync(alert.Id, disabledChannel.Id, CheckStatuses.Up, success: true);
        await CreateAlertHistoryAsync(alert.Id, enabledChannel.Id, CheckStatuses.Up, success: true);

        DbContext.ChangeTracker.Clear();

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel { Success = true, SentAt = DateTimeOffset.UtcNow });

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));
        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.Received(1).SendStatusAlertAsync(
            Arg.Is<NotificationChannel>(nc => nc.Id == enabledChannel.Id),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());

        await _sharedMockHandler.DidNotReceive().SendStatusAlertAsync(
            Arg.Is<NotificationChannel>(nc => nc.Id == disabledChannel.Id),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldHandleUnknownChannelType()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id, channelType: "UnknownType");
        var alert = await CreateAlertAsync(check.Id);
        await AddChannelToAlertAsync(alert.Id, channel.Id);

        await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Up, success: true);

        DbContext.ChangeTracker.Clear();

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));
        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        var history = DbContext.AlertHistories
            .Where(h => h.AlertId == alert.Id)
            .ToList();

        Assert.HasCount(2, history);
        var latestHistory = history.OrderByDescending(h => h.SentAt).First();
        Assert.IsFalse(latestHistory.Success);
        Assert.Contains("No handler found", latestHistory.ErrorMessage ?? "");
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldSendToAllWorkspaceChannelsWhenAlertHasNoChannels()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel1 = await CreateNotificationChannelAsync(workspace.Id, "Channel 1", channelType: "TestChannel1");
        var channel2 = await CreateNotificationChannelAsync(workspace.Id, "Channel 2", channelType: "TestChannel2");
        var channel3 = await CreateNotificationChannelAsync(workspace.Id, "Channel 3", channelType: "TestChannel1");
        var alert = await CreateAlertAsync(check.Id);

        DbContext.ChangeTracker.Clear();

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel { Success = true, SentAt = DateTimeOffset.UtcNow });

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));
        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.Received(3).SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldSkipDisabledChannelsInAllChannelsMode()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var enabledChannel1 = await CreateNotificationChannelAsync(workspace.Id, "Enabled 1", enabled: true, channelType: "TestChannel1");
        var disabledChannel = await CreateNotificationChannelAsync(workspace.Id, "Disabled", enabled: false, channelType: "TestChannel2");
        var enabledChannel2 = await CreateNotificationChannelAsync(workspace.Id, "Enabled 2", enabled: true, channelType: "TestChannel1");
        var alert = await CreateAlertAsync(check.Id);

        DbContext.ChangeTracker.Clear();

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel { Success = true, SentAt = DateTimeOffset.UtcNow });

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));
        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.Received(2).SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());

        await _sharedMockHandler.DidNotReceive().SendStatusAlertAsync(
            Arg.Is<NotificationChannel>(nc => nc.Id == disabledChannel.Id),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldSendRecoveryToAllChannelsWhenNoChannelsConfigured()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel1 = await CreateNotificationChannelAsync(workspace.Id, "Channel 1", channelType: "TestChannel1");
        var channel2 = await CreateNotificationChannelAsync(workspace.Id, "Channel 2", channelType: "TestChannel2");
        var alert = await CreateAlertAsync(check.Id, sendRecoveryNotification: true);

        await CreateAlertHistoryAsync(alert.Id, channel1.Id, CheckStatuses.Down, success: true);

        DbContext.ChangeTracker.Clear();

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel { Success = true, SentAt = DateTimeOffset.UtcNow });

        var result = CreateCheckExecutionResult(CheckStatuses.Up);
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.Received(2).SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Is<StatusAlertContext>(ctx =>
                ctx.Status == CheckStatuses.Up &&
                ctx.IsRecovery),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldRespectThresholdWithAllChannelsMode()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel1 = await CreateNotificationChannelAsync(workspace.Id, "Channel 1", channelType: "TestChannel1");
        var channel2 = await CreateNotificationChannelAsync(workspace.Id, "Channel 2", channelType: "TestChannel2");
        var alert = await CreateAlertAsync(check.Id, triggerOnDown: true, failureThreshold: 2);

        await CreateAlertHistoryAsync(alert.Id, channel1.Id, CheckStatuses.Up, success: true);
        await CreateAlertHistoryAsync(alert.Id, channel2.Id, CheckStatuses.Up, success: true);

        DbContext.ChangeTracker.Clear();

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel { Success = true, SentAt = DateTimeOffset.UtcNow });

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-2));
        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.DidNotReceive().SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.Received(1).SendStatusAlertAsync(
            Arg.Is<NotificationChannel>(ch => ch.Name == "Channel 1"),
            Arg.Is<StatusAlertContext>(ctx => ctx.ConsecutiveFailures == 2),
            Arg.Any<CancellationToken>());
        await _sharedMockHandler.Received(1).SendStatusAlertAsync(
            Arg.Is<NotificationChannel>(ch => ch.Name == "Channel 2"),
            Arg.Is<StatusAlertContext>(ctx => ctx.ConsecutiveFailures == 2),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldRecordHistoryForAllChannelsWhenNoChannelsConfigured()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel1 = await CreateNotificationChannelAsync(workspace.Id, "Channel 1", channelType: "TestChannel1");
        var channel2 = await CreateNotificationChannelAsync(workspace.Id, "Channel 2", channelType: "TestChannel2");
        var alert = await CreateAlertAsync(check.Id);

        DbContext.ChangeTracker.Clear();

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel { Success = true, SentAt = DateTimeOffset.UtcNow });

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));
        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        var histories = DbContext.AlertHistories
            .Where(h => h.AlertId == alert.Id)
            .ToList();

        Assert.HasCount(2, histories);
        Assert.IsTrue(histories.All(h => h.Success));
        Assert.IsTrue(histories.All(h => h.Status == CheckStatuses.Down));
        Assert.IsTrue(histories.Any(h => h.NotificationChannelId == channel1.Id));
        Assert.IsTrue(histories.Any(h => h.NotificationChannelId == channel2.Id));
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldSendInitialNotificationToAllChannelsWhenNoChannelsConfigured()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel1 = await CreateNotificationChannelAsync(workspace.Id, "Channel 1", channelType: "TestChannel1");
        var channel2 = await CreateNotificationChannelAsync(workspace.Id, "Channel 2", channelType: "TestChannel2");
        var alert = await CreateAlertAsync(check.Id, failureThreshold: 1);

        DbContext.ChangeTracker.Clear();

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel { Success = true, SentAt = DateTimeOffset.UtcNow });

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));
        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.Received(2).SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldOnlyUseEnabledChannelsFromCorrectWorkspace()
    {
        var workspace1 = await CreateWorkspaceAsync();
        var workspace2 = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace1.Id);
        var channel1Workspace1 = await CreateNotificationChannelAsync(workspace1.Id, "WS1 Channel", channelType: "TestChannel1");
        var channel2Workspace2 = await CreateNotificationChannelAsync(workspace2.Id, "WS2 Channel", channelType: "TestChannel2");
        var alert = await CreateAlertAsync(check.Id);

        DbContext.ChangeTracker.Clear();

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel { Success = true, SentAt = DateTimeOffset.UtcNow });

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));
        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.Received(1).SendStatusAlertAsync(
            Arg.Is<NotificationChannel>(nc => nc.Id == channel1Workspace1.Id),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());

        await _sharedMockHandler.DidNotReceive().SendStatusAlertAsync(
            Arg.Is<NotificationChannel>(nc => nc.Id == channel2Workspace2.Id),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldUseSameTriggerEventIdForAllChannels()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel1 = await CreateNotificationChannelAsync(workspace.Id, "Channel 1", channelType: "TestChannel1");
        var channel2 = await CreateNotificationChannelAsync(workspace.Id, "Channel 2", channelType: "TestChannel2");
        var channel3 = await CreateNotificationChannelAsync(workspace.Id, "Channel 3", channelType: "TestChannel1");
        var alert = await CreateAlertAsync(check.Id);
        await AddChannelToAlertAsync(alert.Id, channel1.Id);
        await AddChannelToAlertAsync(alert.Id, channel2.Id);
        await AddChannelToAlertAsync(alert.Id, channel3.Id);

        DbContext.ChangeTracker.Clear();

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel { Success = true, SentAt = DateTimeOffset.UtcNow });

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));
        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        var histories = DbContext.AlertHistories
            .Where(h => h.AlertId == alert.Id)
            .ToList();

        Assert.HasCount(3, histories);

        var triggerEventIds = histories.Select(h => h.TriggerEventId).Distinct().ToList();
        Assert.HasCount(1, triggerEventIds, "All notifications from the same event should share the same TriggerEventId");

        Assert.AreNotEqual(Guid.Empty, triggerEventIds[0]);
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldRecordHistoryWithTwentyPlusWhenConsecutiveFailuresIs20()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id, channelType: "TestChannel1");
        var alert = await CreateAlertAsync(check.Id, failureThreshold: 1);
        await AddChannelToAlertAsync(alert.Id, channel.Id);

        DbContext.ChangeTracker.Clear();

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel { Success = true, SentAt = DateTimeOffset.UtcNow });

        for (int i = 20; i > 0; i--)
        {
            await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-i));
        }

        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        var histories = DbContext.AlertHistories
            .Where(h => h.AlertId == alert.Id)
            .ToList();

        Assert.HasCount(1, histories);
        Assert.Contains("20+", histories[0].Message);
        Assert.DoesNotContain("20 consecutive", histories[0].Message);
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldRecordHistoryWithExactCountWhenConsecutiveFailuresIsBelow20()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id, channelType: "TestChannel1");
        var alert = await CreateAlertAsync(check.Id, failureThreshold: 1);
        await AddChannelToAlertAsync(alert.Id, channel.Id);

        DbContext.ChangeTracker.Clear();

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel { Success = true, SentAt = DateTimeOffset.UtcNow });

        for (int i = 15; i > 0; i--)
        {
            await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-i));
        }

        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        var histories = DbContext.AlertHistories
            .Where(h => h.AlertId == alert.Id)
            .ToList();

        Assert.HasCount(1, histories);
        Assert.Contains("15 consecutive", histories[0].Message);
        Assert.DoesNotContain("20+", histories[0].Message);
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldNotTriggerDownOnlyAlertForWarnStatusOnFirstRun()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id, channelType: "TestChannel1");
        var alert = await CreateAlertAsync(check.Id, triggerOnWarn: false, triggerOnDown: true, failureThreshold: 1);
        await AddChannelToAlertAsync(alert.Id, channel.Id);

        DbContext.ChangeTracker.Clear();

        _sharedMockHandler.SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>())
            .Returns(new NotificationResultModel { Success = true, SentAt = DateTimeOffset.UtcNow });

        await CreateCheckResultAsync(check.Id, CheckStatuses.Warn, DateTimeOffset.UtcNow.AddMinutes(-1));
        var result = CreateCheckExecutionResult(CheckStatuses.Warn, "Certificate expires in 15 days");
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _sharedMockHandler.DidNotReceive().SendStatusAlertAsync(
            Arg.Any<NotificationChannel>(),
            Arg.Any<StatusAlertContext>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldTriggerStatusChangeEventWhenStatusChanges()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);

        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, DateTimeOffset.UtcNow.AddMinutes(-1));

        var result = CreateCheckExecutionResult(CheckStatuses.Down, "Connection refused");
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _mockEventSubscriptionService.Received(1).TriggerStatusChangeEventAsync(
            workspace.Id,
            Arg.Is<StatusChangeEventContext>(ctx =>
                ctx.CheckId == check.Id &&
                ctx.CheckName == "Test Check" &&
                ctx.PreviousStatus == CheckStatuses.Up &&
                ctx.NewStatus == CheckStatuses.Down &&
                ctx.WorkspaceName == workspace.Name),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldNotTriggerStatusChangeEventWhenNoPreviousResult()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);

        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _mockEventSubscriptionService.DidNotReceive().TriggerStatusChangeEventAsync(
            Arg.Any<Guid>(),
            Arg.Any<StatusChangeEventContext>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessCheckResultAsyncShouldNotTriggerStatusChangeEventWhenStatusUnchanged()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, DateTimeOffset.UtcNow.AddMinutes(-1));

        var result = CreateCheckExecutionResult(CheckStatuses.Down);
        await _service.ProcessCheckResultAsync(check.Id, result);

        await _mockEventSubscriptionService.DidNotReceive().TriggerStatusChangeEventAsync(
            Arg.Any<Guid>(),
            Arg.Any<StatusChangeEventContext>(),
            Arg.Any<CancellationToken>());
    }

    private static CheckExecutionResult CreateCheckExecutionResult(
        string status,
        string? errorMessage = null,
        int? responseTimeMs = null)
    {
        return new CheckExecutionResult
        {
            Status = status,
            ErrorMessage = errorMessage,
            ResponseTimeMs = responseTimeMs,
            CheckedAt = DateTimeOffset.UtcNow
        };
    }

    private async Task<Workspace> CreateWorkspaceAsync()
    {
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "Test Workspace",
            IsPublic = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.Workspaces.Add(workspace);
        await DbContext.SaveChangesAsync();
        return workspace;
    }

    private async Task<Check> CreateCheckAsync(Guid workspaceId)
    {
        var check = new Check
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = "Test Check",
            CheckType = "TestCheckType",
            ConfigurationJson = new Dictionary<string, JsonElement>(),
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.Checks.Add(check);
        await DbContext.SaveChangesAsync();
        return check;
    }

    private async Task<NotificationChannel> CreateNotificationChannelAsync(
        Guid workspaceId,
        string name = "Test Channel",
        bool enabled = true,
        string channelType = "TestChannel1")
    {
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name,
            ChannelType = channelType,
            ConfigurationJson = new Dictionary<string, JsonElement>(),
            Enabled = enabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.NotificationChannels.Add(channel);
        await DbContext.SaveChangesAsync();
        return channel;
    }

    private async Task<Alert> CreateAlertAsync(
        Guid checkId,
        bool enabled = true,
        bool triggerOnWarn = false,
        bool triggerOnDown = true,
        int failureThreshold = 1,
        bool sendRecoveryNotification = true)
    {
        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            CheckId = checkId,
            Name = "Test Alert",
            TriggerOnWarn = triggerOnWarn,
            TriggerOnDown = triggerOnDown,
            FailureThreshold = failureThreshold,
            SendRecoveryNotification = sendRecoveryNotification,
            Enabled = enabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.Alerts.Add(alert);
        await DbContext.SaveChangesAsync();
        return alert;
    }

    private async Task AddChannelToAlertAsync(Guid alertId, Guid channelId)
    {
        var alert = await DbContext.Alerts.FindAsync(alertId);
        Assert.IsNotNull(alert, "Alert not found when adding channel.");
        var channel = await DbContext.NotificationChannels.FindAsync(channelId);
        Assert.IsNotNull(channel, "Notification channel not found when adding to alert.");

        alert.NotificationChannels.Add(channel);
        await DbContext.SaveChangesAsync();
    }

    private async Task CreateCheckResultAsync(
        Guid checkId,
        string status,
        DateTimeOffset checkedAt,
        string? errorMessage = null)
    {
        var checkResult = new CheckResult
        {
            Id = Guid.NewGuid(),
            CheckId = checkId,
            Status = status,
            ErrorMessage = errorMessage,
            CheckedAt = checkedAt
        };

        DbContext.CheckResults.Add(checkResult);
        await DbContext.SaveChangesAsync();
    }

    private async Task CreateAlertHistoryAsync(
        Guid alertId,
        Guid channelId,
        string status,
        bool success,
        DateTimeOffset? sentAt = null)
    {
        var history = new AlertHistory
        {
            Id = Guid.NewGuid(),
            AlertId = alertId,
            NotificationChannelId = channelId,
            TriggerEventId = Guid.CreateVersion7(),
            Status = status,
            Message = "Test message",
            Success = success,
            SentAt = sentAt ?? DateTimeOffset.UtcNow
        };

        DbContext.AlertHistories.Add(history);
        await DbContext.SaveChangesAsync();
    }
}
