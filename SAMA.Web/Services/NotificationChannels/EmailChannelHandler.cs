using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using SAMA.Data.Entities;
using SAMA.Shared.Constants;
using SAMA.Web.Constants;
using SAMA.Web.Models;
using SAMA.Web.Wrappers;

namespace SAMA.Web.Services.NotificationChannels;

[ChannelType(ChannelTypes.Email)]
public class EmailChannelHandler(
    SmtpClientFactory _smtpClientFactory,
    GlobalSettingsService _globalSettings,
    ILogger<EmailChannelHandler> _logger) : INotificationChannelHandler
{
    public async Task<NotificationResultModel> SendStatusAlertAsync(
        NotificationChannel channel,
        StatusAlertContext context,
        CancellationToken cancellationToken = default)
    {
        var (subject, body) = BuildStatusAlertEmailContent(context);
        return await SendEmailAsync(channel, subject, body, context.CheckId, "notification", cancellationToken);
    }

    public async Task<NotificationResultModel> SendLifecycleEventAsync(
        NotificationChannel channel,
        LifecycleEventContext context,
        CancellationToken cancellationToken = default)
    {
        var (subject, body) = BuildLifecycleEventEmailContent(context);
        return await SendEmailAsync(channel, subject, body, context.CheckId, "lifecycle event", cancellationToken);
    }

    public async Task<NotificationResultModel> SendStatusChangeEventAsync(
        NotificationChannel channel,
        StatusChangeEventContext context,
        CancellationToken cancellationToken = default)
    {
        var (subject, body) = BuildStatusChangeEventEmailContent(context);
        return await SendEmailAsync(channel, subject, body, context.CheckId, "status change event", cancellationToken);
    }

    private static (string Subject, string Body) BuildStatusAlertEmailContent(StatusAlertContext context)
    {
        var statusIndicator = context.Status switch
        {
            CheckStatuses.Up => "✓",
            CheckStatuses.Warn => "⚠",
            CheckStatuses.Down => "✗",
            _ => "?"
        };

        var statusName = context.Status switch
        {
            CheckStatuses.Up => "Up",
            CheckStatuses.Warn => "Degraded",
            CheckStatuses.Down => "Down",
            _ => "Unknown"
        };

        string subject;
        string description;

        if (context.IsRecovery)
        {
            var failureCountDisplay = context.ConsecutiveFailures >= AlertConstants.ConsecutiveFailureQueryLimit
                ? $"{AlertConstants.ConsecutiveFailureQueryLimit}+"
                : context.ConsecutiveFailures.ToString();

            subject = $"[SAMA] ✓ {context.CheckName} is back up";
            description = context.ConsecutiveFailures > 1
                ? $"Recovered after {failureCountDisplay} consecutive failures"
                : "Recovered";
        }
        else
        {
            var failureCountDisplay = context.ConsecutiveFailures >= AlertConstants.ConsecutiveFailureQueryLimit
                ? $"{AlertConstants.ConsecutiveFailureQueryLimit}+"
                : context.ConsecutiveFailures.ToString();

            subject = $"[SAMA] {statusIndicator} {context.CheckName} is {statusName.ToLowerInvariant()}";

            if (context.Status == CheckStatuses.Warn)
            {
                description = context.ConsecutiveFailures > 1
                    ? $"Reported degraded {failureCountDisplay} consecutive times"
                    : "Check reported degraded";
            }
            else if (context.Status == CheckStatuses.Down)
            {
                description = context.ConsecutiveFailures > 1
                    ? $"Failed {failureCountDisplay} consecutive times"
                    : "Check failed";
            }
            else
            {
                description = "Check completed";
            }
        }

        var bodyBuilder = new StringBuilder();
        bodyBuilder.AppendLine($"Status: {statusName}");
        bodyBuilder.AppendLine($"Description: {description}");
        bodyBuilder.AppendLine();

        if (context.ResponseTimeMs.HasValue)
        {
            bodyBuilder.AppendLine($"Response Time: {context.ResponseTimeMs}ms");
        }

        if (!string.IsNullOrWhiteSpace(context.ErrorMessage))
        {
            bodyBuilder.AppendLine();
            bodyBuilder.AppendLine("Error:");
            bodyBuilder.AppendLine(context.ErrorMessage);
        }

        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine($"Workspace: {context.WorkspaceName}");
        bodyBuilder.AppendLine($"Timestamp: {context.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine("--");
        bodyBuilder.AppendLine("SAMA - Service Availability Monitoring and Alerting");

        return (subject, bodyBuilder.ToString());
    }

    private static (string Subject, string Body) BuildLifecycleEventEmailContent(LifecycleEventContext context)
    {
        var eventIndicator = context.EventType switch
        {
            EventTypes.CheckCreated => "🆕",
            EventTypes.CheckUpdated => "🔄",
            EventTypes.CheckDeleted => "🗑️",
            _ => "ℹ️"
        };

        var (subject, description) = context.EventType switch
        {
            EventTypes.CheckCreated => (
                $"[SAMA] 🆕 New {CheckTypes.GetShortDisplayName(context.CheckType)} check created",
                $"Created by {context.PerformedBy}"
            ),
            EventTypes.CheckUpdated => (
                $"[SAMA] 🔄 Check updated: {context.CheckName}",
                $"Modified by {context.PerformedBy}"
            ),
            EventTypes.CheckDeleted => (
                $"[SAMA] 🗑️ Check deleted: {context.CheckName}",
                $"Removed by {context.PerformedBy}"
            ),
            _ => (
                $"[SAMA] {eventIndicator} Check {context.EventType.ToLowerInvariant()}: {context.CheckName}",
                $"Action performed by {context.PerformedBy}"
            )
        };

        var bodyBuilder = new StringBuilder();
        bodyBuilder.AppendLine($"Check: {context.CheckName}");
        bodyBuilder.AppendLine($"Type: {CheckTypes.GetShortDisplayName(context.CheckType)}");
        bodyBuilder.AppendLine($"Event: {context.EventType}");
        bodyBuilder.AppendLine($"Description: {description}");
        bodyBuilder.AppendLine();

        if (context.ConfigurationChanges != null && context.ConfigurationChanges.Count > 0)
        {
            bodyBuilder.AppendLine("Changed Fields:");
            foreach (var field in context.ConfigurationChanges.Keys)
            {
                bodyBuilder.AppendLine($"  - {field}");
            }
            bodyBuilder.AppendLine();
        }

        bodyBuilder.AppendLine($"Workspace: {context.WorkspaceName}");
        bodyBuilder.AppendLine($"Timestamp: {context.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine("--");
        bodyBuilder.AppendLine("SAMA - Service Availability Monitoring and Alerting");

        return (subject, bodyBuilder.ToString());
    }

    private static (string Subject, string Body) BuildStatusChangeEventEmailContent(StatusChangeEventContext context)
    {
        var statusIndicator = context.NewStatus switch
        {
            CheckStatuses.Up => "✓",
            CheckStatuses.Warn => "⚠",
            CheckStatuses.Down => "✗",
            _ => "?"
        };

        var subject = $"[SAMA] {statusIndicator} {context.CheckName} status changed";

        var bodyBuilder = new StringBuilder();
        bodyBuilder.AppendLine($"Check: {context.CheckName}");
        bodyBuilder.AppendLine($"Previous Status: {context.PreviousStatus}");
        bodyBuilder.AppendLine($"New Status: {context.NewStatus}");
        bodyBuilder.AppendLine();

        if (context.ResponseTimeMs.HasValue)
        {
            bodyBuilder.AppendLine($"Response Time: {context.ResponseTimeMs}ms");
        }

        if (!string.IsNullOrWhiteSpace(context.ErrorMessage))
        {
            bodyBuilder.AppendLine();
            bodyBuilder.AppendLine("Error:");
            bodyBuilder.AppendLine(context.ErrorMessage);
        }

        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine($"Workspace: {context.WorkspaceName}");
        bodyBuilder.AppendLine($"Timestamp: {context.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine("--");
        bodyBuilder.AppendLine("SAMA - Service Availability Monitoring and Alerting");

        return (subject, bodyBuilder.ToString());
    }

    private async Task<NotificationResultModel> SendEmailAsync(
        NotificationChannel channel,
        string subject,
        string body,
        Guid checkId,
        string messageType,
        CancellationToken cancellationToken)
    {
        var sentAt = DateTimeOffset.UtcNow;

        try
        {
            if (!channel.ConfigurationJson.TryGetValue(ConfigurationKeys.Email.SmtpHost, out var smtpHostElement) ||
                string.IsNullOrWhiteSpace(smtpHostElement.GetString()))
            {
                return new NotificationResultModel
                {
                    Success = false,
                    ErrorMessage = "SMTP host not configured",
                    SentAt = sentAt
                };
            }

            if (!channel.ConfigurationJson.TryGetValue(ConfigurationKeys.Email.SmtpPort, out var smtpPortElement))
            {
                return new NotificationResultModel
                {
                    Success = false,
                    ErrorMessage = "SMTP port not configured",
                    SentAt = sentAt
                };
            }

            if (!channel.ConfigurationJson.TryGetValue(ConfigurationKeys.Email.FromAddress, out var fromAddressElement) ||
                string.IsNullOrWhiteSpace(fromAddressElement.GetString()))
            {
                return new NotificationResultModel
                {
                    Success = false,
                    ErrorMessage = "From address not configured",
                    SentAt = sentAt
                };
            }

            if (!channel.ConfigurationJson.TryGetValue(ConfigurationKeys.Email.Recipients, out var recipientsElement))
            {
                return new NotificationResultModel
                {
                    Success = false,
                    ErrorMessage = "Recipients not configured",
                    SentAt = sentAt
                };
            }

            var smtpHost = smtpHostElement.GetString()!;
            var smtpPort = smtpPortElement.GetInt32();
            var fromAddress = fromAddressElement.GetString()!;
            var useSsl = channel.ConfigurationJson.TryGetValue(ConfigurationKeys.Email.UseSsl, out var useSslElement) && useSslElement.GetBoolean();
            var username = channel.ConfigurationJson.TryGetValue(ConfigurationKeys.Email.Username, out var usernameElement) ? usernameElement.GetString() : null;
            var password = channel.ConfigurationJson.TryGetValue(ConfigurationKeys.Email.Password, out var passwordElement) ? passwordElement.GetString() : null;

            var recipients = new List<string>();
            foreach (var recipient in recipientsElement.EnumerateArray())
            {
                var recipientEmail = recipient.GetString();
                if (!string.IsNullOrWhiteSpace(recipientEmail))
                {
                    recipients.Add(recipientEmail);
                }
            }

            if (recipients.Count == 0)
            {
                return new NotificationResultModel
                {
                    Success = false,
                    ErrorMessage = "No valid recipients configured",
                    SentAt = sentAt
                };
            }

            using var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(fromAddress));

            foreach (var recipient in recipients)
            {
                message.To.Add(MailboxAddress.Parse(recipient));
            }

            message.Subject = subject;
            message.Body = new TextPart("plain")
            {
                Text = body
            };

            var timeoutSeconds = _globalSettings.NotificationTimeoutSeconds;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            using var client = _smtpClientFactory.CreateClient();

            // Implicit SSL/TLS supersedes StartTLS
            var secureSocketOptions = useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
            await client.ConnectAsync(smtpHost, smtpPort, secureSocketOptions, cts.Token);

            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            {
                await client.AuthenticateAsync(username, password, cts.Token);
            }

            await client.SendAsync(message, cts.Token);
            await client.DisconnectAsync(true, cts.Token);

            _logger.LogDebug(
                "Email {MessageType} sent successfully for check {CheckId} via channel {ChannelId}",
                messageType,
                checkId,
                channel.Id);

            return new NotificationResultModel
            {
                Success = true,
                SentAt = sentAt
            };
        }
        catch (MailKit.Security.AuthenticationException ex)
        {
            _logger.LogError(
                ex,
                "SMTP authentication error sending email {MessageType} for check {CheckId}",
                messageType,
                checkId);

            return new NotificationResultModel
            {
                Success = false,
                ErrorMessage = $"SMTP authentication failed: {ex.Message}",
                SentAt = sentAt
            };
        }
        catch (SmtpCommandException ex)
        {
            _logger.LogError(
                ex,
                "SMTP command error sending email {MessageType} for check {CheckId}",
                messageType,
                checkId);

            return new NotificationResultModel
            {
                Success = false,
                ErrorMessage = $"SMTP error: {ex.Message}",
                SentAt = sentAt
            };
        }
        catch (SmtpProtocolException ex)
        {
            _logger.LogError(
                ex,
                "SMTP protocol error sending email {MessageType} for check {CheckId}",
                messageType,
                checkId);

            return new NotificationResultModel
            {
                Success = false,
                ErrorMessage = $"SMTP protocol error: {ex.Message}",
                SentAt = sentAt
            };
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            _logger.LogError(
                ex,
                "Network error sending email {MessageType} for check {CheckId}",
                messageType,
                checkId);

            return new NotificationResultModel
            {
                Success = false,
                ErrorMessage = $"Network error: {ex.Message}",
                SentAt = sentAt
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Email {MessageType} cancelled for check {CheckId}",
                messageType,
                checkId);

            return new NotificationResultModel
            {
                Success = false,
                ErrorMessage = "Request cancelled",
                SentAt = sentAt
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Email {MessageType} timeout ({TimeoutSeconds}s) for check {CheckId}",
                messageType,
                _globalSettings.NotificationTimeoutSeconds,
                checkId);

            return new NotificationResultModel
            {
                Success = false,
                ErrorMessage = $"SMTP operation timeout ({_globalSettings.NotificationTimeoutSeconds}s)",
                SentAt = sentAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error sending email {MessageType} for check {CheckId}",
                messageType,
                checkId);

            return new NotificationResultModel
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}",
                SentAt = sentAt
            };
        }
    }
}
