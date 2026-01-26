using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

using static Microsoft.Playwright.Assertions;

namespace SAMA.Tests.System;

[TestClass]
[SystemTestCondition]
public class CheckTests : SystemTestBase
{
    private const string Smtp4DevApiUrl = "http://localhost:6467/api";

    [TestMethod]
    public async Task ShouldSendEmailNotificationsWhenCheckIsEnabledAndFails()
    {
        // Setup: Create initial admin user and login
        await SetupInitialAdminAsync();
        await LoginAsync();
        await Page.WaitForURLAsync($"{BaseUrl}/Dashboard**");

        // Import configuration
        await ImportSystemTestConfigurationAsync();

        // Update the notification channel to use a unique recipient for this test run
        await Page.GotoAsync($"{BaseUrl}/Workspaces");
        await Page.Locator("a:has-text('SysTest')").ClickAsync();
        await Page.Locator("a:has-text('Channels')").ClickAsync();
        await Page.Locator("a:has-text('TestEmail')").ClickAsync();
        await Page.Locator("a:has-text('Edit')").ClickAsync();

        var uniqueRecipient = $"{TestRunId}@example.com";
        await Page.FillAsync("input[name='Input.EmailRecipients']", uniqueRecipient);
        await Page.Locator("button[type='submit']:has-text('Save')").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Navigate to the workspace checks page
        await Page.GotoAsync($"{BaseUrl}/Workspaces");
        await Page.Locator("a:has-text('SysTest')").ClickAsync();
        await Page.Locator("a:has-text('Checks')").ClickAsync();

        // Find and click on the "Bad DNS" check to go to its details
        await Page.Locator("a:has-text('Bad DNS')").ClickAsync();

        // Click the Edit button to go to the edit page
        await Page.Locator("a:has-text('Edit')").ClickAsync();

        // Enable the check
        var enabledCheckbox = Page.Locator("input[type='checkbox'][name='Input.Enabled']");
        await enabledCheckbox.CheckAsync();

        // Submit the form
        await Page.Locator("button[type='submit']:has-text('Save Changes')").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify we're back on the details page and the check is enabled
        await Expect(Page.Locator(".badge:has-text('Disabled')")).Not.ToBeVisibleAsync();

        // Verify that an email notification was sent about the check modification
        // Use TestRunId to filter for emails only from this test run
        using var httpClient = new HttpClient();
        var updateEmail = await WaitForEmailAsync(httpClient, "Check updated: Bad DNS", uniqueRecipient, TimeSpan.FromSeconds(10));
        Assert.IsNotNull(updateEmail, "Expected to find an email notification about Bad DNS check modification");
        Assert.AreEqual("[SAMA] 🔄 Check updated: Bad DNS", updateEmail.Value.GetProperty("subject").GetString());

        // Wait for the check to run and fail, then verify the failure notification email
        var failureEmail = await WaitForEmailAsync(httpClient, "Bad DNS is down", uniqueRecipient, TimeSpan.FromSeconds(30));
        Assert.IsNotNull(failureEmail, "Expected to find an email notification about Bad DNS check failure");
        Assert.AreEqual("[SAMA] ✗ Bad DNS is down", failureEmail.Value.GetProperty("subject").GetString());
    }

    private static async Task<JsonElement?> WaitForEmailAsync(HttpClient httpClient, string subjectContains, string recipientContains, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var response = await httpClient.GetAsync($"{Smtp4DevApiUrl}/Messages");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                var results = json.GetProperty("results");

                foreach (var message in results.EnumerateArray())
                {
                    var subject = message.GetProperty("subject").GetString() ?? string.Empty;
                    var toAddresses = string.Join(", ", message.GetProperty("to").EnumerateArray().Select(t => t.GetString() ?? string.Empty));

                    if (subject.Contains(subjectContains, StringComparison.OrdinalIgnoreCase) &&
                        toAddresses.Contains(recipientContains, StringComparison.OrdinalIgnoreCase))
                    {
                        return message;
                    }
                }
            }

            await Task.Delay(500);
        }

        return null;
    }
}
