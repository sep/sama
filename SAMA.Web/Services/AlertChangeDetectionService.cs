using SAMA.Data.Entities;

namespace SAMA.Web.Services;

/// <summary>
/// Service for detecting changes between old and new alert configurations.
/// Generates a dictionary of changed field names with friendly labels for lifecycle notifications.
/// </summary>
public class AlertChangeDetectionService
{
    /// <summary>
    /// Detects changes between the old and new alert state, including notification channel changes.
    /// Returns a dictionary where keys are friendly field names and values indicate the field changed.
    /// All keys are prefixed with the alert name for context.
    /// </summary>
    /// <param name="oldAlert">Original alert state with loaded channels</param>
    /// <param name="newName">New alert name</param>
    /// <param name="newTriggerOnWarn">New trigger on degraded setting</param>
    /// <param name="newTriggerOnDown">New trigger on down setting</param>
    /// <param name="newFailureThreshold">New failure threshold</param>
    /// <param name="newSendRecoveryNotification">New send recovery notification setting</param>
    /// <param name="newEnabled">New enabled state</param>
    /// <param name="newChannelIds">New selected channel IDs</param>
    /// <returns>Dictionary of changed field names (friendly, with alert context) to a marker value</returns>
    public virtual Dictionary<string, object> DetectChanges(
        Alert oldAlert,
        string newName,
        bool newTriggerOnWarn,
        bool newTriggerOnDown,
        int newFailureThreshold,
        bool newSendRecoveryNotification,
        bool newEnabled,
        List<Guid> newChannelIds)
    {
        var changes = new Dictionary<string, object>();
        var alertContext = $"Alert '{oldAlert.Name}'";
        var nameChanged = oldAlert.Name != newName;

        if (nameChanged)
        {
            changes[$"{alertContext} (renamed to '{newName}'): Name"] = "changed";

            // Update context to show rename for all subsequent fields
            alertContext = $"Alert '{oldAlert.Name}' (renamed to '{newName}')";
        }

        if (oldAlert.TriggerOnWarn != newTriggerOnWarn)
        {
            changes[$"{alertContext}: Trigger on Degraded"] = "changed";
        }

        if (oldAlert.TriggerOnDown != newTriggerOnDown)
        {
            changes[$"{alertContext}: Trigger on Down"] = "changed";
        }

        if (oldAlert.FailureThreshold != newFailureThreshold)
        {
            changes[$"{alertContext}: Failure Threshold"] = "changed";
        }

        if (oldAlert.SendRecoveryNotification != newSendRecoveryNotification)
        {
            changes[$"{alertContext}: Send Recovery Notification"] = "changed";
        }

        if (oldAlert.Enabled != newEnabled)
        {
            changes[$"{alertContext}: Enabled"] = "changed";
        }

        DetectChannelChanges(oldAlert.NotificationChannels, newChannelIds, changes, alertContext);

        changes[$"{alertContext}: Updated At"] = "changed";

        return changes;
    }

    /// <summary>
    /// Generates a friendly description for alert creation.
    /// </summary>
    public virtual Dictionary<string, object> BuildCreationInfo(
        string alertName,
        bool triggerOnWarn,
        bool triggerOnDown,
        int failureThreshold,
        bool sendRecoveryNotification,
        bool enabled,
        List<Guid> channelIds)
    {
        var info = new Dictionary<string, object>
        {
            ["Alert Created"] = alertName
        };

        var triggers = new List<string>();
        if (triggerOnWarn)
        {
            triggers.Add("Degraded");
        }
        if (triggerOnDown)
        {
            triggers.Add("Down");
        }
        if (triggers.Count > 0)
        {
            info["Triggers"] = string.Join(", ", triggers);
        }

        info["Failure Threshold"] = failureThreshold.ToString();

        if (sendRecoveryNotification)
        {
            info["Recovery Notifications"] = "enabled";
        }

        if (!enabled)
        {
            info["Alert Status"] = "disabled";
        }

        if (channelIds.Count > 0)
        {
            info["Notification Channels"] = $"{channelIds.Count} selected";
        }
        else
        {
            info["Notification Channels"] = "all workspace channels";
        }

        return info;
    }

    /// <summary>
    /// Generates a friendly description for alert deletion.
    /// </summary>
    public virtual Dictionary<string, object> BuildDeletionInfo(string alertName)
    {
        return new Dictionary<string, object>
        {
            ["Alert Deleted"] = alertName
        };
    }

    private void DetectChannelChanges(
        ICollection<NotificationChannel> oldChannels,
        List<Guid> newChannelIds,
        Dictionary<string, object> changes,
        string alertContext)
    {
        var oldChannelIds = oldChannels.Select(c => c.Id).ToHashSet();
        var newChannelIdsSet = newChannelIds.ToHashSet();

        var addedChannels = newChannelIdsSet.Except(oldChannelIds).ToList();
        var removedChannels = oldChannelIds.Except(newChannelIdsSet).ToList();

        if (addedChannels.Count > 0 || removedChannels.Count > 0)
        {
            var channelChanges = new List<string>();

            if (addedChannels.Count > 0)
            {
                channelChanges.Add($"{addedChannels.Count} added");
            }

            if (removedChannels.Count > 0)
            {
                channelChanges.Add($"{removedChannels.Count} removed");
            }

            changes[$"{alertContext}: Notification Channels"] = string.Join(", ", channelChanges);
        }
    }
}
