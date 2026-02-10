using System.Text.Json;
using System.Text.RegularExpressions;
using SAMA.Data.Entities;

namespace SAMA.Web.Services;

/// <summary>
/// Service for detecting changes between old and new check configurations.
/// Generates a dictionary of changed field names with friendly labels for lifecycle notifications.
/// </summary>
public class CheckChangeDetectionService
{
    /// <summary>
    /// Detects changes between the old and new check state.
    /// Returns a dictionary where keys are friendly field names and values indicate the field changed.
    /// </summary>
    /// <param name="oldCheck">Original check state</param>
    /// <param name="newName">New check name</param>
    /// <param name="newDescription">New check description</param>
    /// <param name="newCheckType">New check type</param>
    /// <param name="newSchedule">New schedule (interval seconds or cron expression)</param>
    /// <param name="newTimeoutSeconds">New timeout in seconds</param>
    /// <param name="newConfiguration">New configuration JSON</param>
    /// <param name="newEnabled">New enabled state</param>
    /// <returns>Dictionary of changed field names (friendly) to a marker value</returns>
    public virtual Dictionary<string, object> DetectChanges(
        Check oldCheck,
        string newName,
        string? newDescription,
        string newCheckType,
        string newSchedule,
        int newTimeoutSeconds,
        Dictionary<string, JsonElement> newConfiguration,
        bool newEnabled)
    {
        var changes = new Dictionary<string, object>();

        if (oldCheck.Name != newName)
        {
            changes["Name"] = "changed";
        }

        if (oldCheck.Description != newDescription)
        {
            changes["Description"] = "changed";
        }

        if (oldCheck.CheckType != newCheckType)
        {
            changes["Check Type"] = "changed";
        }

        if (oldCheck.Schedule != newSchedule)
        {
            changes["Schedule"] = "changed";
        }

        if (oldCheck.TimeoutSeconds != newTimeoutSeconds)
        {
            changes["Timeout"] = "changed";
        }

        if (oldCheck.Enabled != newEnabled)
        {
            changes["Enabled"] = "changed";
        }

        changes["Updated At"] = "changed";

        DetectConfigurationChanges(oldCheck.ConfigurationJson, newConfiguration, changes);

        return changes;
    }

    private static string ToFriendlyName(string camelCaseName)
    {
        // Insert spaces between lowercase and uppercase letters
        var spacedName = Regex.Replace(camelCaseName, "([a-z])([A-Z])", "$1 $2");

        // Uppercase common acronyms
        spacedName = Regex.Replace(spacedName, @"\bCa\b", "CA");
        spacedName = Regex.Replace(spacedName, @"\bSsl\b", "SSL");
        spacedName = Regex.Replace(spacedName, @"\bUrl\b", "URL");
        spacedName = Regex.Replace(spacedName, @"\bHttp\b", "HTTP");

        return spacedName;
    }

    private void DetectConfigurationChanges(
        Dictionary<string, JsonElement> oldConfig,
        Dictionary<string, JsonElement> newConfig,
        Dictionary<string, object> changes)
    {
        // Exclude TimeoutSeconds from configuration changes since it's tracked as a top-level Check property
        var allKeys = oldConfig.Keys.Union(newConfig.Keys)
            .Distinct()
            .Where(k => k != "TimeoutSeconds");

        foreach (var key in allKeys)
        {
            var oldExists = oldConfig.TryGetValue(key, out var oldElement);
            var newExists = newConfig.TryGetValue(key, out var newElement);

            if (oldExists != newExists)
            {
                changes[ToFriendlyName(key)] = "changed";
                continue;
            }

            if (!oldExists && !newExists)
            {
                continue;
            }

            if (oldElement.ValueKind == JsonValueKind.Array && newElement.ValueKind == JsonValueKind.Array)
            {
                if (!ArraysEqual(oldElement, newElement))
                {
                    changes[ToFriendlyName(key)] = "changed";
                }
            }
            else if (!JsonElementsEqual(oldElement, newElement))
            {
                changes[ToFriendlyName(key)] = "changed";
            }
        }
    }

    private bool JsonElementsEqual(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            return false;
        }

        return a.ValueKind switch
        {
            JsonValueKind.String => a.GetString() == b.GetString(),
            JsonValueKind.Number => Math.Abs(a.GetDouble() - b.GetDouble()) < 0.0001,
            JsonValueKind.True or JsonValueKind.False => a.GetBoolean() == b.GetBoolean(),
            JsonValueKind.Null => true,
            JsonValueKind.Undefined => true,
            _ => a.GetRawText() == b.GetRawText()
        };
    }

    private bool ArraysEqual(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != JsonValueKind.Array || b.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var aArray = a.EnumerateArray().ToList();
        var bArray = b.EnumerateArray().ToList();

        if (aArray.Count != bArray.Count)
        {
            return false;
        }

        for (int i = 0; i < aArray.Count; i++)
        {
            if (!JsonElementsEqual(aArray[i], bArray[i]))
            {
                return false;
            }
        }

        return true;
    }
}
