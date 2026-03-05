using Microsoft.Playwright;

using static Microsoft.Playwright.Assertions;

namespace SAMA.Tests.System;

[TestClass]
[SystemTestCondition]
public class HttpsCheckTests : SystemTestBase
{
    [TestMethod]
    public async Task ShouldCreateHttpsCheckWithCronSchedule()
    {
        await SetupInitialAdminAsync();
        await LoginAsync();
        await Page.WaitForURLAsync($"{BaseUrl}/Dashboard**");

        // Create a workspace
        await Page.GotoAsync($"{BaseUrl}/Workspaces/Create");
        await Page.FillAsync("input[name='Input.Name']", "HTTPS Check Test");
        await Page.Locator("button[type='submit']:has-text('Create')").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Enter the workspace
        await Page.Locator("a:has-text('HTTPS Check Test')").ClickAsync();
        await Page.WaitForURLAsync($"{BaseUrl}/Dashboard**");

        // Navigate to Checks and create a new check
        await Page.Locator("a:has-text('Checks')").ClickAsync();
        await Page.Locator("a.btn:has-text('Create Check')").ClickAsync();
        await Page.WaitForURLAsync($"{BaseUrl}/Checks/Create**");

        // Fill in check name and description
        await Page.FillAsync("input[name='Input.Name']", "Example HTTPS");
        await Page.FillAsync("textarea[name='Input.Description']", "HTTPS check for example.com");

        // Select HTTP check type
        await Page.SelectOptionAsync("select[name='Input.CheckType']", "CheckType_HTTP");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Fill in HTTP configuration
        await Page.FillAsync("input[name='Input.HttpUrl']", "https://example.com");
        await Page.FillAsync("input[name='Input.HttpExpectedStatusCodes']", "200");

        // Set cron schedule: every 5 seconds
        await Page.FillAsync("#schedule-input", "*/5 * * * * ?");

        // Wait for the schedule preview to update
        await Page.WaitForTimeoutAsync(500);
        await Expect(Page.Locator("#schedule-preview")).ToContainTextAsync("Every 5 seconds");

        // Submit the form
        await Page.Locator("button[type='submit']:has-text('Create Check')").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Should redirect to the Checks index page
        await Page.WaitForURLAsync($"{BaseUrl}/Checks**");

        // Verify the check appears in the list and navigate to its details
        await Expect(Page.Locator("a:has-text('Example HTTPS')")).ToBeVisibleAsync();
        await Page.Locator("a:has-text('Example HTTPS')").ClickAsync();

        // Verify check details
        await Expect(Page.Locator("h2")).ToContainTextAsync("Example HTTPS");
        await Expect(Page.Locator("text=HTTPS check for example.com")).ToBeVisibleAsync();
        await Expect(Page.Locator(".badge:has-text('HTTP')")).ToBeVisibleAsync();
        await Expect(Page.Locator("code:has-text('https://example.com')")).ToBeVisibleAsync();
        await Expect(Page.Locator("code:has-text('*/5 * * * * ?')")).ToBeVisibleAsync();
        await Expect(Page.Locator("dl .badge:has-text('Enabled')")).ToBeVisibleAsync();
    }
}
