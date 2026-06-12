using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Quartz;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Shared.Checks;
using SAMA.Shared.Constants;

namespace SAMA.Web.Services;

[DisallowConcurrentExecution]
public class CheckExecutorJob(
    IServiceProvider _serviceProvider,
    ScriptOutputBuffer _scriptOutputBuffer,
    ILogger<CheckExecutorJob> _logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SamaDbContext>();
        var alertHandler = scope.ServiceProvider.GetRequiredService<AlertHandlerService>();

        var checkId = context.JobDetail.JobDataMap.GetGuid("CheckId");

        var check = await dbContext.Checks
            .Where(c => c.Id == checkId && c.Enabled)
            .FirstOrDefaultAsync(context.CancellationToken);

        if (check == null)
        {
            _logger.LogWarning("Check {CheckId} not found or disabled, removing job", checkId);
            await context.Scheduler.DeleteJob(context.JobDetail.Key, context.CancellationToken);
            return;
        }

        _logger.LogDebug("Executing check {CheckId} ({CheckName})", check.Id, check.Name);

        var executor = scope.ServiceProvider.GetKeyedService<ICheckExecutor>(check.CheckType);

        if (executor == null)
        {
            _logger.LogError("No executor found for check type {CheckType}", check.CheckType);
            return;
        }

        try
        {
            var configuration = check.ConfigurationJson;
            configuration[ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(check.TimeoutSeconds);

            var result = await executor.ExecuteAsync(configuration, context.CancellationToken);

            // Generate ID upfront so we can correlate with script output
            var checkResultId = Guid.CreateVersion7();

            var checkResult = new CheckResult
            {
                Id = checkResultId,
                CheckId = check.Id,
                Status = result.Status,
                ResponseTimeMs = result.ResponseTimeMs,
                StatusCode = result.StatusCode,
                ErrorMessage = result.ErrorMessage,
                CheckedAt = result.CheckedAt
            };

            dbContext.CheckResults.Add(checkResult);

            if (!check.LatestCheckedAt.HasValue || result.CheckedAt >= check.LatestCheckedAt.Value)
            {
                check.LatestStatus = result.Status;
                check.LatestCheckedAt = result.CheckedAt;
                check.LatestResponseTimeMs = result.ResponseTimeMs;
                check.LatestErrorMessage = result.ErrorMessage;
            }

            await dbContext.SaveChangesAsync(context.CancellationToken);

            // Buffer script output for UI display (also logs to Serilog)
            if (check.CheckType == CheckTypes.Script)
            {
                _scriptOutputBuffer.Add(check.Id, checkResultId, result.StandardOutput, result.StandardError);
            }

            _logger.LogDebug(
                "Check {CheckId} executed: Status={Status}, ResponseTime={ResponseTime}ms",
                check.Id,
                result.Status,
                result.ResponseTimeMs);

            await alertHandler.ProcessCheckResultAsync(check.Id, result, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing check {CheckId}", check.Id);
        }
    }
}
