using Microsoft.EntityFrameworkCore;
using SAMA.Data;
using SAMA.Shared.Constants;
using SAMA.Web.Extensions;
using SAMA.Web.Models;

namespace SAMA.Web.Services.Queries;

public class CheckQueryService(SamaDbContext _samaDbContext, ApplicationStateService _appStateService, GlobalSettingsService _globalSettings, SensitiveDataMaskingService _maskingService)
{
    private const int MaxHistoryHours = 168; // 7 days

    public virtual async Task<List<CheckListItemViewModel>> GetChecksForWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var startupTime = _appStateService.StartupTime;

        var checks = await _samaDbContext.Checks
            .AsSplitQuery()
            .Include(c => c.CheckResults)
            .Include(c => c.Alerts)
            .Where(c => c.WorkspaceId == workspaceId)
            .Select(c => new CheckListItemViewModel
            {
                Id = c.Id,
                Name = c.Name,
                CheckType = c.CheckType,
                Enabled = c.Enabled,
                Schedule = c.Schedule,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                LastStatus = c.CheckResults
                    .OrderByDescending(cr => cr.CheckedAt)
                    .Select(cr => cr.Status)
                    .FirstOrDefault(),
                LastCheckedAt = c.CheckResults
                    .OrderByDescending(cr => cr.CheckedAt)
                    .Select(cr => (DateTimeOffset?)cr.CheckedAt)
                    .FirstOrDefault(),
                LastResponseTimeMs = c.CheckResults
                    .OrderByDescending(cr => cr.CheckedAt)
                    .Select(cr => cr.ResponseTimeMs)
                    .FirstOrDefault(),
                LastErrorMessage = c.CheckResults
                    .OrderByDescending(cr => cr.CheckedAt)
                    .Select(cr => cr.ErrorMessage)
                    .FirstOrDefault(),
                AlertCount = c.Alerts.Count
            })
            .ToListAsync(cancellationToken);

        foreach (var check in checks)
        {
            if (!check.Enabled ||
                !check.LastCheckedAt.HasValue ||
                 check.LastCheckedAt.Value < startupTime ||
                 check.UpdatedAt > check.LastCheckedAt.Value)
            {
                check.LastStatus = null;
            }
        }

        return checks
            .OrderBy(c => c.LastStatus switch
            {
                CheckStatuses.Down => 0,
                CheckStatuses.Warn => 1,
                CheckStatuses.Up => 2,
                _ => 3 // null (pending) and disabled
            })
            .ThenBy(c => c.Name)
            .ToList();
    }

    public virtual async Task<CheckDetailsViewModel?> GetCheckDetailsAsync(
        Guid checkId,
        CancellationToken cancellationToken = default)
    {
        var startupTime = _appStateService.StartupTime;

        var check = await _samaDbContext.Checks
            .AsSplitQuery()
            .Include(c => c.Workspace)
            .Include(c => c.CheckResults.OrderByDescending(cr => cr.CheckedAt).Take(1))
            .Include(c => c.Alerts)
                .ThenInclude(a => a.NotificationChannels)
            .FirstOrDefaultAsync(c => c.Id == checkId, cancellationToken);

        if (check == null)
        {
            return null;
        }

        var lastResult = check.CheckResults.FirstOrDefault();
        var resultCount = await _samaDbContext.CheckResults.CountAsync(cr => cr.CheckId == check.Id, cancellationToken);

        var viewModel = new CheckDetailsViewModel
        {
            Id = check.Id,
            WorkspaceId = check.WorkspaceId,
            WorkspaceName = check.Workspace.Name,
            Name = check.Name,
            Description = check.Description,
            CheckType = check.CheckType,
            Schedule = check.Schedule,
            TimeoutSeconds = check.TimeoutSeconds,
            Enabled = check.Enabled,
            CreatedAt = check.CreatedAt,
            UpdatedAt = check.UpdatedAt,
            ResultCount = resultCount,
            AlertCount = check.Alerts.Count,
            LastStatus = lastResult?.Status,
            LastCheckedAt = lastResult?.CheckedAt,
            LastErrorMessage = lastResult?.ErrorMessage,
            MaskedConfiguration = _maskingService.MaskCheckConfig(check.CheckType, check.ConfigurationJson),
            Alerts = check.Alerts
                .OrderBy(a => a.Name)
                .Select(a => new CheckDetailsViewModel.AlertInfo
                {
                    Id = a.Id,
                    Name = a.Name,
                    TriggerOnWarn = a.TriggerOnWarn,
                    TriggerOnDown = a.TriggerOnDown,
                    FailureThreshold = a.FailureThreshold,
                    SendRecoveryNotification = a.SendRecoveryNotification,
                    Enabled = a.Enabled,
                    ChannelCount = a.NotificationChannels.Count
                })
                .ToList()
        };

        if (!check.Enabled || lastResult == null || lastResult.CheckedAt < startupTime || check.UpdatedAt > lastResult.CheckedAt)
        {
            viewModel.LastStatus = null;
        }

        return viewModel;
    }

    public virtual async Task<CheckEditViewModel?> GetCheckForEditAsync(
        Guid checkId,
        CancellationToken cancellationToken = default)
    {
        var check = await _samaDbContext.Checks
            .Include(c => c.Workspace)
            .FirstOrDefaultAsync(c => c.Id == checkId, cancellationToken);

        if (check == null)
        {
            return null;
        }

        return new CheckEditViewModel
        {
            Id = check.Id,
            WorkspaceId = check.WorkspaceId,
            WorkspaceName = check.Workspace.Name,
            Name = check.Name,
            Description = check.Description,
            CheckType = check.CheckType,
            Schedule = check.Schedule,
            TimeoutSeconds = check.TimeoutSeconds,
            Enabled = check.Enabled,
            ConfigurationJson = check.ConfigurationJson
        };
    }

    public virtual async Task<List<CheckHistoryItemViewModel>> GetCheckHistoryAsync(
        Guid checkId,
        int hours,
        CancellationToken cancellationToken = default)
    {
        hours = Math.Clamp(hours, 1, MaxHistoryHours);
        var cutoffTime = DateTimeOffset.UtcNow.AddHours(-hours);

        return await _samaDbContext.CheckResults
            .AsNoTracking()
            .Where(cr => cr.CheckId == checkId && cr.CheckedAt >= cutoffTime)
            .OrderBy(cr => cr.CheckedAt)
            .Select(cr => new CheckHistoryItemViewModel
            {
                Timestamp = cr.CheckedAt,
                ResponseTimeMs = cr.ResponseTimeMs,
                Status = cr.Status,
                ErrorMessage = cr.ErrorMessage
            })
            .ToListAsync(cancellationToken);
    }

    public virtual async Task<CheckUptimeViewModel?> GetCheckUptimeAsync(
        Guid checkId,
        int hours,
        CancellationToken cancellationToken = default)
    {
        hours = Math.Clamp(hours, 1, MaxHistoryHours);
        var cutoffTime = DateTimeOffset.UtcNow.AddHours(-hours);

        var check = await _samaDbContext.Checks
            .AsNoTracking()
            .Where(c => c.Id == checkId)
            .Select(c => new { c.Schedule })
            .FirstOrDefaultAsync(cancellationToken);
        if (check == null)
        {
            return null;
        }

        var checkResults = await _samaDbContext.CheckResults
            .AsNoTracking()
            .Where(cr => cr.CheckId == checkId && cr.CheckedAt >= cutoffTime)
            .OrderBy(cr => cr.CheckedAt)
            .Select(cr => new { cr.Status, cr.CheckedAt })
            .ToListAsync(cancellationToken);
        if (checkResults.Count == 0)
        {
            return null;
        }

        var totalUptime = TimeSpan.Zero;
        var totalTime = TimeSpan.Zero;

        for (int i = 0; i < checkResults.Count - 1; i++)
        {
            var currentResult = checkResults[i];
            var nextResult = checkResults[i + 1];
            var duration = nextResult.CheckedAt - currentResult.CheckedAt;
            totalTime += duration;

            if (currentResult.Status == CheckStatuses.Up || currentResult.Status == CheckStatuses.Warn)
            {
                totalUptime += duration;
            }
        }

        var lastResult = checkResults[^1];
        var now = DateTimeOffset.UtcNow;
        var timeSinceLastCheck = now - lastResult.CheckedAt;
        var expectedIntervalSeconds = ScheduleExtensions.GetExpectedIntervalSeconds(
            check.Schedule, lastResult.CheckedAt, TimeZoneExtensions.FindTimeZoneByIanaId(_globalSettings.TimeZone));
        var maxLastStateDuration = TimeSpan.FromSeconds(expectedIntervalSeconds * 2);
        var lastStateDuration = timeSinceLastCheck > maxLastStateDuration
            ? maxLastStateDuration
            : timeSinceLastCheck;

        totalTime += lastStateDuration;
        if (lastResult.Status == CheckStatuses.Up || lastResult.Status == CheckStatuses.Warn)
        {
            totalUptime += lastStateDuration;
        }

        var uptimePercentage = totalTime.TotalSeconds > 0
            ? Math.Round(totalUptime.TotalSeconds / totalTime.TotalSeconds * 100.0, 2)
            : 0.0;

        return new CheckUptimeViewModel
        {
            UptimePercentage = uptimePercentage,
            TotalChecks = checkResults.Count,
            UpCount = checkResults.Count(cr => cr.Status == CheckStatuses.Up),
            WarnCount = checkResults.Count(cr => cr.Status == CheckStatuses.Warn),
            DownCount = checkResults.Count(cr => cr.Status == CheckStatuses.Down),
        };
    }

    public virtual async Task<CheckBasicInfoViewModel?> GetCheckBasicInfoAsync(
        Guid checkId,
        CancellationToken cancellationToken = default)
    {
        var check = await _samaDbContext.Checks
            .Include(c => c.Workspace)
            .FirstOrDefaultAsync(c => c.Id == checkId, cancellationToken);

        if (check == null)
        {
            return null;
        }

        return new CheckBasicInfoViewModel
        {
            Id = check.Id,
            Name = check.Name,
            WorkspaceId = check.WorkspaceId,
            WorkspaceName = check.Workspace.Name
        };
    }

    public virtual async Task<WorkspaceIncidentTimelineViewModel> GetWorkspaceIncidentTimelineAsync(
        Guid workspaceId,
        int hours,
        int maxChecks = 100,
        CancellationToken cancellationToken = default)
    {
        hours = Math.Clamp(hours, 1, MaxHistoryHours);
        var startTime = DateTimeOffset.UtcNow.AddHours(-hours);
        var endTime = DateTimeOffset.UtcNow;

        var incrementMinutes = hours switch
        {
            <= 3 => 5,
            <= 6 => 10,
            <= 24 => 30,
            <= 72 => 60,
            _ => 120
        };

        // Align start time to increment boundary
        var alignedStartTime = AlignToIncrementBoundary(startTime, incrementMinutes, roundDown: true);

        var checks = await _samaDbContext.Checks
            .AsNoTracking()
            .Where(c => c.WorkspaceId == workspaceId && c.Enabled)
            .OrderBy(c => c.Name)
            .Take(maxChecks)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken);

        if (checks.Count == 0)
        {
            return new WorkspaceIncidentTimelineViewModel
            {
                Increments = [],
                StartTime = alignedStartTime,
                EndTime = endTime,
                Hours = hours,
                IncrementMinutes = incrementMinutes
            };
        }

        var checkIds = checks.Select(c => c.Id).ToList();
        var allResults = await _samaDbContext.CheckResults
            .AsNoTracking()
            .Where(cr => checkIds.Contains(cr.CheckId) && cr.CheckedAt >= alignedStartTime)
            .OrderBy(cr => cr.CheckedAt)
            .Select(cr => new { cr.CheckId, cr.Status, cr.CheckedAt, cr.ErrorMessage })
            .ToListAsync(cancellationToken);

        var checkLookup = checks.ToDictionary(c => c.Id, c => c.Name);
        var incrementDuration = TimeSpan.FromMinutes(incrementMinutes);
        var increments = new List<WorkspaceIncidentTimelineViewModel.TimeIncrement>();

        for (var currentTime = alignedStartTime; currentTime < endTime; currentTime = currentTime.Add(incrementDuration))
        {
            var incrementEnd = currentTime.Add(incrementDuration);
            if (incrementEnd > endTime)
            {
                incrementEnd = endTime;
            }

            var increment = new WorkspaceIncidentTimelineViewModel.TimeIncrement
            {
                StartTime = currentTime,
                EndTime = incrementEnd,
                TotalChecks = checks.Count
            };

            var checkStatusMap = new Dictionary<Guid, (string Status, string? ErrorMessage)>();

            foreach (var checkId in checkIds)
            {
                // Get all results for this check within this increment's time range
                var resultsInIncrement = allResults
                    .Where(r => r.CheckId == checkId && r.CheckedAt >= currentTime && r.CheckedAt < incrementEnd)
                    .ToList();

                if (resultsInIncrement.Count > 0)
                {
                    // Find the most severe status: Down > Warn > Up
                    var mostSevereResult = resultsInIncrement
                        .OrderByDescending(r => r.Status == CheckStatuses.Down ? 3 : r.Status == CheckStatuses.Warn ? 2 : 1)
                        .ThenByDescending(r => r.CheckedAt)
                        .First();

                    checkStatusMap[checkId] = (mostSevereResult.Status, mostSevereResult.ErrorMessage);
                }
            }

            foreach (var kvp in checkStatusMap)
            {
                var (status, errorMessage) = kvp.Value;
                var checkId = kvp.Key;

                switch (status)
                {
                    case CheckStatuses.Up:
                        increment.UpCount++;
                        break;
                    case CheckStatuses.Warn:
                        increment.WarnCount++;
                        increment.ChecksInWarn.Add(new WorkspaceIncidentTimelineViewModel.CheckStatusInfo
                        {
                            CheckId = checkId,
                            CheckName = checkLookup[checkId],
                            ErrorMessage = errorMessage
                        });
                        break;
                    case CheckStatuses.Down:
                        increment.DownCount++;
                        increment.ChecksInDown.Add(new WorkspaceIncidentTimelineViewModel.CheckStatusInfo
                        {
                            CheckId = checkId,
                            CheckName = checkLookup[checkId],
                            ErrorMessage = errorMessage
                        });
                        break;
                }
            }

            increments.Add(increment);
        }

        return new WorkspaceIncidentTimelineViewModel
        {
            Increments = increments,
            StartTime = alignedStartTime,
            EndTime = endTime,
            Hours = hours,
            IncrementMinutes = incrementMinutes
        };
    }

    private static DateTimeOffset AlignToIncrementBoundary(DateTimeOffset time, int incrementMinutes, bool roundDown)
    {
        var totalMinutes = time.Minute + (time.Hour * 60);
        var alignedMinutes = roundDown
            ? (totalMinutes / incrementMinutes) * incrementMinutes
            : ((totalMinutes + incrementMinutes - 1) / incrementMinutes) * incrementMinutes;

        return new DateTimeOffset(
            time.Year,
            time.Month,
            time.Day,
            alignedMinutes / 60,
            alignedMinutes % 60,
            0,
            time.Offset);
    }

    public virtual async Task<WorkspaceResponseTimeTrendsViewModel> GetWorkspaceResponseTimeTrendsAsync(
        Guid workspaceId,
        int hours,
        int maxChecks = 15,
        CancellationToken cancellationToken = default)
    {
        hours = Math.Clamp(hours, 1, MaxHistoryHours);
        var startTime = DateTimeOffset.UtcNow.AddHours(-hours);
        var endTime = DateTimeOffset.UtcNow;

        var checks = await _samaDbContext.Checks
            .AsNoTracking()
            .Where(c => c.WorkspaceId == workspaceId && c.Enabled)
            .OrderBy(c => c.Name)
            .Take(maxChecks)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken);

        var checkIds = checks.Select(c => c.Id).ToList();
        var allResults = await _samaDbContext.CheckResults
            .AsNoTracking()
            .Where(cr => checkIds.Contains(cr.CheckId) && cr.CheckedAt >= startTime)
            .OrderBy(cr => cr.CheckId)
            .ThenBy(cr => cr.CheckedAt)
            .Select(cr => new { cr.CheckId, cr.CheckedAt, cr.ResponseTimeMs })
            .ToListAsync(cancellationToken);

        var series = new List<WorkspaceResponseTimeTrendsViewModel.CheckResponseTimeSeries>();

        foreach (var check in checks)
        {
            var checkResults = allResults.Where(r => r.CheckId == check.Id).ToList();
            var dataPoints = checkResults
                .Select(r => new WorkspaceResponseTimeTrendsViewModel.ResponseTimeDataPoint
                {
                    Timestamp = r.CheckedAt,
                    ResponseTimeMs = r.ResponseTimeMs
                })
                .ToList();

            var validResponseTimes = checkResults.Where(r => r.ResponseTimeMs.HasValue).Select(r => r.ResponseTimeMs!.Value).ToList();
            var averageResponseTime = validResponseTimes.Any() ? (double?)validResponseTimes.Average() : null;

            series.Add(new WorkspaceResponseTimeTrendsViewModel.CheckResponseTimeSeries
            {
                CheckId = check.Id,
                CheckName = check.Name,
                DataPoints = dataPoints,
                AverageResponseTimeMs = averageResponseTime.HasValue ? Math.Round(averageResponseTime.Value, 2) : null
            });
        }

        return new WorkspaceResponseTimeTrendsViewModel
        {
            Series = series,
            StartTime = startTime,
            EndTime = endTime,
            Hours = hours
        };
    }
}
