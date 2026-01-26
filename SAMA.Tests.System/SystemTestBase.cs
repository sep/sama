using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Playwright;

namespace SAMA.Tests.System;

/// <summary>
/// Base class for system tests using Playwright.
/// Each test class gets its own app instance and database schema for parallel execution.
/// When debugging, runs with a visible browser and slower actions for easier inspection.
/// </summary>
public abstract class SystemTestBase
{
    private static readonly ConcurrentDictionary<Type, TestClassContext> _contexts = new();

    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private TestClassContext? _context;

    protected static bool IsDebugging => Debugger.IsAttached;

    protected IPage Page { get; private set; } = null!;

    protected string BaseUrl => _context?.BaseUrl ?? throw new InvalidOperationException("Test not initialized.");

    protected string TestRunId => _context?.TestRunId ?? throw new InvalidOperationException("Test not initialized.");

    internal static void Cleanup()
    {
        foreach (var context in _contexts.Values)
        {
            context.Dispose();
        }

        _contexts.Clear();
    }

    [TestInitialize]
    public virtual async Task TestInitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !IsDebugging,
            SlowMo = IsDebugging ? 250 : null,
        });
        _context = await GetOrCreateContextAsync();
        Page = await _browser.NewPageAsync(new BrowserNewPageOptions
        {
            IgnoreHTTPSErrors = true,
        });

        // Use longer timeout when debugging to allow time for inspection
        Page.SetDefaultTimeout(IsDebugging ? 30_000 : 10_000);
    }

    [TestCleanup]
    public virtual async Task TestCleanupAsync()
    {
        await Page.CloseAsync();
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }

    protected async Task LoginAsync(string email = "admin@example.com", string password = "TestPassword123!")
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.FillAsync("input[name='Input.Email']", email);
        await Page.FillAsync("input[name='Input.Password']", password);
        await Page.ClickAsync("button[type='submit']");
        await Page.WaitForURLAsync($"{BaseUrl}/**");
    }

    protected async Task SetupInitialAdminAsync(string email = "admin@example.com", string password = "TestPassword123!")
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForURLAsync($"{BaseUrl}/Setup");

        await Page.FillAsync("input[name='Input.Email']", email);
        await Page.FillAsync("input[name='Input.Password']", password);
        await Page.FillAsync("input[name='Input.ConfirmPassword']", password);
        await Page.ClickAsync("button[type='submit']");

        await Page.WaitForURLAsync(BaseUrl);
    }

    protected async Task LogoutAsync()
    {
        await Page.Locator("#userDropdown").ClickAsync();
        await Page.Locator("button:has-text('Logout')").ClickAsync();
        await Page.WaitForURLAsync(BaseUrl);
    }

    protected async Task ImportSystemTestConfigurationAsync()
    {
        var resourcesPath = Path.Combine(AppContext.BaseDirectory, "Resources", "sama-export-system-test.json");

        await Page.GotoAsync($"{BaseUrl}/Admin/Settings");
        await Page.Locator("input[name='ImportInput.File']").SetInputFilesAsync(resourcesPath);
        await Page.FillAsync("input[name='ImportInput.Password']", "system-test-password");
        await Page.Locator("form[action*='Import'] button[type='submit']").ClickAsync();
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
    }

    private async Task<TestClassContext> GetOrCreateContextAsync()
    {
        var testClassType = GetType();

        if (_contexts.TryGetValue(testClassType, out var existing))
        {
            return existing;
        }

        var context = await TestClassContext.CreateAsync();

        if (!_contexts.TryAdd(testClassType, context))
        {
            // Another thread created it first, dispose ours and use theirs
            context.Dispose();
            return _contexts[testClassType];
        }

        return context;
    }
}
