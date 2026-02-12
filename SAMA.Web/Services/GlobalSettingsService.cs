using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SAMA.Data;
using SAMA.Data.Entities;

namespace SAMA.Web.Services;

public class GlobalSettingsService(IServiceProvider _serviceProvider, ILogger<GlobalSettingsService> _logger)
{
    // Data retention
    private const string KeyCheckResultsRetentionDays = "CheckResultsRetentionDays";
    private const int DefaultCheckResultsRetentionDays = 365;

    private const string KeyAlertHistoryRetentionDays = "AlertHistoryRetentionDays";
    private const int DefaultAlertHistoryRetentionDays = 365;

    private const string KeyAuditLogRetentionDays = "AuditLogRetentionDays";
    private const int DefaultAuditLogRetentionDays = 365;

    // Dashboard
    private const string KeyDashboardRefreshIntervalSeconds = "DashboardRefreshIntervalSeconds";
    private const int DefaultDashboardRefreshIntervalSeconds = 5;

    private const string KeyMaxRecentAlerts = "MaxRecentAlerts";
    private const int DefaultMaxRecentAlerts = 20;

    // Checks
    private const string KeyCheckTimeoutSeconds = "DefaultCheckTimeoutSeconds";
    private const int DefaultCheckTimeoutSecondsValue = 30;

    // Notifications
    private const string KeyNotificationTimeoutSeconds = "NotificationTimeoutSeconds";
    private const int DefaultNotificationTimeoutSeconds = 30;

    // Scheduling
    private const string KeyTimeZone = "TimeZone";
    private const string DefaultTimeZone = "UTC";

    // Anonymous Users
    private const string KeyAnonymousDefaultWorkspaceId = "AnonymousDefaultWorkspaceId";

    /* --- */

    private readonly ConcurrentDictionary<string, string> _cache = new();

    public virtual int CheckResultsRetentionDays
    {
        get => GetIntAsync(KeyCheckResultsRetentionDays, DefaultCheckResultsRetentionDays).GetAwaiter().GetResult();
        set => SetIntAsync(KeyCheckResultsRetentionDays, value).GetAwaiter().GetResult();
    }

    public virtual int AlertHistoryRetentionDays
    {
        get => GetIntAsync(KeyAlertHistoryRetentionDays, DefaultAlertHistoryRetentionDays).GetAwaiter().GetResult();
        set => SetIntAsync(KeyAlertHistoryRetentionDays, value).GetAwaiter().GetResult();
    }

    public virtual int AuditLogRetentionDays
    {
        get => GetIntAsync(KeyAuditLogRetentionDays, DefaultAuditLogRetentionDays).GetAwaiter().GetResult();
        set => SetIntAsync(KeyAuditLogRetentionDays, value).GetAwaiter().GetResult();
    }

    public virtual int DashboardRefreshIntervalSeconds
    {
        get => GetIntAsync(KeyDashboardRefreshIntervalSeconds, DefaultDashboardRefreshIntervalSeconds).GetAwaiter().GetResult();
        set => SetIntAsync(KeyDashboardRefreshIntervalSeconds, value).GetAwaiter().GetResult();
    }

    public virtual int MaxRecentAlerts
    {
        get => GetIntAsync(KeyMaxRecentAlerts, DefaultMaxRecentAlerts).GetAwaiter().GetResult();
        set => SetIntAsync(KeyMaxRecentAlerts, value).GetAwaiter().GetResult();
    }

    public virtual int DefaultCheckTimeoutSeconds
    {
        get => GetIntAsync(KeyCheckTimeoutSeconds, DefaultCheckTimeoutSecondsValue).GetAwaiter().GetResult();
        set => SetIntAsync(KeyCheckTimeoutSeconds, value).GetAwaiter().GetResult();
    }

    public virtual int NotificationTimeoutSeconds
    {
        get => GetIntAsync(KeyNotificationTimeoutSeconds, DefaultNotificationTimeoutSeconds).GetAwaiter().GetResult();
        set => SetIntAsync(KeyNotificationTimeoutSeconds, value).GetAwaiter().GetResult();
    }

    public virtual string TimeZone
    {
        get => GetStringAsync(KeyTimeZone, DefaultTimeZone).GetAwaiter().GetResult();
        set => SetStringAsync(KeyTimeZone, value).GetAwaiter().GetResult();
    }

    public virtual Guid? AnonymousDefaultWorkspaceId
    {
        get => GetGuidAsync(KeyAnonymousDefaultWorkspaceId).GetAwaiter().GetResult();
        set => SetGuidAsync(KeyAnonymousDefaultWorkspaceId, value).GetAwaiter().GetResult();
    }

    /* --- */

    private async Task<int> GetIntAsync(string key, int defaultValue)
    {
        if (_cache.TryGetValue(key, out var cachedValue) && int.TryParse(cachedValue, out var intValue))
        {
            return intValue;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SamaDbContext>();

            var setting = await context.GlobalSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == key);

            if (setting != null)
            {
                if (int.TryParse(setting.Value, out intValue))
                {
                    _logger.LogDebug("Loaded global setting {Key} = {Value}", key, intValue);
                    _cache[key] = setting.Value;
                    return intValue;
                }
            }

            _logger.LogDebug("Using default value for global setting {Key} = {Value}", key, defaultValue);
            _cache[key] = defaultValue.ToString();
            return defaultValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load global setting {Key}, using default {Value}", key, defaultValue);
            return defaultValue;
        }
    }

    private async Task SetIntAsync(string key, int value)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SamaDbContext>();

        var setting = await context.GlobalSettings.FindAsync(key);
        if (setting == null)
        {
            setting = new GlobalSetting
            {
                Key = key,
                Value = value.ToString(),
                UpdatedAt = DateTimeOffset.UtcNow
            };
            context.GlobalSettings.Add(setting);
        }
        else
        {
            setting.Value = value.ToString();
            setting.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync();

        _cache[key] = value.ToString();

        _logger.LogInformation("Updated global setting {Key} to {Value}", key, value);
    }

    private async Task<string> GetStringAsync(string key, string defaultValue)
    {
        if (_cache.TryGetValue(key, out var cachedValue))
        {
            return cachedValue;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SamaDbContext>();

            var setting = await context.GlobalSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == key);

            if (setting != null && !string.IsNullOrEmpty(setting.Value))
            {
                _logger.LogDebug("Loaded global setting {Key} = {Value}", key, setting.Value);
                _cache[key] = setting.Value;
                return setting.Value;
            }

            _logger.LogDebug("Using default value for global setting {Key} = {Value}", key, defaultValue);
            _cache[key] = defaultValue;
            return defaultValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load global setting {Key}, using default {Value}", key, defaultValue);
            return defaultValue;
        }
    }

    private async Task SetStringAsync(string key, string value)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SamaDbContext>();

        var setting = await context.GlobalSettings.FindAsync(key);
        if (setting == null)
        {
            setting = new GlobalSetting
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            context.GlobalSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync();

        _cache[key] = value;

        _logger.LogInformation("Updated global setting {Key} to {Value}", key, value);
    }

    private async Task<Guid?> GetGuidAsync(string key)
    {
        if (_cache.TryGetValue(key, out var cachedValue))
        {
            if (string.IsNullOrEmpty(cachedValue))
            {
                return null;
            }

            if (Guid.TryParse(cachedValue, out var guidValue))
            {
                return guidValue;
            }
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SamaDbContext>();

            var setting = await context.GlobalSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == key);

            if (setting != null && !string.IsNullOrEmpty(setting.Value))
            {
                if (Guid.TryParse(setting.Value, out var parsedGuid))
                {
                    _logger.LogDebug("Loaded global setting {Key} = {Value}", key, parsedGuid);
                    _cache[key] = setting.Value;
                    return parsedGuid;
                }
            }

            _logger.LogDebug("Using default value for global setting {Key} = null", key);
            _cache[key] = string.Empty;
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load global setting {Key}, using default null", key);
            return null;
        }
    }

    private async Task SetGuidAsync(string key, Guid? value)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SamaDbContext>();

        var setting = await context.GlobalSettings.FindAsync(key);
        var stringValue = value?.ToString() ?? string.Empty;

        if (setting == null)
        {
            setting = new GlobalSetting
            {
                Key = key,
                Value = stringValue,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            context.GlobalSettings.Add(setting);
        }
        else
        {
            setting.Value = stringValue;
            setting.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync();

        _cache[key] = stringValue;

        _logger.LogInformation("Updated global setting {Key} to {Value}", key, value?.ToString() ?? "(null)");
    }

    public virtual void ClearCache()
    {
        _cache.Clear();
        _logger.LogDebug("Global settings cache cleared");
    }
}
