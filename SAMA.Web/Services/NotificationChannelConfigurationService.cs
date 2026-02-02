using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SAMA.Shared.Constants;
using SAMA.Shared.Utilities;
using SAMA.Web.Constants;
using SAMA.Web.Models;

namespace SAMA.Web.Services;

/// <summary>
/// Service for handling notification channel configuration serialization, deserialization, and validation.
/// Provides a single source of truth for configuration logic shared between Create and Edit operations.
/// </summary>
public class NotificationChannelConfigurationService
{
    /// <summary>
    /// Builds a configuration dictionary from the input model based on channel type.
    /// </summary>
    /// <typeparam name="T">Concrete input model type</typeparam>
    /// <param name="input">Input model</param>
    /// <returns>Dictionary of configuration values as JsonElements</returns>
    public virtual Dictionary<string, JsonElement> BuildConfiguration<T>(T input) where T : NotificationChannelInputBase
    {
        return input.ChannelType switch
        {
            ChannelTypes.Email => new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.Email.SmtpHost] = JsonSerializer.SerializeToElement(input.EmailSmtpHost!),
                [ConfigurationKeys.Email.SmtpPort] = JsonSerializer.SerializeToElement(input.EmailSmtpPort!.Value),
                [ConfigurationKeys.Email.UseSsl] = JsonSerializer.SerializeToElement(input.EmailUseSsl),
                [ConfigurationKeys.Email.Username] = JsonSerializer.SerializeToElement(input.EmailSmtpUsername ?? ""),
                [ConfigurationKeys.Email.Password] = JsonSerializer.SerializeToElement(input.EmailSmtpPassword ?? ""),
                [ConfigurationKeys.Email.FromAddress] = JsonSerializer.SerializeToElement(input.EmailFromAddress!),
                [ConfigurationKeys.Email.Recipients] = JsonSerializer.SerializeToElement(input.EmailRecipients!.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            },
            ChannelTypes.Slack => new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.Webhook.WebhookUrl] = JsonSerializer.SerializeToElement(input.SlackWebhookUrl!)
            },
            ChannelTypes.Teams => new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.Webhook.WebhookUrl] = JsonSerializer.SerializeToElement(input.TeamsWebhookUrl!)
            },
            ChannelTypes.Discord => new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.Webhook.WebhookUrl] = JsonSerializer.SerializeToElement(input.DiscordWebhookUrl!)
            },
            ChannelTypes.Script => BuildScriptConfiguration(input),
            ChannelTypes.EventGrid => new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.EventGrid.TopicEndpoint] = JsonSerializer.SerializeToElement(input.EventGridTopicEndpoint!),
                [ConfigurationKeys.EventGrid.AccessKey] = JsonSerializer.SerializeToElement(input.EventGridAccessKey!)
            },
            _ => []
        };
    }

    /// <summary>
    /// Populates the input model from a configuration dictionary based on channel type.
    /// Used when editing an existing channel to load current values.
    /// </summary>
    /// <typeparam name="T">Concrete input model type</typeparam>
    /// <param name="input">Input model</param>
    /// <param name="config">Configuration dictionary with JsonElement values</param>
    public virtual void PopulateFromConfiguration<T>(T input, Dictionary<string, JsonElement> config) where T : NotificationChannelInputBase
    {
        switch (input.ChannelType)
        {
            case ChannelTypes.Email:
                input.EmailSmtpHost = JsonElementHelper.GetString(config, ConfigurationKeys.Email.SmtpHost);
                input.EmailSmtpPort = JsonElementHelper.GetInt32(config, ConfigurationKeys.Email.SmtpPort);
                input.EmailUseSsl = JsonElementHelper.GetBoolean(config, ConfigurationKeys.Email.UseSsl) ?? false;
                input.EmailSmtpUsername = JsonElementHelper.GetString(config, ConfigurationKeys.Email.Username);
                input.EmailSmtpPassword = JsonElementHelper.GetString(config, ConfigurationKeys.Email.Password);
                input.EmailFromAddress = JsonElementHelper.GetString(config, ConfigurationKeys.Email.FromAddress);

                var recipients = JsonElementHelper.GetStringArray(config, ConfigurationKeys.Email.Recipients);
                if (recipients != null && recipients.Length > 0)
                {
                    input.EmailRecipients = string.Join(", ", recipients);
                }
                break;

            case ChannelTypes.Slack:
                input.SlackWebhookUrl = JsonElementHelper.GetString(config, ConfigurationKeys.Webhook.WebhookUrl);
                break;

            case ChannelTypes.Teams:
                input.TeamsWebhookUrl = JsonElementHelper.GetString(config, ConfigurationKeys.Webhook.WebhookUrl);
                break;

            case ChannelTypes.Discord:
                input.DiscordWebhookUrl = JsonElementHelper.GetString(config, ConfigurationKeys.Webhook.WebhookUrl);
                break;

            case ChannelTypes.Script:
                input.ScriptPath = JsonElementHelper.GetString(config, ConfigurationKeys.Script.Path);
                input.ScriptArguments = JsonElementHelper.GetString(config, ConfigurationKeys.Script.Arguments);
                input.ScriptContent = JsonElementHelper.GetString(config, ConfigurationKeys.Script.Content);
                break;

            case ChannelTypes.EventGrid:
                input.EventGridTopicEndpoint = JsonElementHelper.GetString(config, ConfigurationKeys.EventGrid.TopicEndpoint);
                input.EventGridAccessKey = JsonElementHelper.GetString(config, ConfigurationKeys.EventGrid.AccessKey);
                break;
        }
    }

    /// <summary>
    /// Validates the configuration fields based on channel type and adds errors to ModelState.
    /// </summary>
    /// <typeparam name="T">Concrete input model type</typeparam>
    /// <param name="modelState">Current model state</param>
    /// <param name="input">Input model</param>
    public virtual void ValidateConfiguration<T>(ModelStateDictionary modelState, T input) where T : NotificationChannelInputBase
    {
        switch (input.ChannelType)
        {
            case ChannelTypes.Email:
                if (string.IsNullOrWhiteSpace(input.EmailSmtpHost))
                {
                    modelState.AddModelError($"{nameof(input.EmailSmtpHost)}", "SMTP host is required");
                }
                if (!input.EmailSmtpPort.HasValue || input.EmailSmtpPort.Value <= 0)
                {
                    modelState.AddModelError($"{nameof(input.EmailSmtpPort)}", "Valid SMTP port is required");
                }
                if (string.IsNullOrWhiteSpace(input.EmailFromAddress))
                {
                    modelState.AddModelError($"{nameof(input.EmailFromAddress)}", "From address is required");
                }
                if (string.IsNullOrWhiteSpace(input.EmailRecipients))
                {
                    modelState.AddModelError($"{nameof(input.EmailRecipients)}", "At least one recipient is required");
                }
                break;

            case ChannelTypes.Slack:
                if (string.IsNullOrWhiteSpace(input.SlackWebhookUrl))
                {
                    modelState.AddModelError($"{nameof(input.SlackWebhookUrl)}", "Webhook URL is required");
                }
                if (!string.IsNullOrWhiteSpace(input.SlackWebhookUrl) && !Uri.TryCreate(input.SlackWebhookUrl, UriKind.Absolute, out _))
                {
                    modelState.AddModelError($"{nameof(input.SlackWebhookUrl)}", "Invalid webhook URL");
                }
                break;

            case ChannelTypes.Teams:
                if (string.IsNullOrWhiteSpace(input.TeamsWebhookUrl))
                {
                    modelState.AddModelError($"{nameof(input.TeamsWebhookUrl)}", "Webhook URL is required");
                }
                if (!string.IsNullOrWhiteSpace(input.TeamsWebhookUrl) && !Uri.TryCreate(input.TeamsWebhookUrl, UriKind.Absolute, out _))
                {
                    modelState.AddModelError($"{nameof(input.TeamsWebhookUrl)}", "Invalid webhook URL");
                }
                break;

            case ChannelTypes.Discord:
                if (string.IsNullOrWhiteSpace(input.DiscordWebhookUrl))
                {
                    modelState.AddModelError($"{nameof(input.DiscordWebhookUrl)}", "Webhook URL is required");
                }
                if (!string.IsNullOrWhiteSpace(input.DiscordWebhookUrl) && !Uri.TryCreate(input.DiscordWebhookUrl, UriKind.Absolute, out _))
                {
                    modelState.AddModelError($"{nameof(input.DiscordWebhookUrl)}", "Invalid webhook URL");
                }
                break;

            case ChannelTypes.Script:
                if (string.IsNullOrWhiteSpace(input.ScriptPath))
                {
                    modelState.AddModelError($"{nameof(input.ScriptPath)}", "Script path or interpreter is required");
                }

                if (!string.IsNullOrWhiteSpace(input.ScriptContent))
                {
                    if (string.IsNullOrWhiteSpace(input.ScriptArguments) ||
                        !input.ScriptArguments.Contains(ChannelDefaults.ScriptFilePlaceholder, StringComparison.Ordinal))
                    {
                        modelState.AddModelError(
                            $"{nameof(input.ScriptArguments)}",
                            $"Arguments must contain {ChannelDefaults.ScriptFilePlaceholder} placeholder when using inline script content");
                    }
                }
                break;

            case ChannelTypes.EventGrid:
                if (string.IsNullOrWhiteSpace(input.EventGridTopicEndpoint))
                {
                    modelState.AddModelError($"{nameof(input.EventGridTopicEndpoint)}", "Event Grid topic endpoint is required");
                }
                if (!string.IsNullOrWhiteSpace(input.EventGridTopicEndpoint) && !Uri.TryCreate(input.EventGridTopicEndpoint, UriKind.Absolute, out _))
                {
                    modelState.AddModelError($"{nameof(input.EventGridTopicEndpoint)}", "Invalid topic endpoint URL");
                }
                if (string.IsNullOrWhiteSpace(input.EventGridAccessKey))
                {
                    modelState.AddModelError($"{nameof(input.EventGridAccessKey)}", "Event Grid access key is required");
                }
                break;
        }
    }

    private static Dictionary<string, JsonElement> BuildScriptConfiguration<T>(T input) where T : NotificationChannelInputBase
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.Script.Path] = JsonSerializer.SerializeToElement(input.ScriptPath!),
            [ConfigurationKeys.Script.Arguments] = JsonSerializer.SerializeToElement(input.ScriptArguments ?? "")
        };

        if (!string.IsNullOrWhiteSpace(input.ScriptContent))
        {
            config[ConfigurationKeys.Script.Content] = JsonSerializer.SerializeToElement(input.ScriptContent);
        }

        return config;
    }
}
