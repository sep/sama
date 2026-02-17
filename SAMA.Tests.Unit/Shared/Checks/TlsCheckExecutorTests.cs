using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SAMA.Shared.Checks;
using SAMA.Shared.Constants;
using SAMA.Shared.Wrappers;

namespace SAMA.Tests.Unit.Shared.Checks;

[TestClass]
public class TlsCheckExecutorTests
{
    private TcpClientFactory _mockTcpFactory = null!;
    private TcpClientWrapper _mockTcpClientWrapper = null!;
    private SslStreamFactory _mockSslFactory = null!;
    private SslStreamWrapper _mockSslStreamWrapper = null!;
    private CustomTlsValidator _mockTlsValidator = null!;
    private TlsCheckExecutor _executor = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockTcpFactory = Substitute.For<TcpClientFactory>();
        _mockTcpClientWrapper = Substitute.For<TcpClientWrapper>();
        _mockTcpFactory.CreateClient().Returns(_mockTcpClientWrapper);

        _mockSslFactory = Substitute.For<SslStreamFactory>();
        _mockSslStreamWrapper = Substitute.For<SslStreamWrapper>(null, null);
        _mockSslFactory.CreateSslStream(Arg.Any<Stream>(), Arg.Any<RemoteCertificateValidationCallback>())
            .Returns(_mockSslStreamWrapper);

        _mockTlsValidator = Substitute.For<CustomTlsValidator>();

        _executor = new TlsCheckExecutor(_mockTcpFactory, _mockSslFactory, _mockTlsValidator);
    }

    [TestCleanup]
    public void Teardown()
    {
        _mockSslStreamWrapper?.Dispose();
        _mockTcpClientWrapper?.Dispose();
        _mockTcpClientWrapper = null!;
        _mockSslStreamWrapper = null!;
        _mockTlsValidator = null!;
        _executor = null!;
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnDownWhenUrlNotConfigured()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(30)
        };

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.AreEqual("URI not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnDownWhenUrlIsInvalid()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.Url] = JsonSerializer.SerializeToElement("not-a-url"),
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(30)
        };

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.AreEqual("URI must be a valid absolute URI", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnDownWhenDaysBeforeExpiryWarningIsZero()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.Url] = JsonSerializer.SerializeToElement("https://example.com"),
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(0)
        };

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.AreEqual("Days before expiry warning must be greater than 0", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldHandleConnectionFailure()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.Url] = JsonSerializer.SerializeToElement("https://localhost:9999"),
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(30),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(5)
        };

        _mockTcpClientWrapper.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new SocketException());

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("Connection failed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldHandleTimeout()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.Url] = JsonSerializer.SerializeToElement("https://example.com:443"),
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(30),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(1)
        };

        _mockTcpClientWrapper.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(2);
                try
                {
                    await Task.Delay(10000, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            });

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.AreEqual("Connection timeout", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldHandleCancellation()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.Url] = JsonSerializer.SerializeToElement("https://example.com"),
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(30),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(30)
        };

        using var cts = new CancellationTokenSource();

        _mockTcpClientWrapper.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(2);
                try
                {
                    await Task.Delay(10000, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            });

        cts.CancelAfter(TimeSpan.FromMilliseconds(10));

        var result = await _executor.ExecuteAsync(config, cts.Token);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.AreEqual("Connection timeout", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnUpForValidCertificate()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.Url] = JsonSerializer.SerializeToElement("https://example.com"),
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(30),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        _mockTcpClientWrapper.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        RemoteCertificateValidationCallback? capturedCallback = null;
        _mockSslFactory.CreateSslStream(Arg.Any<Stream>(), Arg.Any<RemoteCertificateValidationCallback>())
            .Returns(callInfo =>
            {
                capturedCallback = callInfo.ArgAt<RemoteCertificateValidationCallback>(1);
                return _mockSslStreamWrapper;
            });

        _mockSslStreamWrapper.AuthenticateAsClientAsync(Arg.Any<SslClientAuthenticationOptions>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var validCert = CreateTestCertificate(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(365));
                capturedCallback?.Invoke(null!, validCert, null!, SslPolicyErrors.None);
                await Task.Delay(10);
            });

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Up, result.Status);
        Assert.IsNotNull(result.ResponseTimeMs);
        Assert.IsGreaterThanOrEqualTo(0, result.ResponseTimeMs.Value);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnDownForExpiredCertificate()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.Url] = JsonSerializer.SerializeToElement("https://expired.example.com"),
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(30),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        _mockTcpClientWrapper.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        RemoteCertificateValidationCallback? capturedCallback = null;
        _mockSslFactory.CreateSslStream(Arg.Any<Stream>(), Arg.Any<RemoteCertificateValidationCallback>())
            .Returns(callInfo =>
            {
                capturedCallback = callInfo.ArgAt<RemoteCertificateValidationCallback>(1);
                return _mockSslStreamWrapper;
            });

        _mockSslStreamWrapper.AuthenticateAsClientAsync(Arg.Any<SslClientAuthenticationOptions>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var expiredCert = CreateTestCertificate(DateTime.UtcNow.AddDays(-400), DateTime.UtcNow.AddDays(-10));
                capturedCallback?.Invoke(null!, expiredCert, null!, SslPolicyErrors.None);
                await Task.Delay(10);
            });

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.IsNotNull(result.ResponseTimeMs);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("expired", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnWarnForCertificateNearExpiry()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.Url] = JsonSerializer.SerializeToElement("https://example.com"),
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(30),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        _mockTcpClientWrapper.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        RemoteCertificateValidationCallback? capturedCallback = null;
        _mockSslFactory.CreateSslStream(Arg.Any<Stream>(), Arg.Any<RemoteCertificateValidationCallback>())
            .Returns(callInfo =>
            {
                capturedCallback = callInfo.ArgAt<RemoteCertificateValidationCallback>(1);
                return _mockSslStreamWrapper;
            });

        _mockSslStreamWrapper.AuthenticateAsClientAsync(Arg.Any<SslClientAuthenticationOptions>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                // Certificate expires in 15 days (less than warning threshold of 30)
                var nearExpiryCert = CreateTestCertificate(DateTime.UtcNow.AddDays(-350), DateTime.UtcNow.AddDays(15));
                capturedCallback?.Invoke(null!, nearExpiryCert, null!, SslPolicyErrors.None);
                await Task.Delay(10);
            });

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Warn, result.Status);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("expires in", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnDownForSelfSignedCertificateWithoutCustomCa()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.Url] = JsonSerializer.SerializeToElement("https://self-signed.example.com"),
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(30),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        _mockTcpClientWrapper.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        RemoteCertificateValidationCallback? capturedCallback = null;
        _mockSslFactory.CreateSslStream(Arg.Any<Stream>(), Arg.Any<RemoteCertificateValidationCallback>())
            .Returns(callInfo =>
            {
                capturedCallback = callInfo.ArgAt<RemoteCertificateValidationCallback>(1);
                return _mockSslStreamWrapper;
            });

        _mockSslStreamWrapper.AuthenticateAsClientAsync(Arg.Any<SslClientAuthenticationOptions>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var validCert = CreateTestCertificate(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(365));

                // Self-signed will have chain errors
                capturedCallback?.Invoke(null!, validCert, null!, SslPolicyErrors.RemoteCertificateChainErrors);
                await Task.Delay(10);
            });

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("validation failed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnUpForSelfSignedCertificateWithValidCustomCa()
    {
        var customCaPem = "-----BEGIN CERTIFICATE-----\nMIIC...TestCA...\n-----END CERTIFICATE-----";

        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.Url] = JsonSerializer.SerializeToElement("https://internal.example.com"),
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(30),
            [ConfigurationKeys.TlsCheck.CustomCaCertificate] = JsonSerializer.SerializeToElement(customCaPem),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        _mockTcpClientWrapper.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _mockTlsValidator.ValidateWithCustomCa(Arg.Any<X509Certificate>(), Arg.Any<X509Chain>(), customCaPem)
            .Returns(true);

        RemoteCertificateValidationCallback? capturedCallback = null;
        _mockSslFactory.CreateSslStream(Arg.Any<Stream>(), Arg.Any<RemoteCertificateValidationCallback>())
            .Returns(callInfo =>
            {
                capturedCallback = callInfo.ArgAt<RemoteCertificateValidationCallback>(1);
                return _mockSslStreamWrapper;
            });

        _mockSslStreamWrapper.AuthenticateAsClientAsync(Arg.Any<SslClientAuthenticationOptions>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var validCert = CreateTestCertificate(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(365));
                var chain = new X509Chain();

                capturedCallback?.Invoke(null!, validCert, chain, SslPolicyErrors.RemoteCertificateChainErrors);
                await Task.Delay(10);
            });

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Up, result.Status);
        Assert.IsNull(result.ErrorMessage);
        _mockTlsValidator.Received(1).ValidateWithCustomCa(Arg.Any<X509Certificate>(), Arg.Any<X509Chain>(), customCaPem);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnDownForSelfSignedCertificateWithFailedCustomCaValidation()
    {
        var customCaPem = "-----BEGIN CERTIFICATE-----\nMIIC...TestCA...\n-----END CERTIFICATE-----";

        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.Url] = JsonSerializer.SerializeToElement("https://internal.example.com"),
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(30),
            [ConfigurationKeys.TlsCheck.CustomCaCertificate] = JsonSerializer.SerializeToElement(customCaPem),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        _mockTcpClientWrapper.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _mockTlsValidator.ValidateWithCustomCa(Arg.Any<X509Certificate>(), Arg.Any<X509Chain>(), customCaPem)
            .Returns(false);

        RemoteCertificateValidationCallback? capturedCallback = null;
        _mockSslFactory.CreateSslStream(Arg.Any<Stream>(), Arg.Any<RemoteCertificateValidationCallback>())
            .Returns(callInfo =>
            {
                capturedCallback = callInfo.ArgAt<RemoteCertificateValidationCallback>(1);
                return _mockSslStreamWrapper;
            });

        _mockSslStreamWrapper.AuthenticateAsClientAsync(Arg.Any<SslClientAuthenticationOptions>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var validCert = CreateTestCertificate(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(365));
                var chain = new X509Chain();

                capturedCallback?.Invoke(null!, validCert, chain, SslPolicyErrors.RemoteCertificateChainErrors);
                await Task.Delay(10);
            });

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("validation failed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _mockTlsValidator.Received(1).ValidateWithCustomCa(Arg.Any<X509Certificate>(), Arg.Any<X509Chain>(), customCaPem);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnUpForCustomCaValidationWithNoOtherErrors()
    {
        var customCaPem = "-----BEGIN CERTIFICATE-----\nMIIC...TestCA...\n-----END CERTIFICATE-----";

        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.Url] = JsonSerializer.SerializeToElement("https://internal.example.com"),
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(30),
            [ConfigurationKeys.TlsCheck.CustomCaCertificate] = JsonSerializer.SerializeToElement(customCaPem),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        _mockTcpClientWrapper.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _mockTlsValidator.ValidateWithCustomCa(Arg.Any<X509Certificate>(), Arg.Any<X509Chain>(), customCaPem)
            .Returns(true);

        RemoteCertificateValidationCallback? capturedCallback = null;
        _mockSslFactory.CreateSslStream(Arg.Any<Stream>(), Arg.Any<RemoteCertificateValidationCallback>())
            .Returns(callInfo =>
            {
                capturedCallback = callInfo.ArgAt<RemoteCertificateValidationCallback>(1);
                return _mockSslStreamWrapper;
            });

        _mockSslStreamWrapper.AuthenticateAsClientAsync(Arg.Any<SslClientAuthenticationOptions>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var validCert = CreateTestCertificate(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(365));
                var chain = new X509Chain();

                capturedCallback?.Invoke(null!, validCert, chain, SslPolicyErrors.None);
                await Task.Delay(10);
            });

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Up, result.Status);
        Assert.IsNull(result.ErrorMessage);
        _mockTlsValidator.Received(1).ValidateWithCustomCa(Arg.Any<X509Certificate>(), Arg.Any<X509Chain>(), customCaPem);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnDownWhenCustomCaValidationSucceedsButNameMismatch()
    {
        var customCaPem = "-----BEGIN CERTIFICATE-----\nMIIC...TestCA...\n-----END CERTIFICATE-----";

        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.Url] = JsonSerializer.SerializeToElement("https://wrong-host.example.com"),
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(30),
            [ConfigurationKeys.TlsCheck.CustomCaCertificate] = JsonSerializer.SerializeToElement(customCaPem),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        _mockTcpClientWrapper.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _mockTlsValidator.ValidateWithCustomCa(Arg.Any<X509Certificate>(), Arg.Any<X509Chain>(), customCaPem)
            .Returns(true);

        RemoteCertificateValidationCallback? capturedCallback = null;
        _mockSslFactory.CreateSslStream(Arg.Any<Stream>(), Arg.Any<RemoteCertificateValidationCallback>())
            .Returns(callInfo =>
            {
                capturedCallback = callInfo.ArgAt<RemoteCertificateValidationCallback>(1);
                return _mockSslStreamWrapper;
            });

        _mockSslStreamWrapper.AuthenticateAsClientAsync(Arg.Any<SslClientAuthenticationOptions>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var validCert = CreateTestCertificate(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(365));
                var chain = new X509Chain();

                var errors = SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors;
                capturedCallback?.Invoke(null!, validCert, chain, errors);
                await Task.Delay(10);
            });

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("validation failed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name mismatch", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldNotCallCustomCaValidatorWhenNoCustomCaProvided()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.Url] = JsonSerializer.SerializeToElement("https://example.com"),
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(30),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        _mockTcpClientWrapper.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        RemoteCertificateValidationCallback? capturedCallback = null;
        _mockSslFactory.CreateSslStream(Arg.Any<Stream>(), Arg.Any<RemoteCertificateValidationCallback>())
            .Returns(callInfo =>
            {
                capturedCallback = callInfo.ArgAt<RemoteCertificateValidationCallback>(1);
                return _mockSslStreamWrapper;
            });

        _mockSslStreamWrapper.AuthenticateAsClientAsync(Arg.Any<SslClientAuthenticationOptions>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var validCert = CreateTestCertificate(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(365));
                capturedCallback?.Invoke(null!, validCert, null!, SslPolicyErrors.None);
                await Task.Delay(10);
            });

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Up, result.Status);
        _mockTlsValidator.DidNotReceive().ValidateWithCustomCa(Arg.Any<X509Certificate>(), Arg.Any<X509Chain>(), Arg.Any<string>());
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldIncludeCertificateDetailsInErrorMessage()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.Url] = JsonSerializer.SerializeToElement("https://test.example.com"),
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(30),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        _mockTcpClientWrapper.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        RemoteCertificateValidationCallback? capturedCallback = null;
        _mockSslFactory.CreateSslStream(Arg.Any<Stream>(), Arg.Any<RemoteCertificateValidationCallback>())
            .Returns(callInfo =>
            {
                capturedCallback = callInfo.ArgAt<RemoteCertificateValidationCallback>(1);
                return _mockSslStreamWrapper;
            });

        _mockSslStreamWrapper.AuthenticateAsClientAsync(Arg.Any<SslClientAuthenticationOptions>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var testCert = CreateTestCertificate(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(365));

                // Create a mock chain to simulate chain errors
                var chain = new X509Chain();

                capturedCallback?.Invoke(null!, testCert, chain, SslPolicyErrors.RemoteCertificateChainErrors);
                await Task.Delay(10);
            });

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.IsNotNull(result.ErrorMessage);

        // Verify error message contains detailed information
        Assert.Contains("Certificate validation failed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Subject:", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Issuer:", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Thumbprint:", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnDownForCertificateNotYetValid()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.Url] = JsonSerializer.SerializeToElement("https://future.example.com"),
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(30),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        _mockTcpClientWrapper.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        RemoteCertificateValidationCallback? capturedCallback = null;
        _mockSslFactory.CreateSslStream(Arg.Any<Stream>(), Arg.Any<RemoteCertificateValidationCallback>())
            .Returns(callInfo =>
            {
                capturedCallback = callInfo.ArgAt<RemoteCertificateValidationCallback>(1);
                return _mockSslStreamWrapper;
            });

        _mockSslStreamWrapper.AuthenticateAsClientAsync(Arg.Any<SslClientAuthenticationOptions>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var futureCert = CreateTestCertificate(DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(375));
                capturedCallback?.Invoke(null!, futureCert, null!, SslPolicyErrors.None);
                await Task.Delay(10);
            });

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("not yet valid", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnDownWhenNoCertificateReceived()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.TlsCheck.Url] = JsonSerializer.SerializeToElement("https://example.com"),
            [ConfigurationKeys.TlsCheck.DaysBeforeExpiryWarning] = JsonSerializer.SerializeToElement(30),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        _mockTcpClientWrapper.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        RemoteCertificateValidationCallback? capturedCallback = null;
        _mockSslFactory.CreateSslStream(Arg.Any<Stream>(), Arg.Any<RemoteCertificateValidationCallback>())
            .Returns(callInfo =>
            {
                capturedCallback = callInfo.ArgAt<RemoteCertificateValidationCallback>(1);
                return _mockSslStreamWrapper;
            });

        _mockSslStreamWrapper.AuthenticateAsClientAsync(Arg.Any<SslClientAuthenticationOptions>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                await Task.Delay(10);
            });

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("No certificate received", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static X509Certificate2 CreateTestCertificate(DateTime notBefore, DateTime notAfter)
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=example.com",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        return request.CreateSelfSigned(notBefore, notAfter);
    }
}
