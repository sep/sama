using Microsoft.Playwright;

using static Microsoft.Playwright.Assertions;

namespace SAMA.Tests.System;

[TestClass]
[SystemTestCondition]
public class OidcGroupTests : SystemTestBase
{
    [TestMethod]
    public async Task ShouldAssignWorkspaceAccessViaOidcGroupClaim()
    {
        await SetupInitialAdminAsync();
        await LoginAsync();

        // Create a workspace for group-based access
        await Page.GotoAsync($"{BaseUrl}/Workspaces/Create");
        await Page.FillAsync("input[name='Input.Name']", "OIDC Group Workspace");
        await Page.Locator("button[type='submit']:has-text('Create')").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Configure OIDC to use the mock provider
        await Page.GotoAsync($"{BaseUrl}/Admin/Settings/Oidc");
        await Page.CheckAsync("input[name='OidcInput.Enabled']");
        await Page.FillAsync("input[name='OidcInput.Authority']", "http://localhost:9400");
        await Page.FillAsync("input[name='OidcInput.ProviderName']", "Mock OIDC");
        await Page.FillAsync("input[name='OidcInput.ClientId']", "test-client");
        await Page.FillAsync("input[name='OidcInput.ClientSecret']", "test-secret");
        await Page.ClickAsync("button[type='submit']");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator("text=OIDC settings saved successfully")).ToBeVisibleAsync();

        // Add a group mapping: "test-editors" group → OIDC Group Workspace → Editor
        await Page.GotoAsync($"{BaseUrl}/Admin/Settings/GroupMappings");
        await Page.FillAsync("input[name='Input.ExternalGroupId']", "test-editors");
        await Page.Locator("select[name='Input.WorkspaceId']").SelectOptionAsync(
            new SelectOptionValue { Label = "OIDC Group Workspace" });
        await Page.Locator("select[name='Input.Role']").SelectOptionAsync("Editor");
        await Page.Locator("select[name='Input.IdentityProvider']").SelectOptionAsync("OIDC");
        await Page.Locator("button:has-text('Add Group Mapping')").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify the mapping was created
        await Expect(Page.Locator("text=test-editors")).ToBeVisibleAsync();

        await LogoutAsync();

        // Login as bob via OIDC
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.Locator("a:has-text('Sign in with Mock OIDC')").ClickAsync();

        // On the mock provider, click the predefined "bob" user button
        await Page.Locator("button:has-text('bob')").ClickAsync();

        // Bob should be redirected back to the app and auto-redirected to the workspace dashboard
        // (since he has exactly one accessible workspace via group mapping)
        await Page.WaitForURLAsync($"{BaseUrl}/Dashboard**");
        await Expect(Page.Locator("text=OIDC Group Workspace")).ToBeVisibleAsync();

        // Verify Bob has Editor access (Create Check button is editor-only)
        await Expect(Page.Locator("a:has-text('Create Check')")).ToBeVisibleAsync();
    }
}
