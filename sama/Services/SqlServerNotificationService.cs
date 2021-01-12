using Dapper;
using Microsoft.Extensions.Logging;
using sama.Models;
using System;
using System.Data.Common;

namespace sama.Services
{
    public class SqlServerNotificationService : INotificationService
    {
        public const string CREATE_TABLE_SCRIPT = @"
IF OBJECT_ID(N'{0}', N'U') IS NULL
BEGIN
  CREATE TABLE {0} (
    EventId bigint IDENTITY(1,1) PRIMARY KEY,
    Timestamp datetimeoffset NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    EndpointId int NOT NULL,
    EndpointName nvarchar(64) NOT NULL,
    IsUp bit,
    RecordedDowntimeMinutes int NOT NULL,
    JsonMetadata nvarchar(max) NOT NULL
  );
END
";
        public const string INSERT_SCRIPT = @"INSERT INTO {0} (EndpointId, EndpointName, IsUp, RecordedDowntimeMinutes, JsonMetadata) VALUES (@EndpointId, @EndpointName, @IsUp, @RecordedDowntimeMinutes, @JsonMetadata);";

        private readonly ILogger<SqlServerNotificationService> _logger;
        private readonly SettingsService _settings;
        private readonly SqlConnectionWrapper _sqlConnectionWrapper;

        public SqlServerNotificationService(ILogger<SqlServerNotificationService> logger, SettingsService settings, SqlConnectionWrapper sqlConnectionWrapper)
        {
            _logger = logger;
            _settings = settings;
            _sqlConnectionWrapper = sqlConnectionWrapper;
        }

        public void NotifyDown(Endpoint endpoint, DateTimeOffset downAsOf, Exception reason)
        {
            SendToDb(new DbModel
            {
                EndpointId = endpoint.Id,
                EndpointName = endpoint.Name,
                IsUp = false,
                RecordedDowntimeMinutes = 0,
                JsonMetadata = GenerateJsonMetadata(endpoint, null, downAsOf, reason)
            });
        }

        public void NotifyMisc(Endpoint endpoint, NotificationType type)
        {
            SendToDb(new DbModel
            {
                EndpointId = endpoint.Id,
                EndpointName = endpoint.Name,
                IsUp = null,
                RecordedDowntimeMinutes = 0,
                JsonMetadata = GenerateJsonMetadata(endpoint, type, null, null)
            });
        }

        public void NotifyUp(Endpoint endpoint, DateTimeOffset? downAsOf)
        {
            SendToDb(new DbModel
            {
                EndpointId = endpoint.Id,
                EndpointName = endpoint.Name,
                IsUp = true,
                RecordedDowntimeMinutes = (downAsOf.HasValue ? (int)DateTimeOffset.Now.Subtract(downAsOf.Value).TotalMinutes : 0),
                JsonMetadata = GenerateJsonMetadata(endpoint, null, downAsOf, null)
            });
        }

        public void NotifySingleResult(Endpoint endpoint, EndpointCheckResult result)
        {
            // Ignore.
        }

        private void SendToDb(DbModel model)
        {
            if (!IsConfigured())
            {
                return;
            }

            var insertScript = FormatScript(INSERT_SCRIPT, _settings.Notifications_SqlServer_TableName);

            try
            {
                using var dbConnection = _sqlConnectionWrapper.GetSqlConnection(_settings.Notifications_SqlServer_Connection);
                dbConnection.Execute(insertScript, model);
            }
            catch (Exception)
            {
                try
                {
                    using var dbConnection = _sqlConnectionWrapper.GetSqlConnection(_settings.Notifications_SqlServer_Connection);
                    CreateDb(dbConnection, _settings.Notifications_SqlServer_TableName);
                    dbConnection.Execute(insertScript, model);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Unable to log notification in SQL Server: {ex.Message}");
                }
            }
        }

        private bool IsConfigured() => !string.IsNullOrWhiteSpace(_settings.Notifications_SqlServer_Connection) && !string.IsNullOrWhiteSpace(_settings.Notifications_SqlServer_TableName);

        private static string GenerateJsonMetadata(Endpoint endpoint, NotificationType? type, DateTimeOffset? downAsOf, Exception downReason)
        {
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                enabled = endpoint.Enabled,
                endpointType = endpoint.Kind.ToString(),
                @event = type?.ToString(),
                downAsOf = downAsOf?.ToString("r"),
                downReason = downReason?.Message
            });
        }

        private static string FormatScript(string script, string tableName) => string.Format(script, tableName.Replace("\"", "").Replace("'", ""));

        private static void CreateDb(DbConnection connection, string tableName) => connection.Execute(FormatScript(CREATE_TABLE_SCRIPT, tableName));

        public record DbModel
        {
            public int EndpointId { get; init; }
            public string EndpointName { get; init; }
            public bool? IsUp { get; init; }
            public int? RecordedDowntimeMinutes { get; init; }
            public string JsonMetadata { get; init; }
        }
    }
}
