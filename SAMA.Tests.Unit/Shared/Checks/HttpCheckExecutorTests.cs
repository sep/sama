using System.Net;
using System.Text.Json;
using NSubstitute;
using SAMA.Shared.Checks;
using SAMA.Shared.Constants;
using SAMA.Tests.Unit.TestUtilities;

namespace SAMA.Tests.Unit.Shared.Checks;

[TestClass]
public class HttpCheckExecutorTests
{
    [TestMethod]
    public async Task ExecuteAsyncShouldReturnUpStatusWithValidUrl()
    {
        using var mockHandler = HttpTestHelpers.CreateMockHandlerWithDelay(HttpStatusCode.OK, "Success", TimeSpan.FromMilliseconds(10));
        var executor = new HttpCheckExecutor(HttpTestHelpers.CreateConfigurableHttpClientFactory(mockHandler));
        var configuration = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com"),
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement(CheckDefaults.HttpMethod),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200 })
        };

        var result = await executor.ExecuteAsync(configuration);

        Assert.AreEqual(CheckStatuses.Up, result.Status);
        Assert.IsGreaterThan(0, result.ResponseTimeMs!.Value);
        Assert.AreEqual(200, result.StatusCode);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnDownStatusWithInvalidUrl()
    {
        using var handler = new HttpClientHandler();
        var executor = new HttpCheckExecutor(HttpTestHelpers.CreateConfigurableHttpClientFactory(handler));
        var configuration = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://this-does-not-exist-12345.example.nonexistent-tld"),
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement(CheckDefaults.HttpMethod),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(5)
        };

        var result = await executor.ExecuteAsync(configuration);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("HTTP request failed", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnDownStatusWithUnexpectedStatusCode()
    {
        using var mockHandler = HttpTestHelpers.CreateMockHandler(HttpStatusCode.NotFound, "Not Found");
        var executor = new HttpCheckExecutor(HttpTestHelpers.CreateConfigurableHttpClientFactory(mockHandler));
        var configuration = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com/notfound"),
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement(CheckDefaults.HttpMethod),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200 })
        };

        var result = await executor.ExecuteAsync(configuration);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.AreEqual(404, result.StatusCode);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("Unexpected status code", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnDownStatusWithMissingUrl()
    {
        using var mockHandler = HttpTestHelpers.CreateMockHandler(HttpStatusCode.OK, "Success");
        var executor = new HttpCheckExecutor(HttpTestHelpers.CreateConfigurableHttpClientFactory(mockHandler));
        var configuration = new Dictionary<string, JsonElement>();

        var result = await executor.ExecuteAsync(configuration);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.AreEqual("URL not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldSendCustomHeadersInRequest()
    {
        using var mockHandler = HttpTestHelpers.CreateMockHandler(HttpStatusCode.OK, "Success");
        var executor = new HttpCheckExecutor(HttpTestHelpers.CreateConfigurableHttpClientFactory(mockHandler));
        var configuration = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com/api"),
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement("GET"),
            [ConfigurationKeys.HttpCheck.Headers] = JsonSerializer.SerializeToElement("X-Custom-Header: CustomValue\r\nAuthorization: Bearer token123\r\n"),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200 })
        };

        var result = await executor.ExecuteAsync(configuration);

        Assert.AreEqual(CheckStatuses.Up, result.Status);
        Assert.IsNotNull(mockHandler.RequestReceived);
        Assert.IsTrue(mockHandler.RequestReceived.Headers.Contains("X-Custom-Header"));
        Assert.AreEqual("CustomValue", string.Join(',', mockHandler.RequestReceived.Headers.GetValues("X-Custom-Header")));
        Assert.IsTrue(mockHandler.RequestReceived.Headers.Contains("Authorization"));
        Assert.AreEqual("Bearer token123", string.Join(',', mockHandler.RequestReceived.Headers.GetValues("Authorization")));
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldSendBodyInPostRequest()
    {
        using var mockHandler = HttpTestHelpers.CreateMockHandler(HttpStatusCode.Created, "Created");
        var executor = new HttpCheckExecutor(HttpTestHelpers.CreateConfigurableHttpClientFactory(mockHandler));
        var configuration = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com/api"),
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement("POST"),
            [ConfigurationKeys.HttpCheck.Body] = JsonSerializer.SerializeToElement("{\"key\":\"value\"}"),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 201 })
        };

        var result = await executor.ExecuteAsync(configuration);

        Assert.AreEqual(CheckStatuses.Up, result.Status);
        Assert.AreEqual(201, result.StatusCode);
        Assert.IsNotNull(mockHandler.RequestReceived);
        Assert.AreEqual(HttpMethod.Post, mockHandler.RequestReceived.Method);
        Assert.AreEqual("{\"key\":\"value\"}", mockHandler.RequestContent);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldNotSetContentTypeAutomaticallyWhenBodyIsProvided()
    {
        using var mockHandler = HttpTestHelpers.CreateMockHandler(HttpStatusCode.OK, "Success");
        var executor = new HttpCheckExecutor(HttpTestHelpers.CreateConfigurableHttpClientFactory(mockHandler));
        var configuration = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com/api"),
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement("POST"),
            [ConfigurationKeys.HttpCheck.Body] = JsonSerializer.SerializeToElement("{\"key\":\"value\"}"),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200 })
        };

        var result = await executor.ExecuteAsync(configuration);

        Assert.AreEqual(CheckStatuses.Up, result.Status);
        Assert.IsNotNull(mockHandler.RequestReceived);
        Assert.IsNotNull(mockHandler.RequestReceived.Content);
        Assert.IsNull(mockHandler.RequestReceived.Content.Headers.ContentType);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldSetContentTypeWhenProvidedViaCustomHeaders()
    {
        using var mockHandler = HttpTestHelpers.CreateMockHandler(HttpStatusCode.OK, "Success");
        var executor = new HttpCheckExecutor(HttpTestHelpers.CreateConfigurableHttpClientFactory(mockHandler));
        var configuration = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com/api"),
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement("POST"),
            [ConfigurationKeys.HttpCheck.Headers] = JsonSerializer.SerializeToElement("Content-Type: application/json"),
            [ConfigurationKeys.HttpCheck.Body] = JsonSerializer.SerializeToElement("{\"key\":\"value\"}"),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200 })
        };

        var result = await executor.ExecuteAsync(configuration);

        Assert.AreEqual(CheckStatuses.Up, result.Status);
        Assert.IsNotNull(mockHandler.RequestReceived);
        Assert.IsNotNull(mockHandler.RequestReceived.Content);
        Assert.IsNotNull(mockHandler.RequestReceived.Content.Headers.ContentType);
        Assert.AreEqual("application/json", mockHandler.RequestReceived.Content.Headers.ContentType.MediaType);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnUpWhenContentValidationMatches()
    {
        using var mockHandler = HttpTestHelpers.CreateMockHandler(HttpStatusCode.OK, "The response contains success keyword here");
        var executor = new HttpCheckExecutor(HttpTestHelpers.CreateConfigurableHttpClientFactory(mockHandler));
        var configuration = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com/api"),
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement("GET"),
            [ConfigurationKeys.HttpCheck.ContentValidation] = JsonSerializer.SerializeToElement("success"),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200 })
        };

        var result = await executor.ExecuteAsync(configuration);

        Assert.AreEqual(CheckStatuses.Up, result.Status);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnDownWhenContentValidationFails()
    {
        using var mockHandler = HttpTestHelpers.CreateMockHandler(HttpStatusCode.OK, "The response does not contain the expected keyword");
        var executor = new HttpCheckExecutor(HttpTestHelpers.CreateConfigurableHttpClientFactory(mockHandler));
        var configuration = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com/api"),
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement("GET"),
            [ConfigurationKeys.HttpCheck.ContentValidation] = JsonSerializer.SerializeToElement("success"),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200 })
        };

        var result = await executor.ExecuteAsync(configuration);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("Content validation failed", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnWarnWhenResponseTimeThresholdExceeded()
    {
        using var mockHandler = HttpTestHelpers.CreateMockHandlerWithDelay(HttpStatusCode.OK, "Success", TimeSpan.FromMilliseconds(150));
        var executor = new HttpCheckExecutor(HttpTestHelpers.CreateConfigurableHttpClientFactory(mockHandler));
        var configuration = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com/api"),
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement("GET"),
            [ConfigurationKeys.HttpCheck.ResponseTimeWarnThresholdMs] = JsonSerializer.SerializeToElement(100),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200 })
        };

        var result = await executor.ExecuteAsync(configuration);

        Assert.AreEqual(CheckStatuses.Warn, result.Status);
        Assert.IsGreaterThan(100, result.ResponseTimeMs!.Value);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnDownWhenResponseTimeThresholdExceededAndExpectedStatusCodeIsNotMet()
    {
        using var mockHandler = HttpTestHelpers.CreateMockHandlerWithDelay(HttpStatusCode.UnsupportedMediaType, "Error", TimeSpan.FromMilliseconds(150));
        var executor = new HttpCheckExecutor(HttpTestHelpers.CreateConfigurableHttpClientFactory(mockHandler));
        var configuration = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com/api"),
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement("GET"),
            [ConfigurationKeys.HttpCheck.ResponseTimeWarnThresholdMs] = JsonSerializer.SerializeToElement(100),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200 })
        };

        var result = await executor.ExecuteAsync(configuration);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.IsGreaterThan(100, result.ResponseTimeMs!.Value);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnUpWhenResponseTimeThresholdNotExceeded()
    {
        using var mockHandler = HttpTestHelpers.CreateMockHandler(HttpStatusCode.OK, "Success");
        var executor = new HttpCheckExecutor(HttpTestHelpers.CreateConfigurableHttpClientFactory(mockHandler));
        var configuration = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com/api"),
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement("GET"),
            [ConfigurationKeys.HttpCheck.ResponseTimeWarnThresholdMs] = JsonSerializer.SerializeToElement(5000),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200 })
        };

        var result = await executor.ExecuteAsync(configuration);

        Assert.AreEqual(CheckStatuses.Up, result.Status);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldAcceptAnyConfiguredStatusCode()
    {
        using var mockHandler = HttpTestHelpers.CreateMockHandler(HttpStatusCode.NotFound, "Not Found");
        var executor = new HttpCheckExecutor(HttpTestHelpers.CreateConfigurableHttpClientFactory(mockHandler));
        var configuration = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com/notfound"),
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement("GET"),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200, 404, 500 })
        };

        var result = await executor.ExecuteAsync(configuration);

        Assert.AreEqual(CheckStatuses.Up, result.Status);
        Assert.AreEqual(404, result.StatusCode);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldConfigureHttpClientToFollowRedirects()
    {
        using var mockHandler = HttpTestHelpers.CreateMockHandler(HttpStatusCode.OK, "Success");
        var factory = HttpTestHelpers.CreateConfigurableHttpClientFactory(mockHandler);
        var executor = new HttpCheckExecutor(factory);
        var configuration = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com/redirect"),
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement("GET"),
            [ConfigurationKeys.HttpCheck.FollowRedirects] = JsonSerializer.SerializeToElement(true),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200 })
        };
        var result = await executor.ExecuteAsync(configuration);
        Assert.AreEqual(CheckStatuses.Up, result.Status);
        factory.Received().CreateClient(Arg.Is(true), Arg.Any<bool>(), Arg.Any<int>());
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldConfigureHttpClientToNotFollowRedirects()
    {
        using var mockHandler = HttpTestHelpers.CreateMockHandler(HttpStatusCode.OK, "Success");
        var factory = HttpTestHelpers.CreateConfigurableHttpClientFactory(mockHandler);
        var executor = new HttpCheckExecutor(factory);
        var configuration = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com/redirect"),
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement("GET"),
            [ConfigurationKeys.HttpCheck.FollowRedirects] = JsonSerializer.SerializeToElement(false),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200 })
        };
        var result = await executor.ExecuteAsync(configuration);
        Assert.AreEqual(CheckStatuses.Up, result.Status);
        factory.Received().CreateClient(Arg.Is(false), Arg.Any<bool>(), Arg.Any<int>());
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldConfigureHttpClientToAllowInvalidSsl()
    {
        using var mockHandler = HttpTestHelpers.CreateMockHandler(HttpStatusCode.OK, "Success");
        var factory = HttpTestHelpers.CreateConfigurableHttpClientFactory(mockHandler);
        var executor = new HttpCheckExecutor(factory);
        var configuration = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com/redirect"),
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement("GET"),
            [ConfigurationKeys.HttpCheck.AllowInvalidSsl] = JsonSerializer.SerializeToElement(true),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200 })
        };
        var result = await executor.ExecuteAsync(configuration);
        Assert.AreEqual(CheckStatuses.Up, result.Status);
        factory.Received().CreateClient(Arg.Any<bool>(), Arg.Is(true), Arg.Any<int>());
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldConfigureHttpClientToNotAllowInvalidSsl()
    {
        using var mockHandler = HttpTestHelpers.CreateMockHandler(HttpStatusCode.OK, "Success");
        var factory = HttpTestHelpers.CreateConfigurableHttpClientFactory(mockHandler);
        var executor = new HttpCheckExecutor(factory);
        var configuration = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com/redirect"),
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement("GET"),
            [ConfigurationKeys.HttpCheck.AllowInvalidSsl] = JsonSerializer.SerializeToElement(false),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200 })
        };
        var result = await executor.ExecuteAsync(configuration);
        Assert.AreEqual(CheckStatuses.Up, result.Status);
        factory.Received().CreateClient(Arg.Any<bool>(), Arg.Is(false), Arg.Any<int>());
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldSetConnectionCloseHeader()
    {
        using var mockHandler = HttpTestHelpers.CreateMockHandler(HttpStatusCode.OK, "Success");
        var executor = new HttpCheckExecutor(HttpTestHelpers.CreateConfigurableHttpClientFactory(mockHandler));
        var configuration = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com"),
            [ConfigurationKeys.HttpCheck.Method] = JsonSerializer.SerializeToElement("GET"),
            [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200 })
        };

        var result = await executor.ExecuteAsync(configuration);

        Assert.AreEqual(CheckStatuses.Up, result.Status);
        Assert.IsNotNull(mockHandler.RequestReceived);
        Assert.IsTrue(mockHandler.RequestReceived.Headers.ConnectionClose);
        Assert.IsTrue(mockHandler.RequestReceived.Headers.Contains("Connection"));
        Assert.AreEqual("close", string.Join(',', mockHandler.RequestReceived.Headers.GetValues("Connection")));
    }
}
