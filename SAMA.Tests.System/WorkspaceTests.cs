using Microsoft.Playwright;

using static Microsoft.Playwright.Assertions;

namespace SAMA.Tests.System;

[TestClass]
[SystemTestCondition]
public class WorkspaceTests : SystemTestBase
{
    [TestMethod]
    public async Task ShouldUpdateWorkspaceSettingsIncludingDashboardMessage()
    {
        await SetupInitialAdminAsync();
        await LoginAsync();
        await Page.WaitForURLAsync($"{BaseUrl}/Dashboard**");

        // Create a new workspace with a dashboard message
        await Page.GotoAsync($"{BaseUrl}/Workspaces/Create");
        await Page.FillAsync("input[name='Input.Name']", "Test Workspace");
        await Page.FillAsync("textarea[name='Input.Description']", "Original description");

        // Add a dashboard message during creation
        var createMessage = "## Initial Message\n\nCreated with **Markdown** support.";
        await Page.FillAsync("textarea[name='Input.DashboardMessage']", createMessage);

        // Wait for preview to update and verify it works on create page
        await Page.WaitForTimeoutAsync(600);
        await Expect(Page.Locator("#dashboard-message-preview h2")).ToContainTextAsync("Initial Message");
        await Expect(Page.Locator("#dashboard-message-preview strong")).ToContainTextAsync("Markdown");

        await Page.Locator("button[type='submit']:has-text('Create')").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // After creation, we're on the workspaces index - click into the new workspace
        await Page.Locator("a:has-text('Test Workspace')").ClickAsync();
        await Page.WaitForURLAsync($"{BaseUrl}/Dashboard**");

        // Verify the dashboard message from creation is displayed
        await Expect(Page.Locator(".dashboard-message h2")).ToContainTextAsync("Initial Message");
        await Expect(Page.Locator(".dashboard-message strong")).ToContainTextAsync("Markdown");

        // Navigate to workspace settings (use the workspace nav, not admin settings)
        await Page.Locator(".workspace-context a:has-text('Settings')").ClickAsync();
        await Page.WaitForURLAsync($"{BaseUrl}/Workspaces/Edit**");

        // Update workspace name
        await Page.FillAsync("input[name='Input.Name']", "Updated Workspace");

        // Update description
        await Page.FillAsync("textarea[name='Input.Description']", "Updated description");

        // Update the dashboard message with different Markdown
        var dashboardMessage = "# Welcome\n\nThis is a **test** workspace with [a link](https://example.com).";
        await Page.FillAsync("textarea[name='Input.DashboardMessage']", dashboardMessage);

        // Wait for preview to update
        await Page.WaitForTimeoutAsync(600);
        await Expect(Page.Locator("#dashboard-message-preview h1")).ToContainTextAsync("Welcome");
        await Expect(Page.Locator("#dashboard-message-preview strong")).ToContainTextAsync("test");

        // Make workspace public
        await Page.Locator("input[type='checkbox'][name='Input.IsPublic']").CheckAsync();

        // Save changes
        await Page.Locator("button[type='submit']:has-text('Save')").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Should redirect to dashboard
        await Page.WaitForURLAsync($"{BaseUrl}/Dashboard**");

        // Verify the updated dashboard message is displayed
        await Expect(Page.Locator(".dashboard-message h1")).ToContainTextAsync("Welcome");
        await Expect(Page.Locator(".dashboard-message strong")).ToContainTextAsync("test");
        await Expect(Page.Locator(".dashboard-message a")).ToHaveAttributeAsync("target", "_blank");

        // Navigate back to settings and verify values were saved
        await Page.Locator(".workspace-context a:has-text('Settings')").ClickAsync();
        await Expect(Page.Locator("input[name='Input.Name']")).ToHaveValueAsync("Updated Workspace");
        await Expect(Page.Locator("textarea[name='Input.Description']")).ToHaveValueAsync("Updated description");
        await Expect(Page.Locator("textarea[name='Input.DashboardMessage']")).ToHaveValueAsync(dashboardMessage);
        await Expect(Page.Locator("input[type='checkbox'][name='Input.IsPublic']")).ToBeCheckedAsync();

        // Clear the dashboard message
        await Page.FillAsync("textarea[name='Input.DashboardMessage']", "");
        await Page.Locator("button[type='submit']:has-text('Save')").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify message is no longer displayed on dashboard
        await Page.WaitForURLAsync($"{BaseUrl}/Dashboard**");
        await Expect(Page.Locator(".dashboard-message")).Not.ToBeVisibleAsync();
    }
}
