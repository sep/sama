using Microsoft.EntityFrameworkCore;
using Quartz;
using SAMA.Data;
using SAMA.Data.Entities;

namespace SAMA.Web.Services;

[DisallowConcurrentExecution]
public class DataCleanupJob(
    IServiceProvider _serviceProvider,
    ILogger<DataCleanupJob> _logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SamaDbContext>();
        var globalSettings = scope.ServiceProvider.GetRequiredService<GlobalSettingsService>();

        var checkResultsRetentionDays = globalSettings.CheckResultsRetentionDays;
        var alertHistoryRetentionDays = globalSettings.AlertHistoryRetentionDays;
        var auditLogRetentionDays = globalSettings.AuditLogRetentionDays;

        _logger.LogInformation(
            "Data cleanup job started. Retention: CheckResults={CheckResultsRetention}d, " +
            "AlertHistory={AlertHistoryRetention}d, AuditLogs={AuditLogsRetention}d",
            checkResultsRetentionDays,
            alertHistoryRetentionDays,
            auditLogRetentionDays);

        if (checkResultsRetentionDays < 30)
        {
            _logger.LogWarning(
                "CheckResults retention is set to {Days} days, which is less than 30 days. " +
                "This may result in insufficient historical data.",
                checkResultsRetentionDays);
        }

        if (alertHistoryRetentionDays < 30)
        {
            _logger.LogWarning(
                "AlertHistory retention is set to {Days} days, which is less than 30 days. " +
                "This may result in insufficient audit trail.",
                alertHistoryRetentionDays);
        }

        if (auditLogRetentionDays < 30)
        {
            _logger.LogWarning(
                "AuditLog retention is set to {Days} days, which is less than 30 days. " +
                "This may result in insufficient audit trail.",
                auditLogRetentionDays);
        }

        var startTime = DateTimeOffset.UtcNow;

        var checkResultsDeleted = await CleanupCheckResultsAsync(dbContext, checkResultsRetentionDays, context.CancellationToken);
        var alertHistoryDeleted = await CleanupAlertHistoryAsync(dbContext, alertHistoryRetentionDays, context.CancellationToken);
        var auditLogsDeleted = await CleanupAuditLogsAsync(dbContext, auditLogRetentionDays, context.CancellationToken);

        var totalDeleted = checkResultsDeleted + alertHistoryDeleted + auditLogsDeleted;

        // Bulk deletes leave dead tuples that bloat tables/indexes until vacuumed, so reclaim space right away
        if (checkResultsDeleted > 0)
        {
            await VacuumAsync<CheckResult>(dbContext, context.CancellationToken);
        }
        if (alertHistoryDeleted > 0)
        {
            await VacuumAsync<AlertHistory>(dbContext, context.CancellationToken);
        }
        if (auditLogsDeleted > 0)
        {
            await VacuumAsync<AuditLog>(dbContext, context.CancellationToken);
        }

        var duration = DateTimeOffset.UtcNow - startTime;

        _logger.LogInformation(
            "Data cleanup job completed. Total records deleted: {TotalDeleted}, Duration: {Duration}ms",
            totalDeleted,
            duration.TotalMilliseconds);
    }

    private async Task VacuumAsync<TEntity>(SamaDbContext dbContext, CancellationToken cancellationToken)
        where TEntity : class
    {
        // Table name comes from EF's own mapping metadata, never from user input, so this is not injectable
        var tableName = dbContext.Model.FindEntityType(typeof(TEntity))!.GetTableName();
        var sql = "VACUUM (ANALYZE) \"" + tableName + "\"";
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to vacuum table {TableName} after cleanup", tableName);
        }
    }

    private async Task<int> CleanupCheckResultsAsync(
        SamaDbContext dbContext,
        int retentionDays,
        CancellationToken cancellationToken)
    {
        try
        {
            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-retentionDays);

            var deletedCount = await dbContext.CheckResults
                .Where(cr => cr.CheckedAt < cutoffDate)
                .ExecuteDeleteAsync(cancellationToken);

            if (deletedCount > 0)
            {
                await dbContext.Checks
                    .Where(c => c.LatestCheckedAt != null && c.LatestCheckedAt < cutoffDate)
                    .ExecuteUpdateAsync(
                        s => s
                        .SetProperty(c => c.LatestStatus, (string?)null)
                        .SetProperty(c => c.LatestCheckedAt, (DateTimeOffset?)null)
                        .SetProperty(c => c.LatestResponseTimeMs, (int?)null)
                        .SetProperty(c => c.LatestErrorMessage, (string?)null),
                        cancellationToken);

                _logger.LogInformation(
                    "Deleted {Count} check result(s) older than {CutoffDate}",
                    deletedCount,
                    cutoffDate);
            }
            else
            {
                _logger.LogDebug("No check results to delete (cutoff: {CutoffDate})", cutoffDate);
            }

            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to cleanup CheckResults table. Retention: {Days} days",
                retentionDays);
            return 0;
        }
    }

    private async Task<int> CleanupAlertHistoryAsync(
        SamaDbContext dbContext,
        int retentionDays,
        CancellationToken cancellationToken)
    {
        try
        {
            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-retentionDays);

            var deletedCount = await dbContext.AlertHistories
                .Where(ah => ah.SentAt < cutoffDate)
                .ExecuteDeleteAsync(cancellationToken);

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "Deleted {Count} alert history record(s) older than {CutoffDate}",
                    deletedCount,
                    cutoffDate);
            }
            else
            {
                _logger.LogDebug("No alert history to delete (cutoff: {CutoffDate})", cutoffDate);
            }

            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to cleanup AlertHistory table. Retention: {Days} days",
                retentionDays);
            return 0;
        }
    }

    private async Task<int> CleanupAuditLogsAsync(
        SamaDbContext dbContext,
        int retentionDays,
        CancellationToken cancellationToken)
    {
        try
        {
            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-retentionDays);

            var deletedCount = await dbContext.AuditLogs
                .Where(al => al.Timestamp < cutoffDate)
                .ExecuteDeleteAsync(cancellationToken);

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "Deleted {Count} audit log(s) older than {CutoffDate}",
                    deletedCount,
                    cutoffDate);
            }
            else
            {
                _logger.LogDebug("No audit logs to delete (cutoff: {CutoffDate})", cutoffDate);
            }

            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to cleanup AuditLogs table. Retention: {Days} days",
                retentionDays);
            return 0;
        }
    }
}
