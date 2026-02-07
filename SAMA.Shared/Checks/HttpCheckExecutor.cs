using System.Diagnostics;
using System.Text;
using System.Text.Json;
using SAMA.Shared.Constants;
using SAMA.Shared.Factories;
using SAMA.Shared.Models;
using SAMA.Shared.Utilities;

namespace SAMA.Shared.Checks;

[CheckType(CheckTypes.Http)]
public class HttpCheckExecutor(ConfigurableHttpClientFactory _httpClientFactory)
    : ICheckExecutor
{
    public async Task<CheckExecutionResult> ExecuteAsync(Dictionary<string, JsonElement> configuration, CancellationToken cancellationToken = default)
    {
        long? timestamp = null;

        try
        {
            var url = JsonElementHelper.GetString(configuration, ConfigurationKeys.HttpCheck.Url);

            if (string.IsNullOrWhiteSpace(url))
            {
                return new CheckExecutionResult
                {
                    Status = CheckStatuses.Down,
                    ErrorMessage = "URL not configured"
                };
            }

            var method = JsonElementHelper.GetString(configuration, ConfigurationKeys.HttpCheck.Method, CheckDefaults.HttpMethod);

            var expectedStatusCodes = JsonElementHelper.GetInt32Array(
                configuration,
                ConfigurationKeys.HttpCheck.ExpectedStatusCodes,
                [.. CheckDefaults.HttpExpectedStatusCodes.Split(',').Select(int.Parse)]);

            var timeout = JsonElementHelper.GetInt32(
                configuration,
                ConfigurationKeys.Common.TimeoutSeconds,
                CheckDefaults.CheckTimeoutSeconds);

            var headers = JsonElementHelper.GetString(configuration, ConfigurationKeys.HttpCheck.Headers);
            var body = JsonElementHelper.GetString(configuration, ConfigurationKeys.HttpCheck.Body);
            var contentValidation = JsonElementHelper.GetString(configuration, ConfigurationKeys.HttpCheck.ContentValidation);
            var responseTimeWarnThresholdMs = JsonElementHelper.GetInt32(configuration, ConfigurationKeys.HttpCheck.ResponseTimeWarnThresholdMs);
            var followRedirects = JsonElementHelper.GetBoolean(configuration, ConfigurationKeys.HttpCheck.FollowRedirects, CheckDefaults.HttpFollowRedirects);
            var allowInvalidSsl = JsonElementHelper.GetBoolean(configuration, ConfigurationKeys.HttpCheck.AllowInvalidSsl, CheckDefaults.HttpAllowInvalidSsl);

            using var client = _httpClientFactory.CreateClient(followRedirects, allowInvalidSsl, timeout);

            using var request = new HttpRequestMessage(new HttpMethod(method), url);

            request.Headers.ConnectionClose = true;

            if (!string.IsNullOrWhiteSpace(body))
            {
                request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
            }

            if (!string.IsNullOrWhiteSpace(headers))
            {
                ParseAndAddHeaders(request, headers);
            }

            timestamp = Stopwatch.GetTimestamp();
            var response = await client.SendAsync(request, cancellationToken);
            var responseTime = Stopwatch.GetElapsedTime(timestamp.Value);

            var statusCode = (int)response.StatusCode;
            var isExpectedStatus = expectedStatusCodes.Contains(statusCode);

            if (!isExpectedStatus)
            {
                return new CheckExecutionResult
                {
                    Status = CheckStatuses.Down,
                    ResponseTimeMs = (int)responseTime.TotalMilliseconds,
                    StatusCode = statusCode,
                    ErrorMessage = $"Unexpected status code: {statusCode}"
                };
            }

            if (!string.IsNullOrWhiteSpace(contentValidation))
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!responseBody.Contains(contentValidation, StringComparison.Ordinal))
                {
                    return new CheckExecutionResult
                    {
                        Status = CheckStatuses.Down,
                        ResponseTimeMs = (int)responseTime.TotalMilliseconds,
                        StatusCode = statusCode,
                        ErrorMessage = "Content validation failed: expected content not found"
                    };
                }
            }

            var responseTimeMs = (int)responseTime.TotalMilliseconds;
            var status = CheckStatuses.Up;
            string? errorMessage = null;

            if (responseTimeWarnThresholdMs.HasValue && responseTimeMs > responseTimeWarnThresholdMs.Value)
            {
                status = CheckStatuses.Warn;
                errorMessage = $"Response time ({responseTimeMs}ms) exceeded threshold ({responseTimeWarnThresholdMs.Value}ms)";
            }

            return new CheckExecutionResult
            {
                Status = status,
                ResponseTimeMs = responseTimeMs,
                StatusCode = statusCode,
                ErrorMessage = errorMessage
            };
        }
        catch (HttpRequestException ex)
        {
            return new CheckExecutionResult
            {
                Status = CheckStatuses.Down,
                ResponseTimeMs = timestamp.HasValue ? (int)Stopwatch.GetElapsedTime(timestamp.Value).TotalMilliseconds : null,
                ErrorMessage = $"HTTP request failed: {ex.Message}"
            };
        }
        catch (TaskCanceledException)
        {
            return new CheckExecutionResult
            {
                Status = CheckStatuses.Down,
                ResponseTimeMs = timestamp.HasValue ? (int)Stopwatch.GetElapsedTime(timestamp.Value).TotalMilliseconds : null,
                ErrorMessage = "Request timeout"
            };
        }
        catch (Exception ex)
        {
            return new CheckExecutionResult
            {
                Status = CheckStatuses.Down,
                ResponseTimeMs = timestamp.HasValue ? (int)Stopwatch.GetElapsedTime(timestamp.Value).TotalMilliseconds : null,
                ErrorMessage = $"Unexpected error: {ex.Message}"
            };
        }
    }

    private static void ParseAndAddHeaders(HttpRequestMessage request, string headers)
    {
        var lines = headers.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var colonIndex = line.IndexOf(':');
            if (colonIndex <= 0)
            {
                continue;
            }

            var headerName = line[..colonIndex].Trim();
            var headerValue = line[(colonIndex + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(headerName))
            {
                continue;
            }

            if (string.Equals(headerName, "Content-Type", StringComparison.OrdinalIgnoreCase) && request.Content != null)
            {
                request.Content.Headers.TryAddWithoutValidation(headerName, headerValue);
            }
            else
            {
                request.Headers.TryAddWithoutValidation(headerName, headerValue);
            }
        }
    }
}
