using System.Diagnostics;
using SAMA.Data.Entities;
using SAMA.Shared.Constants;
using SAMA.Shared.Wrappers;
using SAMA.Web.Constants;
using SAMA.Web.Models;

namespace SAMA.Web.Services.NotificationChannels;

[ChannelType(ChannelTypes.Script)]
public class ScriptChannelHandler(
    ProcessFactory _processFactory,
    GlobalSettingsService _globalSettings,
    ILogger<ScriptChannelHandler> _logger) : INotificationChannelHandler
{
    public async Task<NotificationResultModel> SendStatusAlertAsync(
        NotificationChannel channel,
        StatusAlertContext context,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteScriptAsync(channel, BuildStatusAlertEnvironment(context), context.CheckId, "status alert", cancellationToken);
    }

    public async Task<NotificationResultModel> SendLifecycleEventAsync(
        NotificationChannel channel,
        LifecycleEventContext context,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteScriptAsync(channel, BuildLifecycleEventEnvironment(context), context.CheckId, "lifecycle event", cancellationToken);
    }

    public async Task<NotificationResultModel> SendStatusChangeEventAsync(
        NotificationChannel channel,
        StatusChangeEventContext context,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteScriptAsync(channel, BuildStatusChangeEventEnvironment(context), context.CheckId, "status change event", cancellationToken);
    }

    private static Dictionary<string, string> BuildStatusAlertEnvironment(StatusAlertContext context)
    {
        var env = new Dictionary<string, string>
        {
            ["SAMA_CHECK_NAME"] = context.CheckName,
            ["SAMA_CHECK_ID"] = context.CheckId.ToString(),
            ["SAMA_STATUS"] = context.Status,
            ["SAMA_TIMESTAMP"] = context.Timestamp.ToString("o"),
            ["SAMA_WORKSPACE"] = context.WorkspaceName,
            ["SAMA_IS_RECOVERY"] = context.IsRecovery.ToString(),
            ["SAMA_CONSECUTIVE_FAILURES"] = context.ConsecutiveFailures.ToString()
        };

        if (context.ResponseTimeMs.HasValue)
        {
            env["SAMA_RESPONSE_TIME_MS"] = context.ResponseTimeMs.Value.ToString();
        }

        if (!string.IsNullOrWhiteSpace(context.ErrorMessage))
        {
            env["SAMA_ERROR_MESSAGE"] = context.ErrorMessage;
        }

        return env;
    }

    private static Dictionary<string, string> BuildLifecycleEventEnvironment(LifecycleEventContext context)
    {
        var env = new Dictionary<string, string>
        {
            ["SAMA_EVENT_TYPE"] = context.EventType,
            ["SAMA_CHECK_NAME"] = context.CheckName,
            ["SAMA_CHECK_ID"] = context.CheckId.ToString(),
            ["SAMA_CHECK_TYPE"] = CheckTypes.GetShortDisplayName(context.CheckType),
            ["SAMA_TIMESTAMP"] = context.Timestamp.ToString("o"),
            ["SAMA_WORKSPACE"] = context.WorkspaceName,
            ["SAMA_PERFORMED_BY"] = context.PerformedBy
        };

        if (context.ConfigurationChanges != null && context.ConfigurationChanges.Count > 0)
        {
            env["SAMA_CHANGED_FIELDS"] = string.Join(",", context.ConfigurationChanges.Keys);
        }

        return env;
    }

    private static Dictionary<string, string> BuildStatusChangeEventEnvironment(StatusChangeEventContext context)
    {
        var env = new Dictionary<string, string>
        {
            ["SAMA_CHECK_NAME"] = context.CheckName,
            ["SAMA_CHECK_ID"] = context.CheckId.ToString(),
            ["SAMA_PREVIOUS_STATUS"] = context.PreviousStatus,
            ["SAMA_NEW_STATUS"] = context.NewStatus,
            ["SAMA_TIMESTAMP"] = context.Timestamp.ToString("o"),
            ["SAMA_WORKSPACE"] = context.WorkspaceName
        };

        if (context.ResponseTimeMs.HasValue)
        {
            env["SAMA_RESPONSE_TIME_MS"] = context.ResponseTimeMs.Value.ToString();
        }

        if (!string.IsNullOrWhiteSpace(context.ErrorMessage))
        {
            env["SAMA_ERROR_MESSAGE"] = context.ErrorMessage;
        }

        return env;
    }

    private static string TruncateErrorMessage(string errorMessage, int maxLength = 500)
    {
        if (errorMessage.Length <= maxLength)
        {
            return errorMessage;
        }

        return errorMessage[..(maxLength - 3)] + "...";
    }

    private async Task<NotificationResultModel> ExecuteScriptAsync(
        NotificationChannel channel,
        Dictionary<string, string> environmentVariables,
        Guid checkId,
        string messageType,
        CancellationToken cancellationToken)
    {
        var sentAt = DateTimeOffset.UtcNow;
        long? timestamp = null;
        string? tempScriptFile = null;

        try
        {
            if (!channel.ConfigurationJson.TryGetValue(ConfigurationKeys.Script.Path, out var scriptPathElement))
            {
                return new NotificationResultModel
                {
                    Success = false,
                    ErrorMessage = "Script path not configured",
                    SentAt = sentAt
                };
            }

            var scriptPath = scriptPathElement.GetString();

            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                return new NotificationResultModel
                {
                    Success = false,
                    ErrorMessage = "Script path not configured",
                    SentAt = sentAt
                };
            }

            var arguments = channel.ConfigurationJson.TryGetValue(ConfigurationKeys.Script.Arguments, out var argsElement)
                ? argsElement.GetString()
                : null;

            var scriptContent = channel.ConfigurationJson.TryGetValue(ConfigurationKeys.Script.Content, out var contentElement)
                ? contentElement.GetString()
                : null;

            // Handle inline script content
            if (!string.IsNullOrWhiteSpace(scriptContent))
            {
                if (string.IsNullOrWhiteSpace(arguments) ||
                    !arguments.Contains(ChannelDefaults.ScriptFilePlaceholder, StringComparison.Ordinal))
                {
                    return new NotificationResultModel
                    {
                        Success = false,
                        ErrorMessage = $"Arguments must contain {ChannelDefaults.ScriptFilePlaceholder} placeholder when using inline script content",
                        SentAt = sentAt
                    };
                }

                // Use .ps1 extension because PowerShell requires it for -File parameter.
                // Other interpreters (bash, python, etc.) ignore file extensions.
                tempScriptFile = Path.Combine(Path.GetTempPath(), $"sama-notify-{Guid.NewGuid():N}.ps1");
                await File.WriteAllTextAsync(tempScriptFile, scriptContent, cancellationToken);

                arguments = arguments.Replace(ChannelDefaults.ScriptFilePlaceholder, tempScriptFile, StringComparison.Ordinal);
            }

            var timeoutSeconds = _globalSettings.NotificationTimeoutSeconds;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            var startInfo = new ProcessStartInfo
            {
                FileName = scriptPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            if (!string.IsNullOrWhiteSpace(arguments))
            {
                startInfo.Arguments = arguments;
            }

            foreach (var kvp in environmentVariables)
            {
                startInfo.Environment[kvp.Key] = kvp.Value;
            }

            using var process = _processFactory.CreateProcess();

            timestamp = Stopwatch.GetTimestamp();
            process.Start(startInfo);

            // Start reading stdout/stderr immediately to prevent buffer deadlock.
            var stdoutTask = process.ReadStandardOutputAsync(CancellationToken.None);
            var stderrTask = process.ReadStandardErrorAsync(CancellationToken.None);

            var timedOut = false;
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Timeout occurred (our internal timeout, not external cancellation)
                timedOut = true;
                try
                {
                    process.Kill();
                }
                catch
                {
                    // Best effort - process may have already exited or we may not have permission to kill all children
                }
            }

            var executionTime = Stopwatch.GetElapsedTime(timestamp.Value);
            var executionTimeMs = (int)executionTime.TotalMilliseconds;

            // Await the output tasks - they complete when streams close (process exits or is killed)
            // We discard stdout but must drain it to prevent buffer deadlock
            _ = await stdoutTask;
            var stderr = await stderrTask;

            if (timedOut)
            {
                _logger.LogWarning(
                    "Script {MessageType} timeout for check {CheckId} after {ExecutionTimeMs}ms",
                    messageType,
                    checkId,
                    executionTimeMs);

                return new NotificationResultModel
                {
                    Success = false,
                    ErrorMessage = $"Script execution timeout ({_globalSettings.NotificationTimeoutSeconds}s)",
                    SentAt = sentAt
                };
            }

            var exitCode = process.ExitCode;

            if (exitCode == 0)
            {
                _logger.LogDebug(
                    "Script {MessageType} executed successfully for check {CheckId} via channel {ChannelId} in {ExecutionTimeMs}ms",
                    messageType,
                    checkId,
                    channel.Id,
                    executionTimeMs);

                return new NotificationResultModel
                {
                    Success = true,
                    SentAt = sentAt
                };
            }
            var errorMessage = string.IsNullOrWhiteSpace(stderr)
                ? $"Script exited with code {exitCode}"
                : $"Script exited with code {exitCode}: {TruncateErrorMessage(stderr.Trim())}";

            _logger.LogWarning(
                "Script {MessageType} failed for check {CheckId} via channel {ChannelId}: {Error}",
                messageType,
                checkId,
                channel.Id,
                errorMessage);

            return new NotificationResultModel
            {
                Success = false,
                ErrorMessage = errorMessage,
                SentAt = sentAt
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Script {MessageType} cancelled for check {CheckId}",
                messageType,
                checkId);

            return new NotificationResultModel
            {
                Success = false,
                ErrorMessage = "Request cancelled",
                SentAt = sentAt
            };
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to start script {MessageType} for check {CheckId}",
                messageType,
                checkId);

            return new NotificationResultModel
            {
                Success = false,
                ErrorMessage = $"Failed to start script: {ex.Message}",
                SentAt = sentAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error executing script {MessageType} for check {CheckId}",
                messageType,
                checkId);

            return new NotificationResultModel
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}",
                SentAt = sentAt
            };
        }
        finally
        {
            // Clean up temp script file
            if (tempScriptFile != null)
            {
                try
                {
                    await Task.Run(() => File.Delete(tempScriptFile), CancellationToken.None);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }
    }
}
