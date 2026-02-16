using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Pages.Account;
using SAMA.Web.Services;

namespace SAMA.Tests.Unit.Web.Pages.Account;

[TestClass]
public class LoginModelTests
{
    private SignInManager<ApplicationUser> _mockSignInManager = null!;
    private LdapAuthenticationService _mockLdapService = null!;
    private ILogger<LoginModel> _mockLogger = null!;
    private LoginModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);

        _mockSignInManager = Substitute.For<SignInManager<ApplicationUser>>(
            userManager,
            Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null,
            null,
            null,
            null);

        _mockLdapService = Substitute.For<LdapAuthenticationService>(null!, null!, null!);
        _mockLogger = Substitute.For<ILogger<LoginModel>>();

        _pageModel = new LoginModel(_mockSignInManager, _mockLdapService, _mockLogger);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
    }

    [TestMethod]
    public void LdapEnabledShouldReturnTrueWhenServiceReportsEnabled()
    {
        _mockLdapService.IsLdapEnabled.Returns(true);

        Assert.IsTrue(_pageModel.LdapEnabled);
    }

    [TestMethod]
    public void LdapEnabledShouldReturnFalseWhenServiceReportsDisabled()
    {
        _mockLdapService.IsLdapEnabled.Returns(false);

        Assert.IsFalse(_pageModel.LdapEnabled);
    }

    [TestMethod]
    public async Task OnPostShouldReturnPageWhenModelStateIsInvalid()
    {
        _pageModel.ModelState.AddModelError("Input.EmailOrUsername", "Required");

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnPostShouldRedirectOnSuccessfulLocalLogin()
    {
        _pageModel.Input = new LoginModel.InputModel
        {
            EmailOrUsername = "user@example.com",
            Password = "testpassword12345",
        };

        _mockSignInManager.PasswordSignInAsync("user@example.com", "testpassword12345", false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var result = await _pageModel.OnPostAsync("/");

        Assert.IsInstanceOfType<LocalRedirectResult>(result);
    }

    [TestMethod]
    public async Task OnPostShouldReturnPageWithErrorOnFailedLocalLogin()
    {
        _pageModel.Input = new LoginModel.InputModel
        {
            EmailOrUsername = "user@example.com",
            Password = "wrongpassword1234",
        };

        _mockSignInManager.PasswordSignInAsync("user@example.com", "wrongpassword1234", false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var result = await _pageModel.OnPostAsync("/");

        Assert.IsInstanceOfType<PageResult>(result);
        Assert.IsFalse(_pageModel.ModelState.IsValid);
    }

    [TestMethod]
    public async Task OnPostShouldTryLdapFirstWhenEnabled()
    {
        _mockLdapService.IsLdapEnabled.Returns(true);

        _pageModel.Input = new LoginModel.InputModel
        {
            EmailOrUsername = "testuser",
            Password = "correctpassword1",
        };

        var ldapResult = LdapLoginResult.Success(
            "CN=Test User,DC=example,DC=com",
            "testuser@example.com",
            "Test User",
            []);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser@example.com",
            Email = "testuser@example.com",
        };

        _mockLdapService.AuthenticateAsync("testuser", "correctpassword1")
            .Returns(ldapResult);

        _mockLdapService.ProvisionOrUpdateUserAsync(ldapResult)
            .Returns(user);

        var result = await _pageModel.OnPostAsync("/");

        Assert.IsInstanceOfType<LocalRedirectResult>(result);
        await _mockSignInManager.Received(1).SignInAsync(user, false);
    }

    [TestMethod]
    public async Task OnPostShouldFallBackToLocalWhenLdapFails()
    {
        _mockLdapService.IsLdapEnabled.Returns(true);

        _pageModel.Input = new LoginModel.InputModel
        {
            EmailOrUsername = "user@example.com",
            Password = "testpassword12345",
        };

        _mockLdapService.AuthenticateAsync("user@example.com", "testpassword12345")
            .Returns(LdapLoginResult.Fail("User not found."));

        _mockSignInManager.PasswordSignInAsync("user@example.com", "testpassword12345", false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var result = await _pageModel.OnPostAsync("/");

        Assert.IsInstanceOfType<LocalRedirectResult>(result);
    }

    [TestMethod]
    public async Task OnPostShouldBlockLocalLoginForLdapUsers()
    {
        _pageModel.Input = new LoginModel.InputModel
        {
            EmailOrUsername = "ldapuser@example.com",
            Password = "testpassword12345",
        };

        var ldapUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "ldapuser@example.com",
            Email = "ldapuser@example.com",
        };

        _mockSignInManager.UserManager.FindByEmailAsync("ldapuser@example.com")
            .Returns(ldapUser);

        _mockSignInManager.UserManager.GetLoginsAsync(ldapUser)
            .Returns([new UserLoginInfo("LDAP", "CN=User,DC=example,DC=com", "LDAP")]);

        var result = await _pageModel.OnPostAsync("/");

        Assert.IsInstanceOfType<PageResult>(result);
        Assert.IsFalse(_pageModel.ModelState.IsValid);
    }

    [TestMethod]
    public async Task OnPostShouldReturnPageWithErrorOnProvisioningFailure()
    {
        _mockLdapService.IsLdapEnabled.Returns(true);

        _pageModel.Input = new LoginModel.InputModel
        {
            EmailOrUsername = "testuser",
            Password = "correctpassword1",
        };

        var ldapResult = LdapLoginResult.Success(
            "CN=Test User,DC=example,DC=com",
            "testuser@example.com",
            "Test User",
            []);

        _mockLdapService.AuthenticateAsync("testuser", "correctpassword1")
            .Returns(ldapResult);

        _mockLdapService.ProvisionOrUpdateUserAsync(ldapResult)
            .Returns<ApplicationUser>(x => throw new InvalidOperationException("Provisioning failed"));

        var result = await _pageModel.OnPostAsync("/");

        Assert.IsInstanceOfType<PageResult>(result);
        Assert.IsFalse(_pageModel.ModelState.IsValid);
    }
}
