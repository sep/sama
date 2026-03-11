using Microsoft.Playwright;

using static Microsoft.Playwright.Assertions;

namespace SAMA.Tests.System;

[TestClass]
[SystemTestCondition]
public class OidcTests : SystemTestBase
{
    [TestMethod]
    public async Task ShouldLoginViaOidc()
    {
        await SetupInitialAdminAsync();
        await LoginAsync();

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

        await LogoutAsync();

        // Verify OIDC button appears on login page
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        var oidcButton = Page.Locator("a:has-text('Sign in with Mock OIDC')");
        await Expect(oidcButton).ToBeVisibleAsync();

        // Initiate OIDC login
        await oidcButton.ClickAsync();

        // On the mock provider, click the predefined "alice" user button
        await Page.Locator("button:has-text('alice')").ClickAsync();

        // Should redirect back to the app, logged in as alice
        await Page.WaitForURLAsync($"{BaseUrl}/**");

        // Alice is a new OIDC-provisioned user with no workspaces
        await Expect(Page.Locator("text=You don't have access to any workspaces yet")).ToBeVisibleAsync();
    }
}
