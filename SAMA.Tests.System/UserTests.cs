using Microsoft.Playwright;

using static Microsoft.Playwright.Assertions;

namespace SAMA.Tests.System;

[TestClass]
[SystemTestCondition]
public class UserTests : SystemTestBase
{
    [TestMethod]
    public async Task ShouldRegisterAndLoginWithRegisteredCredentials()
    {
        var email = "testadmin@example.com";
        var password = "SecurePassword123!";

        await SetupInitialAdminAsync(email, password);

        var titleAfterSetup = await Page.TitleAsync();
        Assert.AreEqual("Welcome - SAMA", titleAfterSetup);

        await LoginAsync(email, password);

        await Page.WaitForURLAsync($"{BaseUrl}/Dashboard**");
        var titleAfterLogin = await Page.TitleAsync();
        Assert.AreEqual("Dashboard - SAMA", titleAfterLogin);

        // Create a new non-admin user without any workspace assignments
        var newUserEmail = "regularuser@example.com";
        var newUserPassword = "RegularUserPass123!";

        await Page.GotoAsync($"{BaseUrl}/Admin/Users/Create");
        await Page.WaitForURLAsync($"{BaseUrl}/Admin/Users/Create");

        await Page.FillAsync("input[name='Input.Email']", newUserEmail);
        await Page.FillAsync("input[name='Input.Password']", newUserPassword);
        await Page.FillAsync("input[name='Input.ConfirmPassword']", newUserPassword);
        await Page.ClickAsync("button[type='submit']");

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForURLAsync($"{BaseUrl}/Admin/Users");

        // Logout as admin
        await LogoutAsync();

        var titleAfterLogout = await Page.TitleAsync();
        Assert.AreEqual("Welcome - SAMA", titleAfterLogout);

        // Login as the new non-admin user
        await LoginAsync(newUserEmail, newUserPassword);

        // Non-admin users without workspaces should see the Index page with no workspaces message
        await Page.WaitForURLAsync(BaseUrl);
        await Expect(Page.Locator("text=You don't have access to any workspaces yet")).ToBeVisibleAsync();
    }
}
