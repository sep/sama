using Microsoft.Extensions.Logging;
using NSubstitute;
using Quartz;
using SAMA.Web.Services;

namespace SAMA.Tests.Unit.Web.Services;

[TestClass]
public class CheckSchedulerServiceTests
{
    private ISchedulerFactory _mockSchedulerFactory = null!;
    private IScheduler _mockScheduler = null!;
    private GlobalSettingsService _mockGlobalSettings = null!;
    private ILogger<CheckSchedulerService> _mockLogger = null!;
    private CheckSchedulerService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockSchedulerFactory = Substitute.For<ISchedulerFactory>();
        _mockScheduler = Substitute.For<IScheduler>();
        _mockGlobalSettings = Substitute.For<GlobalSettingsService>(null!, null!);
        _mockLogger = Substitute.For<ILogger<CheckSchedulerService>>();

        _mockGlobalSettings.TimeZone.Returns("UTC");

        _mockSchedulerFactory.GetScheduler(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(_mockScheduler));

        _service = new CheckSchedulerService(_mockSchedulerFactory, _mockGlobalSettings, _mockLogger);
    }

    [TestMethod]
    public async Task ScheduleCheckAsyncShouldScheduleJobWithCorrectJobKey()
    {
        var checkId = Guid.NewGuid();
        var schedule = "60";

        await _service.ScheduleCheckAsync(checkId, schedule);

        await _mockScheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j => j.Key.Name == $"check-{checkId:N}" && j.Key.Group == "checks"),
            Arg.Any<ITrigger>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ScheduleCheckAsyncShouldScheduleJobWithCorrectTriggerKey()
    {
        var checkId = Guid.NewGuid();
        var schedule = "60";

        await _service.ScheduleCheckAsync(checkId, schedule);

        await _mockScheduler.Received(1).ScheduleJob(
            Arg.Any<IJobDetail>(),
            Arg.Is<ITrigger>(t => t.Key.Name == $"check-{checkId:N}-trigger" && t.Key.Group == "checks"),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ScheduleCheckAsyncShouldScheduleJobWithCorrectIntervalRepeatingForever()
    {
        var checkId = Guid.NewGuid();
        var schedule = "120";

        await _service.ScheduleCheckAsync(checkId, schedule);

        await _mockScheduler.Received(1).ScheduleJob(
            Arg.Any<IJobDetail>(),
            Arg.Is<ITrigger>(t =>
                t is ISimpleTrigger && ((ISimpleTrigger)t).RepeatInterval == TimeSpan.FromSeconds(int.Parse(schedule)) && ((ISimpleTrigger)t).RepeatCount == -1),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ScheduleCheckAsyncShouldIncludeCheckIdInJobData()
    {
        var checkId = Guid.NewGuid();
        var schedule = "60";

        await _service.ScheduleCheckAsync(checkId, schedule);

        await _mockScheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j => j.JobDataMap.GetGuid("CheckId") == checkId),
            Arg.Any<ITrigger>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ScheduleCheckAsyncShouldDeleteExistingJobBeforeScheduling()
    {
        var checkId = Guid.NewGuid();
        var schedule = "60";

        await _service.ScheduleCheckAsync(checkId, schedule);

        Received.InOrder(() =>
        {
            _mockScheduler.DeleteJob(Arg.Is<JobKey>(k => k.Name == $"check-{checkId:N}" && k.Group == "checks"), Arg.Any<CancellationToken>());
            _mockScheduler.ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
        });
    }

    [TestMethod]
    public async Task ScheduleCheckAsyncShouldUseCancellationToken()
    {
        var checkId = Guid.NewGuid();
        var schedule = "60";
        using var cts = new CancellationTokenSource();

        await _service.ScheduleCheckAsync(checkId, schedule, cts.Token);

        await _mockSchedulerFactory.Received(1).GetScheduler(cts.Token);
        await _mockScheduler.Received(1).DeleteJob(Arg.Any<JobKey>(), cts.Token);
        await _mockScheduler.Received(1).ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), cts.Token);
    }

    [TestMethod]
    public async Task UnscheduleCheckAsyncShouldDeleteJob()
    {
        var checkId = Guid.NewGuid();

        await _service.UnscheduleCheckAsync(checkId);

        await _mockScheduler.Received(1).DeleteJob(
            Arg.Is<JobKey>(k => k.Name == $"check-{checkId:N}" && k.Group == "checks"),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task UnscheduleCheckAsyncShouldLogUnscheduledMessage()
    {
        var checkId = Guid.NewGuid();

        await _service.UnscheduleCheckAsync(checkId);

        _mockLogger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains(checkId.ToString())),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [TestMethod]
    public async Task UnscheduleCheckAsyncShouldUseCancellationToken()
    {
        var checkId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        await _service.UnscheduleCheckAsync(checkId, cts.Token);

        await _mockSchedulerFactory.Received(1).GetScheduler(cts.Token);
        await _mockScheduler.Received(1).DeleteJob(Arg.Any<JobKey>(), cts.Token);
    }

    [TestMethod]
    public async Task TriggerImmediateCheckAsyncShouldTriggerJobWhenJobExists()
    {
        var checkId = Guid.NewGuid();
        _mockScheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        await _service.TriggerImmediateCheckAsync(checkId);

        await _mockScheduler.Received(1).TriggerJob(
            Arg.Is<JobKey>(k => k.Name == $"check-{checkId:N}" && k.Group == "checks"),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task TriggerImmediateCheckAsyncShouldNotTriggerJobWhenJobDoesNotExist()
    {
        var checkId = Guid.NewGuid();
        _mockScheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        await _service.TriggerImmediateCheckAsync(checkId);

        await _mockScheduler.DidNotReceive().TriggerJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task TriggerImmediateCheckAsyncShouldUseCancellationToken()
    {
        var checkId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        _mockScheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        await _service.TriggerImmediateCheckAsync(checkId, cts.Token);

        await _mockSchedulerFactory.Received(1).GetScheduler(cts.Token);
        await _mockScheduler.Received(1).CheckExists(Arg.Any<JobKey>(), cts.Token);
        await _mockScheduler.Received(1).TriggerJob(Arg.Any<JobKey>(), cts.Token);
    }

    [TestMethod]
    public async Task ScheduleCheckAsyncShouldHandleMultipleDifferentCheckIds()
    {
        var checkIds = Enumerable.Range(1, 5).Select(_ => Guid.NewGuid()).ToArray();
        var schedule = "60";

        foreach (var checkId in checkIds)
        {
            await _service.ScheduleCheckAsync(checkId, schedule);

            await _mockScheduler.Received().ScheduleJob(
                Arg.Is<IJobDetail>(j => j.Key.Name == $"check-{checkId:N}"),
                Arg.Any<ITrigger>(),
                Arg.Any<CancellationToken>());
        }
    }

    [TestMethod]
    public async Task UnscheduleCheckAsyncShouldHandleMultipleDifferentCheckIds()
    {
        var checkIds = Enumerable.Range(1, 5).Select(_ => Guid.NewGuid()).ToArray();

        foreach (var checkId in checkIds)
        {
            await _service.UnscheduleCheckAsync(checkId);

            await _mockScheduler.Received().DeleteJob(
                Arg.Is<JobKey>(k => k.Name == $"check-{checkId:N}"),
                Arg.Any<CancellationToken>());
        }
    }

    [TestMethod]
    public async Task ScheduleCheckAsyncShouldStartTriggerAfterFiveSeconds()
    {
        var checkId = Guid.NewGuid();
        var schedule = "60";
        var beforeCall = DateTimeOffset.UtcNow;

        await _service.ScheduleCheckAsync(checkId, schedule);

        var afterCall = DateTimeOffset.UtcNow;

        await _mockScheduler.Received(1).ScheduleJob(
            Arg.Any<IJobDetail>(),
            Arg.Is<ITrigger>(t =>
                t.StartTimeUtc >= beforeCall.AddSeconds(4) &&
                t.StartTimeUtc <= afterCall.AddSeconds(6)),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ScheduleCheckAsyncShouldUseCronTriggerForCronExpression()
    {
        var checkId = Guid.NewGuid();
        var schedule = "0 */5 * * * ?";

        await _service.ScheduleCheckAsync(checkId, schedule);

        await _mockScheduler.Received(1).ScheduleJob(
            Arg.Any<IJobDetail>(),
            Arg.Is<ITrigger>(t => t is ICronTrigger),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ScheduleCheckAsyncShouldUseSimpleTriggerForNumericSchedule()
    {
        var checkId = Guid.NewGuid();
        var schedule = "90";

        await _service.ScheduleCheckAsync(checkId, schedule);

        await _mockScheduler.Received(1).ScheduleJob(
            Arg.Any<IJobDetail>(),
            Arg.Is<ITrigger>(t => t is ISimpleTrigger),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ScheduleCheckAsyncShouldSetCorrectCronExpression()
    {
        var checkId = Guid.NewGuid();
        var schedule = "0 0 0 * * ?";

        await _service.ScheduleCheckAsync(checkId, schedule);

        await _mockScheduler.Received(1).ScheduleJob(
            Arg.Any<IJobDetail>(),
            Arg.Is<ITrigger>(t =>
                t is ICronTrigger && ((ICronTrigger)t).CronExpressionString == "0 0 0 * * ?"),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ScheduleCheckAsyncWithCronShouldIncludeCheckIdInJobData()
    {
        var checkId = Guid.NewGuid();
        var schedule = "0 */10 * * * ?";

        await _service.ScheduleCheckAsync(checkId, schedule);

        await _mockScheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j => j.JobDataMap.GetGuid("CheckId") == checkId),
            Arg.Any<ITrigger>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ScheduleCheckAsyncWithCronShouldDeleteExistingJobFirst()
    {
        var checkId = Guid.NewGuid();
        var schedule = "30 0 */2 * * ?";

        await _service.ScheduleCheckAsync(checkId, schedule);

        Received.InOrder(() =>
        {
            _mockScheduler.DeleteJob(Arg.Is<JobKey>(k => k.Name == $"check-{checkId:N}" && k.Group == "checks"), Arg.Any<CancellationToken>());
            _mockScheduler.ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
        });
    }

    [TestMethod]
    public async Task ScheduleCheckAsyncWithCronShouldUseCorrectJobAndTriggerKeys()
    {
        var checkId = Guid.NewGuid();
        var schedule = "0 0 8 ? * MON-FRI";

        await _service.ScheduleCheckAsync(checkId, schedule);

        await _mockScheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j => j.Key.Name == $"check-{checkId:N}" && j.Key.Group == "checks"),
            Arg.Is<ITrigger>(t => t.Key.Name == $"check-{checkId:N}-trigger" && t.Key.Group == "checks"),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ScheduleCheckAsyncWithCronShouldApplyConfiguredTimeZone()
    {
        var checkId = Guid.NewGuid();
        var schedule = "0 0 8 * * ?";
        _mockGlobalSettings.TimeZone.Returns("America/New_York");

        await _service.ScheduleCheckAsync(checkId, schedule);

        await _mockScheduler.Received(1).ScheduleJob(
            Arg.Any<IJobDetail>(),
            Arg.Is<ITrigger>(t =>
                t is ICronTrigger && ((ICronTrigger)t).TimeZone.Id == TimeZoneInfo.FindSystemTimeZoneById("America/New_York").Id),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ScheduleCheckAsyncWithCronShouldUseUtcByDefault()
    {
        var checkId = Guid.NewGuid();
        var schedule = "0 0 8 * * ?";
        _mockGlobalSettings.TimeZone.Returns("UTC");

        await _service.ScheduleCheckAsync(checkId, schedule);

        await _mockScheduler.Received(1).ScheduleJob(
            Arg.Any<IJobDetail>(),
            Arg.Is<ITrigger>(t =>
                t is ICronTrigger && ((ICronTrigger)t).TimeZone.Id == TimeZoneInfo.Utc.Id),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ScheduleCheckAsyncWithIntervalShouldNotBeAffectedByTimeZone()
    {
        var checkId = Guid.NewGuid();
        var schedule = "60";
        _mockGlobalSettings.TimeZone.Returns("America/New_York");

        await _service.ScheduleCheckAsync(checkId, schedule);

        await _mockScheduler.Received(1).ScheduleJob(
            Arg.Any<IJobDetail>(),
            Arg.Is<ITrigger>(t => t is ISimpleTrigger),
            Arg.Any<CancellationToken>());
    }
}
