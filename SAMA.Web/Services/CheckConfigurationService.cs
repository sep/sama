using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SAMA.Shared.Constants;
using SAMA.Shared.Utilities;
using SAMA.Web.Models;

namespace SAMA.Web.Services;

/// <summary>
/// Service for handling check configuration serialization, deserialization, and validation.
/// Provides a single source of truth for configuration logic shared between Create and Edit operations.
/// </summary>
public class CheckConfigurationService
{
    /// <summary>
    /// Builds a configuration dictionary from the input model based on check type.
    /// </summary>
    /// <typeparam name="T">Concrete input model type</typeparam>
    /// <param name="input">Input model</param>
    /// <returns>Dictionary of configuration values as JsonElements</returns>
    public virtual Dictionary<string, JsonElement> BuildConfiguration<T>(T input) where T : CheckInputBase
    {
        return input.CheckType switch
        {
            CheckTypes.Http => BuildHttpConfiguration(input),
            CheckTypes.Tcp => BuildTcpConfiguration(input),
            CheckTypes.Ping => BuildPingConfiguration(input),
            CheckTypes.Dns => BuildDnsConfiguration(input),
            CheckTypes.Tls => BuildTlsConfiguration(input),
            CheckTypes.Script => BuildScriptConfiguration(input),
            _ => []
        };
    }

    /// <summary>
    /// Populates the input model from a configuration dictionary based on check type.
    /// Used when editing an existing check to load current values.
    /// </summary>
    /// <typeparam name="T">Concrete input model type</typeparam>
    /// <param name="input">Input model</param>
    /// <param name="config">Configuration dictionary with JsonElement values</param>
    public virtual void PopulateFromConfiguration<T>(T input, Dictionary<string, JsonElement> config) where T : CheckInputBase
    {
        switch (input.CheckType)
        {
            case CheckTypes.Http:
                input.HttpUrl = JsonElementHelper.GetString(config, ConfigurationKeys.HttpCheck.Url);
                input.HttpMethod = JsonElementHelper.GetString(config, ConfigurationKeys.HttpCheck.Method) ?? CheckDefaults.HttpMethod;
                input.HttpHeaders = JsonElementHelper.GetString(config, ConfigurationKeys.HttpCheck.Headers);
                input.HttpBody = JsonElementHelper.GetString(config, ConfigurationKeys.HttpCheck.Body);

                var statusCodes = JsonElementHelper.GetInt32Array(config, ConfigurationKeys.HttpCheck.ExpectedStatusCodes);
                if (statusCodes != null && statusCodes.Length > 0)
                {
                    input.HttpExpectedStatusCodes = string.Join(", ", statusCodes);
                }

                input.HttpContentValidation = JsonElementHelper.GetString(config, ConfigurationKeys.HttpCheck.ContentValidation);
                input.HttpFollowRedirects = JsonElementHelper.GetBoolean(config, ConfigurationKeys.HttpCheck.FollowRedirects) ?? CheckDefaults.HttpFollowRedirects;
                input.HttpAllowInvalidSsl = JsonElementHelper.GetBoolean(config, ConfigurationKeys.HttpCheck.AllowInvalidSsl) ?? CheckDefaults.HttpAllowInvalidSsl;
                input.HttpResponseTimeWarnThresholdMs = JsonElementHelper.GetInt32(config, ConfigurationKeys.HttpCheck.ResponseTimeWarnThresholdMs);
                break;

            case CheckTypes.Tcp:
                input.TcpHost = JsonElementHelper.GetString(config, ConfigurationKeys.TcpCheck.Host);
                input.TcpPort = JsonElementHelper.GetInt32(config, ConfigurationKeys.TcpCheck.Port);
                input.TcpConnectionTimeWarnThresholdMs = JsonElementHelper.GetInt32(config, ConfigurationKeys.TcpCheck.ConnectionTimeWarnThresholdMs);
                break;

            case CheckTypes.Ping:
                input.PingHost = JsonElementHelper.GetString(config, ConfigurationKeys.PingCheck.Host);
                input.PingPacketCount = JsonElementHelper.GetInt32(config, ConfigurationKeys.PingCheck.PacketCount) ?? CheckDefaults.PingPacketCount;
                input.PingPacketLossThresholdPercent = JsonElementHelper.GetInt32(config, ConfigurationKeys.PingCheck.PacketLossThresholdPercent) ?? CheckDefaults.PingPacketLossThresholdPercent;
                break;

            case CheckTypes.Dns:
                input.DnsHostname = JsonElementHelper.GetString(config, ConfigurationKeys.DnsCheck.Hostname);
                input.DnsRecordType = JsonElementHelper.GetString(config, ConfigurationKeys.DnsCheck.RecordType) ?? CheckDefaults.DnsRecordType;

                var expectedValues = JsonElementHelper.GetStringArray(config, ConfigurationKeys.DnsCheck.ExpectedValues);
                if (expectedValues != null && expectedValues.Length > 0)
                {
                    input.DnsExpectedValues = string.Join("\n", expectedValues);
                }
                break;

            case CheckTypes.Tls:
                input.TlsUrl = JsonElementHelper.GetString(config, ConfigurationKeys.TlsCheck.Url);
                input.TlsDaysBeforeExpiryWarning = JsonElementHelper.GetInt32(config, ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning) ?? CheckDefaults.TlsDaysBeforeExpiryWarning;
                input.TlsCustomCaCertificate = JsonElementHelper.GetString(config, ConfigurationKeys.TlsCheck.CustomCaCertificate);
                break;

            case CheckTypes.Script:
                input.ScriptPath = JsonElementHelper.GetString(config, ConfigurationKeys.ScriptCheck.Path);
                input.ScriptArguments = JsonElementHelper.GetString(config, ConfigurationKeys.ScriptCheck.Arguments);
                input.ScriptExpectedExitCode = JsonElementHelper.GetInt32(config, ConfigurationKeys.ScriptCheck.ExpectedExitCode) ?? CheckDefaults.ScriptExpectedExitCode;
                input.ScriptContent = JsonElementHelper.GetString(config, ConfigurationKeys.ScriptCheck.Content);
                break;
        }
    }

    /// <summary>
    /// Validates the configuration fields based on check type and adds errors to ModelState.
    /// </summary>
    /// <typeparam name="T">Concrete input model type</typeparam>
    /// <param name="modelState">Current model state</param>
    /// <param name="input">Input model</param>
    public virtual void ValidateConfiguration<T>(ModelStateDictionary modelState, T input) where T : CheckInputBase
    {
        switch (input.CheckType)
        {
            case CheckTypes.Http:
                if (string.IsNullOrWhiteSpace(input.HttpUrl))
                {
                    modelState.AddModelError($"{nameof(input.HttpUrl)}", "URL is required");
                }
                else if (!Uri.TryCreate(input.HttpUrl, UriKind.Absolute, out _))
                {
                    modelState.AddModelError($"{nameof(input.HttpUrl)}", "Invalid URL format");
                }

                if (string.IsNullOrWhiteSpace(input.HttpExpectedStatusCodes))
                {
                    modelState.AddModelError($"{nameof(input.HttpExpectedStatusCodes)}", "At least one expected status code is required");
                }

                if (input.HttpResponseTimeWarnThresholdMs.HasValue && input.HttpResponseTimeWarnThresholdMs.Value <= 0)
                {
                    modelState.AddModelError($"{nameof(input.HttpResponseTimeWarnThresholdMs)}", "Response time warning threshold must be greater than 0");
                }
                break;

            case CheckTypes.Tcp:
                if (string.IsNullOrWhiteSpace(input.TcpHost))
                {
                    modelState.AddModelError($"{nameof(input.TcpHost)}", "Host is required");
                }
                if (!input.TcpPort.HasValue || input.TcpPort.Value <= 0 || input.TcpPort.Value > 65535)
                {
                    modelState.AddModelError($"{nameof(input.TcpPort)}", "Valid port (1-65535) is required");
                }

                if (input.TcpConnectionTimeWarnThresholdMs.HasValue && input.TcpConnectionTimeWarnThresholdMs.Value <= 0)
                {
                    modelState.AddModelError($"{nameof(input.TcpConnectionTimeWarnThresholdMs)}", "Connection time warning threshold must be greater than 0");
                }
                break;

            case CheckTypes.Ping:
                if (string.IsNullOrWhiteSpace(input.PingHost))
                {
                    modelState.AddModelError($"{nameof(input.PingHost)}", "Host is required");
                }
                if (!input.PingPacketCount.HasValue || input.PingPacketCount.Value <= 0)
                {
                    modelState.AddModelError($"{nameof(input.PingPacketCount)}", "Packet count must be greater than 0");
                }
                if (!input.PingPacketLossThresholdPercent.HasValue || input.PingPacketLossThresholdPercent.Value < 0 || input.PingPacketLossThresholdPercent.Value > 100)
                {
                    modelState.AddModelError($"{nameof(input.PingPacketLossThresholdPercent)}", "Packet loss threshold must be between 0 and 100");
                }
                break;

            case CheckTypes.Dns:
                if (string.IsNullOrWhiteSpace(input.DnsHostname))
                {
                    modelState.AddModelError($"{nameof(input.DnsHostname)}", "Hostname is required");
                }
                break;

            case CheckTypes.Tls:
                if (string.IsNullOrWhiteSpace(input.TlsUrl))
                {
                    modelState.AddModelError($"{nameof(input.TlsUrl)}", "URL is required");
                }
                else if (!Uri.TryCreate(input.TlsUrl, UriKind.Absolute, out _))
                {
                    modelState.AddModelError($"{nameof(input.TlsUrl)}", "Valid absolute URL is required");
                }

                if (!input.TlsDaysBeforeExpiryWarning.HasValue || input.TlsDaysBeforeExpiryWarning.Value <= 0)
                {
                    modelState.AddModelError($"{nameof(input.TlsDaysBeforeExpiryWarning)}", "Days before expiry warning must be greater than 0");
                }
                break;

            case CheckTypes.Script:
                if (string.IsNullOrWhiteSpace(input.ScriptPath))
                {
                    modelState.AddModelError($"{nameof(input.ScriptPath)}", "Script path or interpreter is required");
                }

                if (!string.IsNullOrWhiteSpace(input.ScriptContent))
                {
                    if (string.IsNullOrWhiteSpace(input.ScriptArguments) ||
                        !input.ScriptArguments.Contains(CheckDefaults.ScriptFilePlaceholder, StringComparison.Ordinal))
                    {
                        modelState.AddModelError(
                            $"{nameof(input.ScriptArguments)}",
                            $"Arguments must contain {CheckDefaults.ScriptFilePlaceholder} placeholder when using inline script content");
                    }
                }
                break;
        }
    }

    private Dictionary<string, JsonElement> BuildHttpConfiguration<T>(T input) where T : CheckInputBase
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement(input.HttpUrl!),
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement(input.HttpMethod ?? CheckDefaults.HttpMethod),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(
                input.HttpExpectedStatusCodes!.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(int.Parse).ToArray()),
            [ConfigurationKeys.HttpCheck.FollowRedirects] = JsonSerializer.SerializeToElement(input.HttpFollowRedirects),
            [ConfigurationKeys.HttpCheck.AllowInvalidSsl] = JsonSerializer.SerializeToElement(input.HttpAllowInvalidSsl)
        };

        if (!string.IsNullOrWhiteSpace(input.HttpHeaders))
        {
            config[ConfigurationKeys.HttpCheck.Headers] = JsonSerializer.SerializeToElement(input.HttpHeaders);
        }

        if (!string.IsNullOrWhiteSpace(input.HttpBody))
        {
            config[ConfigurationKeys.HttpCheck.Body] = JsonSerializer.SerializeToElement(input.HttpBody);
        }

        if (!string.IsNullOrWhiteSpace(input.HttpContentValidation))
        {
            config[ConfigurationKeys.HttpCheck.ContentValidation] = JsonSerializer.SerializeToElement(input.HttpContentValidation);
        }

        if (input.HttpResponseTimeWarnThresholdMs.HasValue)
        {
            config[ConfigurationKeys.HttpCheck.ResponseTimeWarnThresholdMs] = JsonSerializer.SerializeToElement(input.HttpResponseTimeWarnThresholdMs.Value);
        }

        return config;
    }

    private Dictionary<string, JsonElement> BuildTcpConfiguration<T>(T input) where T : CheckInputBase
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TcpCheck.Host] = JsonSerializer.SerializeToElement(input.TcpHost!),
            [ConfigurationKeys.TcpCheck.Port] = JsonSerializer.SerializeToElement(input.TcpPort!.Value)
        };

        if (input.TcpConnectionTimeWarnThresholdMs.HasValue)
        {
            config[ConfigurationKeys.TcpCheck.ConnectionTimeWarnThresholdMs] = JsonSerializer.SerializeToElement(input.TcpConnectionTimeWarnThresholdMs.Value);
        }

        return config;
    }

    private Dictionary<string, JsonElement> BuildPingConfiguration<T>(T input) where T : CheckInputBase
    {
        return new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.PingCheck.Host] = JsonSerializer.SerializeToElement(input.PingHost!),
            [ConfigurationKeys.PingCheck.PacketCount] = JsonSerializer.SerializeToElement(input.PingPacketCount ?? CheckDefaults.PingPacketCount),
            [ConfigurationKeys.PingCheck.PacketLossThresholdPercent] = JsonSerializer.SerializeToElement(input.PingPacketLossThresholdPercent ?? CheckDefaults.PingPacketLossThresholdPercent)
        };
    }

    private Dictionary<string, JsonElement> BuildDnsConfiguration<T>(T input) where T : CheckInputBase
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.DnsCheck.Hostname] = JsonSerializer.SerializeToElement(input.DnsHostname!),
            [ConfigurationKeys.DnsCheck.RecordType] = JsonSerializer.SerializeToElement(input.DnsRecordType ?? CheckDefaults.DnsRecordType)
        };

        if (!string.IsNullOrWhiteSpace(input.DnsExpectedValues))
        {
            var expectedValues = input.DnsExpectedValues.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            config[ConfigurationKeys.DnsCheck.ExpectedValues] = JsonSerializer.SerializeToElement(expectedValues);
        }

        return config;
    }

    private Dictionary<string, JsonElement> BuildTlsConfiguration<T>(T input) where T : CheckInputBase
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.Url] = JsonSerializer.SerializeToElement(input.TlsUrl!),
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(input.TlsDaysBeforeExpiryWarning ?? CheckDefaults.TlsDaysBeforeExpiryWarning)
        };

        if (!string.IsNullOrWhiteSpace(input.TlsCustomCaCertificate))
        {
            config[ConfigurationKeys.TlsCheck.CustomCaCertificate] = JsonSerializer.SerializeToElement(input.TlsCustomCaCertificate);
        }

        return config;
    }

    private Dictionary<string, JsonElement> BuildScriptConfiguration<T>(T input) where T : CheckInputBase
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.ScriptCheck.Path] = JsonSerializer.SerializeToElement(input.ScriptPath!),
            [ConfigurationKeys.ScriptCheck.ExpectedExitCode] = JsonSerializer.SerializeToElement(input.ScriptExpectedExitCode ?? CheckDefaults.ScriptExpectedExitCode)
        };

        if (!string.IsNullOrWhiteSpace(input.ScriptArguments))
        {
            config[ConfigurationKeys.ScriptCheck.Arguments] = JsonSerializer.SerializeToElement(input.ScriptArguments);
        }

        if (!string.IsNullOrWhiteSpace(input.ScriptContent))
        {
            config[ConfigurationKeys.ScriptCheck.Content] = JsonSerializer.SerializeToElement(input.ScriptContent);
        }

        return config;
    }
}
