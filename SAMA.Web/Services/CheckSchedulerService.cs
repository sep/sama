using Quartz;
using SAMA.Web.Extensions;

namespace SAMA.Web.Services;

public class CheckSchedulerService(ISchedulerFactory _schedulerFactory, GlobalSettingsService _globalSettings, ILogger<CheckSchedulerService> _logger)
{
    public virtual async Task ScheduleCheckAsync(Guid checkId, string schedule, CancellationToken cancellationToken = default)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

        var jobKey = new JobKey($"check-{checkId:N}", "checks");
        var triggerKey = new TriggerKey($"check-{checkId:N}-trigger", "checks");

        await scheduler.DeleteJob(jobKey, cancellationToken);

        var job = JobBuilder.Create<CheckExecutorJob>()
            .WithIdentity(jobKey)
            .UsingJobData("CheckId", checkId)
            .Build();

        var triggerBuilder = TriggerBuilder.Create()
            .WithIdentity(triggerKey);

        if (int.TryParse(schedule, out var intervalSeconds))
        {
            triggerBuilder
                .StartAt(DateTimeOffset.UtcNow.AddSeconds(5))
                .WithSimpleSchedule(x => x
                    .WithIntervalInSeconds(intervalSeconds)
                    .RepeatForever());
        }
        else
        {
            var timeZone = TimeZoneExtensions.FindTimeZoneByIanaId(_globalSettings.TimeZone);
            triggerBuilder.WithCronSchedule(schedule, x => x.InTimeZone(timeZone));
        }

        var trigger = triggerBuilder.Build();

        await scheduler.ScheduleJob(job, trigger, cancellationToken);

        _logger.LogInformation("Scheduled check {CheckId} with schedule {Schedule}", checkId, schedule);
    }

    public virtual async Task UnscheduleCheckAsync(Guid checkId, CancellationToken cancellationToken = default)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = new JobKey($"check-{checkId:N}", "checks");

        await scheduler.DeleteJob(jobKey, cancellationToken);

        _logger.LogInformation("Unscheduled check {CheckId}", checkId);
    }

    public virtual async Task TriggerImmediateCheckAsync(Guid checkId, CancellationToken cancellationToken = default)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = new JobKey($"check-{checkId:N}", "checks");

        var jobExists = await scheduler.CheckExists(jobKey, cancellationToken);

        if (!jobExists)
        {
            _logger.LogWarning("Cannot trigger immediate check for {CheckId} - job not scheduled", checkId);
            return;
        }

        await scheduler.TriggerJob(jobKey, cancellationToken);
        _logger.LogInformation("Triggered immediate execution for check {CheckId}", checkId);
    }
}
