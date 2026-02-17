using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using SAMA.Shared.Constants;
using SAMA.Shared.Models;
using SAMA.Shared.Utilities;
using SAMA.Shared.Wrappers;

namespace SAMA.Shared.Checks;

[CheckType(CheckTypes.Tls)]
public class TlsCheckExecutor(
    TcpClientFactory _tcpClientFactory,
    SslStreamFactory _sslStreamFactory,
    CustomTlsValidator _customTlsValidator) : ICheckExecutor
{
    private record CertificateValidationResult(
        DateTimeOffset NotBefore,
        DateTimeOffset NotAfter,
        SslPolicyErrors Errors,
        bool IsValid,
        string? Subject,
        string? Issuer,
        string? Thumbprint,
        List<string>? ChainStatusInfo);

    public async Task<CheckExecutionResult> ExecuteAsync(Dictionary<string, JsonElement> configuration, CancellationToken cancellationToken = default)
    {
        long? timestamp = null;

        try
        {
            var url = JsonElementHelper.GetString(configuration, ConfigurationKeys.TlsCheck.Url);

            if (string.IsNullOrWhiteSpace(url))
            {
                return new CheckExecutionResult
                {
                    Status = CheckStatuses.Down,
                    ErrorMessage = "URI not configured"
                };
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return new CheckExecutionResult
                {
                    Status = CheckStatuses.Down,
                    ErrorMessage = "URI must be a valid absolute URI"
                };
            }

            var daysBeforeExpiryWarning = JsonElementHelper.GetInt32(
                configuration,
                ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning,
                CheckDefaults.TlsDaysBeforeExpiryWarning);

            if (daysBeforeExpiryWarning <= 0)
            {
                return new CheckExecutionResult
                {
                    Status = CheckStatuses.Down,
                    ErrorMessage = "Days before expiry warning must be greater than 0"
                };
            }

            var customCaCertificate = JsonElementHelper.GetString(configuration, ConfigurationKeys.TlsCheck.CustomCaCertificate);

            var timeoutSeconds = JsonElementHelper.GetInt32(
                configuration,
                ConfigurationKeys.Common.TimeoutSeconds,
                CheckDefaults.CheckTimeoutSeconds);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 443;

            if (port > 65535)
            {
                return new CheckExecutionResult
                {
                    Status = CheckStatuses.Down,
                    ErrorMessage = $"Invalid port number: {port}"
                };
            }

            using var tcpClient = _tcpClientFactory.CreateClient();

            timestamp = Stopwatch.GetTimestamp();
            try
            {
                await tcpClient.ConnectAsync(host, port, cts.Token);
            }
            catch (SocketException ex)
            {
                var connectionTime = Stopwatch.GetElapsedTime(timestamp.Value);
                return new CheckExecutionResult
                {
                    Status = CheckStatuses.Down,
                    ResponseTimeMs = (int)connectionTime.TotalMilliseconds,
                    ErrorMessage = $"Connection failed: {ex.Message}"
                };
            }

            CertificateValidationResult? validationResult = null;

            using var sslStream = _sslStreamFactory.CreateSslStream(
                tcpClient.GetStream(),
                (sender, certificate, chain, sslPolicyErrors) =>
                {
                    validationResult = ValidateServerCertificate(certificate, chain, sslPolicyErrors, customCaCertificate);
                    return validationResult?.IsValid ?? false;
                });

            try
            {
                await sslStream.AuthenticateAsClientAsync(
                    new SslClientAuthenticationOptions
                    {
                        TargetHost = host,
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                    },
                    cts.Token);
            }
            catch (AuthenticationException ex)
            {
                var connectionTime = Stopwatch.GetElapsedTime(timestamp.Value);
                var errorMessage = $"TLS authentication failed: {ex.Message}";

                // Include certificate details if we have them
                if (validationResult != null)
                {
                    var certDetails = new List<string>();
                    AddCertificateDetailsToList(certDetails, validationResult);

                    if (certDetails.Count > 0)
                    {
                        errorMessage += $" | {string.Join(" | ", certDetails)}";
                    }
                }

                return new CheckExecutionResult
                {
                    Status = CheckStatuses.Down,
                    ResponseTimeMs = (int)connectionTime.TotalMilliseconds,
                    ErrorMessage = errorMessage
                };
            }

            var elapsedTime = Stopwatch.GetElapsedTime(timestamp.Value);

            if (validationResult == null)
            {
                return new CheckExecutionResult
                {
                    Status = CheckStatuses.Down,
                    ResponseTimeMs = (int)elapsedTime.TotalMilliseconds,
                    ErrorMessage = "No certificate received from server"
                };
            }

            if (validationResult.Errors != SslPolicyErrors.None)
            {
                var errorMessage = BuildDetailedErrorMessage(validationResult);
                return new CheckExecutionResult
                {
                    Status = CheckStatuses.Down,
                    ResponseTimeMs = (int)elapsedTime.TotalMilliseconds,
                    ErrorMessage = errorMessage
                };
            }

            var now = DateTimeOffset.UtcNow;
            var notBefore = validationResult.NotBefore;
            var notAfter = validationResult.NotAfter;

            if (now < notBefore)
            {
                return new CheckExecutionResult
                {
                    Status = CheckStatuses.Down,
                    ResponseTimeMs = (int)elapsedTime.TotalMilliseconds,
                    ErrorMessage = $"Certificate not yet valid (valid from {notBefore:yyyy-MM-dd})"
                };
            }

            if (now > notAfter)
            {
                return new CheckExecutionResult
                {
                    Status = CheckStatuses.Down,
                    ResponseTimeMs = (int)elapsedTime.TotalMilliseconds,
                    ErrorMessage = $"Certificate expired on {notAfter:yyyy-MM-dd}"
                };
            }

            var daysUntilExpiry = (int)(notAfter - now).TotalDays;

            if (daysUntilExpiry <= daysBeforeExpiryWarning)
            {
                return new CheckExecutionResult
                {
                    Status = CheckStatuses.Warn,
                    ResponseTimeMs = (int)elapsedTime.TotalMilliseconds,
                    ErrorMessage = $"Certificate expires in {daysUntilExpiry} day(s) on {notAfter:yyyy-MM-dd}"
                };
            }

            return new CheckExecutionResult
            {
                Status = CheckStatuses.Up,
                ResponseTimeMs = (int)elapsedTime.TotalMilliseconds
            };
        }
        catch (OperationCanceledException)
        {
            return new CheckExecutionResult
            {
                Status = CheckStatuses.Down,
                ResponseTimeMs = timestamp.HasValue ? (int)Stopwatch.GetElapsedTime(timestamp.Value).TotalMilliseconds : null,
                ErrorMessage = "Connection timeout"
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

    private static string BuildSslErrorMessage(SslPolicyErrors errors)
    {
        var messages = new List<string>();

        if ((errors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0)
        {
            messages.Add("Certificate not available");
        }

        if ((errors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
        {
            messages.Add("Certificate name mismatch");
        }

        if ((errors & SslPolicyErrors.RemoteCertificateChainErrors) != 0)
        {
            messages.Add("Certificate chain errors");
        }

        return string.Join(", ", messages);
    }

    private static string BuildDetailedErrorMessage(CertificateValidationResult validationResult)
    {
        var messages = new List<string>();

        messages.Add($"Certificate validation failed: {BuildSslErrorMessage(validationResult.Errors)}");

        AddCertificateDetailsToList(messages, validationResult);

        return string.Join(" | ", messages);
    }

    private static void AddCertificateDetailsToList(List<string> messages, CertificateValidationResult validationResult)
    {
        if (!string.IsNullOrEmpty(validationResult.Subject))
        {
            messages.Add($"Subject: {validationResult.Subject}");
        }

        if (!string.IsNullOrEmpty(validationResult.Issuer))
        {
            messages.Add($"Issuer: {validationResult.Issuer}");
        }

        if (!string.IsNullOrEmpty(validationResult.Thumbprint))
        {
            messages.Add($"Thumbprint: {validationResult.Thumbprint}");
        }

        if (validationResult.ChainStatusInfo != null && validationResult.ChainStatusInfo.Count > 0)
        {
            messages.Add($"Chain status: {string.Join("; ", validationResult.ChainStatusInfo)}");
        }
    }

    private CertificateValidationResult? ValidateServerCertificate(
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors policyErrors,
        string? customCaCertificatePem)
    {
        using var cert = certificate != null ? new X509Certificate2(certificate) : null;

        if (cert == null)
        {
            return null;
        }

        bool isValid = (policyErrors == SslPolicyErrors.None);
        var effectivePolicyErrors = policyErrors;
        var chainStatusInfo = new List<string>();

        // Capture certificate chain status information
        if (chain != null && chain.ChainStatus.Length > 0)
        {
            foreach (var status in chain.ChainStatus)
            {
                if (status.Status != X509ChainStatusFlags.NoError)
                {
                    chainStatusInfo.Add($"{status.Status}: {status.StatusInformation}");
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(customCaCertificatePem))
        {
            isValid = _customTlsValidator.ValidateWithCustomCa(certificate, chain, customCaCertificatePem);

            if (isValid)
            {
                effectivePolicyErrors &= ~SslPolicyErrors.RemoteCertificateChainErrors;
            }
        }

        return new CertificateValidationResult(
            new DateTimeOffset(cert.NotBefore.ToUniversalTime(), TimeSpan.Zero),
            new DateTimeOffset(cert.NotAfter.ToUniversalTime(), TimeSpan.Zero),
            effectivePolicyErrors,
            isValid,
            cert.Subject,
            cert.Issuer,
            cert.Thumbprint,
            chainStatusInfo.Count > 0 ? chainStatusInfo : null);
    }
}
