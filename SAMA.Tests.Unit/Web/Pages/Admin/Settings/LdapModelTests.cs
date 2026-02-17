using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Pages.Admin.Settings;
using SAMA.Web.Services;

namespace SAMA.Tests.Unit.Web.Pages.Admin.Settings;

[TestClass]
public class LdapModelTests
{
    private GlobalSettingsService _mockGlobalSettings = null!;
    private LdapAuthenticationService _mockLdapService = null!;
    private ILogger<LdapModel> _mockLogger = null!;
    private LdapModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockGlobalSettings = Substitute.For<GlobalSettingsService>(null!, null!, null!, null!);
        _mockLdapService = Substitute.For<LdapAuthenticationService>(null!, null!, null!);
        _mockLogger = Substitute.For<ILogger<LdapModel>>();

        _pageModel = new LdapModel(_mockGlobalSettings, _mockLdapService, _mockLogger);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
        _pageModel.PageContext.HttpContext.Request.Form = new FormCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>());
    }

    [TestMethod]
    public void OnGetShouldLoadLdapSettings()
    {
        _mockGlobalSettings.LdapEnabled.Returns(true);
        _mockGlobalSettings.LdapHost.Returns("ldap.example.com");
        _mockGlobalSettings.LdapPort.Returns(636);
        _mockGlobalSettings.LdapUseSsl.Returns(true);
        _mockGlobalSettings.LdapUseStartTls.Returns(false);
        _mockGlobalSettings.LdapBindDn.Returns("CN=svc,DC=example,DC=com");
        _mockGlobalSettings.LdapBindTemplate.Returns("uid={0},ou=users,dc=example,dc=com");
        _mockGlobalSettings.LdapSearchBase.Returns("DC=example,DC=com");
        _mockGlobalSettings.LdapSearchFilter.Returns("(&(objectClass=user)(uid={0}))");
        _mockGlobalSettings.LdapGroupSearchBase.Returns("OU=Groups,DC=example,DC=com");
        _mockGlobalSettings.LdapGroupSearchFilter.Returns("(&(objectClass=group)(member={0}))");
        _mockGlobalSettings.LdapCustomRootCa.Returns(string.Empty);

        _pageModel.OnGet();

        Assert.IsTrue(_pageModel.LdapInput.Enabled);
        Assert.AreEqual("ldap.example.com", _pageModel.LdapInput.Host);
        Assert.AreEqual(636, _pageModel.LdapInput.Port);
        Assert.IsTrue(_pageModel.LdapInput.UseSsl);
        Assert.IsFalse(_pageModel.LdapInput.UseStartTls);
        Assert.AreEqual("CN=svc,DC=example,DC=com", _pageModel.LdapInput.BindDn);
        Assert.AreEqual("uid={0},ou=users,dc=example,dc=com", _pageModel.LdapInput.BindTemplate);
        Assert.AreEqual("DC=example,DC=com", _pageModel.LdapInput.SearchBase);
        Assert.AreEqual("(&(objectClass=user)(uid={0}))", _pageModel.LdapInput.SearchFilter);
        Assert.AreEqual("OU=Groups,DC=example,DC=com", _pageModel.LdapInput.GroupSearchBase);
        Assert.AreEqual("(&(objectClass=group)(member={0}))", _pageModel.LdapInput.GroupSearchFilter);
    }

    [TestMethod]
    public void OnPostLdapShouldSaveLdapSettings()
    {
        _pageModel.LdapInput = new LdapModel.LdapInputModel
        {
            Enabled = true,
            Host = "ldap.example.com",
            Port = 636,
            UseSsl = true,
            UseStartTls = false,
            BindDn = "CN=svc,DC=example,DC=com",
            BindPassword = "secret-password1",
            BindTemplate = "",
            SearchBase = "DC=example,DC=com",
            SearchFilter = "(&(objectClass=user)(sAMAccountName={0}))",
            GroupSearchBase = "OU=Groups,DC=example,DC=com",
            GroupSearchFilter = "(&(objectClass=group)(member={0}))",
        };

        var result = _pageModel.OnPost();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        _mockGlobalSettings.Received(1).LdapEnabled = true;
        _mockGlobalSettings.Received(1).LdapHost = "ldap.example.com";
        _mockGlobalSettings.Received(1).LdapPort = 636;
        _mockGlobalSettings.Received(1).LdapUseSsl = true;
        _mockGlobalSettings.Received(1).LdapUseStartTls = false;
        _mockGlobalSettings.Received(1).LdapBindDn = "CN=svc,DC=example,DC=com";
        _mockGlobalSettings.Received(1).LdapBindPassword = "secret-password1";
        _mockGlobalSettings.Received(1).LdapBindTemplate = "";
        _mockGlobalSettings.Received(1).LdapSearchBase = "DC=example,DC=com";
        _mockGlobalSettings.Received(1).LdapSearchFilter = "(&(objectClass=user)(sAMAccountName={0}))";
        _mockGlobalSettings.Received(1).LdapGroupSearchBase = "OU=Groups,DC=example,DC=com";
        _mockGlobalSettings.Received(1).LdapGroupSearchFilter = "(&(objectClass=group)(member={0}))";
    }

    [TestMethod]
    public void OnPostShouldSaveBothSslAndStartTlsWhenBothEnabled()
    {
        _pageModel.LdapInput = new LdapModel.LdapInputModel
        {
            Host = "ldap.example.com",
            Port = 636,
            UseSsl = true,
            UseStartTls = true,
            SearchBase = "DC=example,DC=com",
        };

        var result = _pageModel.OnPost();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        _mockGlobalSettings.Received(1).LdapUseSsl = true;
        _mockGlobalSettings.Received(1).LdapUseStartTls = true;
    }

    [TestMethod]
    public void OnGetShouldLoadBothSslAndStartTlsValues()
    {
        _mockGlobalSettings.LdapUseSsl.Returns(true);
        _mockGlobalSettings.LdapUseStartTls.Returns(true);

        _pageModel.OnGet();

        Assert.IsTrue(_pageModel.LdapInput.UseSsl);
        Assert.IsTrue(_pageModel.LdapInput.UseStartTls);
    }

    [TestMethod]
    public void OnGetShouldLoadCustomRootCa()
    {
        _mockGlobalSettings.LdapCustomRootCa.Returns("-----BEGIN CERTIFICATE-----\ntest\n-----END CERTIFICATE-----");

        _pageModel.OnGet();

        Assert.AreEqual("-----BEGIN CERTIFICATE-----\ntest\n-----END CERTIFICATE-----", _pageModel.LdapInput.CustomRootCa);
    }

    [TestMethod]
    public void OnPostShouldSaveCustomRootCa()
    {
        _pageModel.LdapInput = new LdapModel.LdapInputModel
        {
            Host = "ldap.example.com",
            Port = 636,
            UseSsl = true,
            CustomRootCa = "-----BEGIN CERTIFICATE-----\nMIItest\n-----END CERTIFICATE-----",
        };

        _pageModel.OnPost();

        _mockGlobalSettings.Received(1).LdapCustomRootCa = "-----BEGIN CERTIFICATE-----\nMIItest\n-----END CERTIFICATE-----";
    }

    [TestMethod]
    public void OnPostShouldClearCustomRootCaWhenEmpty()
    {
        _pageModel.LdapInput = new LdapModel.LdapInputModel
        {
            Host = "ldap.example.com",
            Port = 389,
            CustomRootCa = string.Empty,
        };

        _pageModel.OnPost();

        _mockGlobalSettings.Received(1).LdapCustomRootCa = string.Empty;
    }

    [TestMethod]
    public void OnPostLdapShouldNotUpdatePasswordWhenEmpty()
    {
        _pageModel.LdapInput = new LdapModel.LdapInputModel
        {
            Enabled = true,
            Host = "ldap.example.com",
            Port = 389,
            BindDn = "CN=svc,DC=example,DC=com",
            BindPassword = string.Empty,
            SearchBase = "DC=example,DC=com",
        };

        _pageModel.OnPost();

        _mockGlobalSettings.DidNotReceive().LdapBindPassword = Arg.Any<string>();
    }

    [TestMethod]
    public void OnPostLdapShouldClearPasswordWhenCheckboxChecked()
    {
        _pageModel.LdapInput = new LdapModel.LdapInputModel
        {
            Enabled = true,
            Host = "ldap.example.com",
            Port = 389,
            BindDn = "CN=svc,DC=example,DC=com",
            BindPassword = string.Empty,
            SearchBase = "DC=example,DC=com",
        };
        _pageModel.PageContext.HttpContext.Request.Form = new FormCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "ClearBindPassword", "true" }
            });

        _pageModel.OnPost();

        _mockGlobalSettings.Received(1).LdapBindPassword = Arg.Any<string>();
    }

    [TestMethod]
    public void OnGetShouldSetHasExistingBindPassword()
    {
        _mockGlobalSettings.LdapBindPassword.Returns("some-encrypted-password");

        _pageModel.OnGet();

        Assert.IsTrue(_pageModel.HasExistingBindPassword);
    }

    [TestMethod]
    public void OnGetShouldNotSetHasExistingBindPasswordWhenEmpty()
    {
        _mockGlobalSettings.LdapBindPassword.Returns(string.Empty);

        _pageModel.OnGet();

        Assert.IsFalse(_pageModel.HasExistingBindPassword);
    }

    [TestMethod]
    public void OnPostLdapShouldSetSuccessMessage()
    {
        _pageModel.LdapInput = new LdapModel.LdapInputModel
        {
            Host = "ldap.example.com",
            Port = 389,
            SearchBase = "DC=example,DC=com",
        };

        _pageModel.OnPost();

        Assert.AreEqual("LDAP settings saved successfully.", _pageModel.TempData["LdapSuccess"]);
    }

    [TestMethod]
    public async Task OnPostTestLoginShouldReturnSuccessWithGroups()
    {
        _pageModel.TestLoginInput = new LdapModel.TestLoginInputModel
        {
            Username = "jdoe",
            Password = "password123",
        };
        _mockLdapService.AuthenticateAsync("jdoe", "password123", true)
            .Returns(LdapLoginResult.Success(
                "CN=jdoe,DC=example,DC=com",
                "jdoe@example.com",
                "John Doe",
                ["Domain Users", "Developers"]));

        var result = await _pageModel.OnPostTestLoginAsync();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        Assert.IsTrue((bool)_pageModel.TempData["LdapTestSuccess"]!);
        Assert.AreEqual("Login successful. DN: \"CN=jdoe,DC=example,DC=com\", Email: \"jdoe@example.com\", Display Name: \"John Doe\".", (string)_pageModel.TempData["LdapTestMessage"]!);
        Assert.AreEqual("Domain Users\nDevelopers", (string)_pageModel.TempData["LdapTestGroups"]!);
    }

    [TestMethod]
    public async Task OnPostTestLoginShouldReturnFailureMessage()
    {
        _pageModel.TestLoginInput = new LdapModel.TestLoginInputModel
        {
            Username = "jdoe",
            Password = "wrongpassword",
        };
        _mockLdapService.AuthenticateAsync("jdoe", "wrongpassword", true)
            .Returns(LdapLoginResult.Fail("User bind failed for DN 'CN=jdoe,DC=example,DC=com': Invalid credentials"));

        var result = await _pageModel.OnPostTestLoginAsync();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        Assert.IsFalse((bool)_pageModel.TempData["LdapTestSuccess"]!);
        Assert.AreEqual("User bind failed for DN 'CN=jdoe,DC=example,DC=com': Invalid credentials", _pageModel.TempData["LdapTestMessage"]);
        Assert.IsNull(_pageModel.TempData["LdapTestGroups"]);
    }

    [TestMethod]
    public async Task OnPostTestLoginShouldRequireUsernameAndPassword()
    {
        _pageModel.TestLoginInput = new LdapModel.TestLoginInputModel
        {
            Username = "",
            Password = "",
        };

        var result = await _pageModel.OnPostTestLoginAsync();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        Assert.IsFalse((bool)_pageModel.TempData["LdapTestSuccess"]!);
        Assert.AreEqual("Username and password are required.", _pageModel.TempData["LdapTestMessage"]);
        await _mockLdapService.DidNotReceive().AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [TestMethod]
    public void OnPostShouldReturnErrorWhenSearchFilterIsEmpty()
    {
        _pageModel.LdapInput = new LdapModel.LdapInputModel
        {
            Host = "ldap.example.com",
            Port = 389,
            SearchBase = "DC=example,DC=com",
            SearchFilter = string.Empty,
        };

        var result = _pageModel.OnPost();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        Assert.AreEqual("User Search Filter is invalid. It must be non-empty and contain at least one {0} placeholder for the username.", _pageModel.TempData["LdapError"]);
        _mockGlobalSettings.DidNotReceive().LdapSearchFilter = Arg.Any<string>();
    }

    [TestMethod]
    public void OnPostShouldReturnErrorWhenSearchFilterMissingPlaceholder()
    {
        _pageModel.LdapInput = new LdapModel.LdapInputModel
        {
            Host = "ldap.example.com",
            Port = 389,
            SearchBase = "DC=example,DC=com",
            SearchFilter = "(&(objectClass=user)(sAMAccountName=invalid))",
        };

        var result = _pageModel.OnPost();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        Assert.AreEqual("User Search Filter is invalid. It must be non-empty and contain at least one {0} placeholder for the username.", _pageModel.TempData["LdapError"]);
        _mockGlobalSettings.DidNotReceive().LdapSearchFilter = Arg.Any<string>();
    }

    [TestMethod]
    public void OnPostShouldReturnErrorWhenGroupSearchFilterIsEmptyWithGroupSearchBase()
    {
        _pageModel.LdapInput = new LdapModel.LdapInputModel
        {
            Host = "ldap.example.com",
            Port = 389,
            SearchBase = "DC=example,DC=com",
            SearchFilter = "(&(objectClass=user)(uid={0}))",
            GroupSearchBase = "OU=Groups,DC=example,DC=com",
            GroupSearchFilter = string.Empty,
        };

        var result = _pageModel.OnPost();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        Assert.AreEqual("Group Search Filter is invalid. It must be non-empty and contain at least one {0} placeholder for the user DN.", _pageModel.TempData["LdapError"]);
        _mockGlobalSettings.DidNotReceive().LdapGroupSearchFilter = Arg.Any<string>();
    }

    [TestMethod]
    public void OnPostShouldReturnErrorWhenGroupSearchFilterMissingPlaceholder()
    {
        _pageModel.LdapInput = new LdapModel.LdapInputModel
        {
            Host = "ldap.example.com",
            Port = 389,
            SearchBase = "DC=example,DC=com",
            SearchFilter = "(&(objectClass=user)(uid={0}))",
            GroupSearchBase = "OU=Groups,DC=example,DC=com",
            GroupSearchFilter = "(&(objectClass=group)(member=invalid))",
        };

        var result = _pageModel.OnPost();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        Assert.AreEqual("Group Search Filter is invalid. It must be non-empty and contain at least one {0} placeholder for the user DN.", _pageModel.TempData["LdapError"]);
        _mockGlobalSettings.DidNotReceive().LdapGroupSearchFilter = Arg.Any<string>();
    }

    [TestMethod]
    public void OnPostShouldAllowInvalidGroupSearchFilterWhenGroupSearchBaseIsEmpty()
    {
        _pageModel.LdapInput = new LdapModel.LdapInputModel
        {
            Host = "ldap.example.com",
            Port = 389,
            SearchBase = "DC=example,DC=com",
            SearchFilter = "(&(objectClass=user)(uid={0}))",
            GroupSearchBase = string.Empty,
            GroupSearchFilter = string.Empty,
        };

        var result = _pageModel.OnPost();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        Assert.AreEqual("LDAP settings saved successfully.", _pageModel.TempData["LdapSuccess"]);
        _mockGlobalSettings.Received(1).LdapGroupSearchFilter = string.Empty;
    }

    [TestMethod]
    public void OnPostShouldUseValidSearchFilter()
    {
        _pageModel.LdapInput = new LdapModel.LdapInputModel
        {
            Host = "ldap.example.com",
            Port = 389,
            SearchBase = "DC=example,DC=com",
            SearchFilter = "(&(objectClass=person)(uid={0}))",
        };

        _pageModel.OnPost();

        _mockGlobalSettings.Received(1).LdapSearchFilter = "(&(objectClass=person)(uid={0}))";
    }

    [TestMethod]
    public void OnPostShouldUseValidGroupSearchFilter()
    {
        _pageModel.LdapInput = new LdapModel.LdapInputModel
        {
            Host = "ldap.example.com",
            Port = 389,
            SearchBase = "DC=example,DC=com",
            GroupSearchBase = "OU=Groups,DC=example,DC=com",
            GroupSearchFilter = "(&(objectClass=posixGroup)(memberUid={0}))",
        };

        _pageModel.OnPost();

        _mockGlobalSettings.Received(1).LdapGroupSearchFilter = "(&(objectClass=posixGroup)(memberUid={0}))";
    }

    [TestMethod]
    public void OnPostShouldRejectSearchFilterWithMultiplePlaceholders()
    {
        _pageModel.LdapInput = new LdapModel.LdapInputModel
        {
            Host = "ldap.example.com",
            Port = 389,
            SearchBase = "DC=example,DC=com",
            SearchFilter = "(&(objectClass=user)(uid={0})(cn={1}))",
        };

        var result = _pageModel.OnPost();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        Assert.AreEqual("User Search Filter is invalid. It must be non-empty and contain at least one {0} placeholder for the username.", _pageModel.TempData["LdapError"]);
        _mockGlobalSettings.DidNotReceive().LdapSearchFilter = Arg.Any<string>();
    }

    [TestMethod]
    public void OnPostShouldRejectSearchFilterWithInvalidFormatSpecifier()
    {
        _pageModel.LdapInput = new LdapModel.LdapInputModel
        {
            Host = "ldap.example.com",
            Port = 389,
            SearchBase = "DC=example,DC=com",
            SearchFilter = "(&(objectClass=user)(uid={0:D}))",
        };

        var result = _pageModel.OnPost();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        Assert.AreEqual("User Search Filter is invalid. It must be non-empty and contain at least one {0} placeholder for the username.", _pageModel.TempData["LdapError"]);
        _mockGlobalSettings.DidNotReceive().LdapSearchFilter = Arg.Any<string>();
    }

    [TestMethod]
    public void OnPostShouldRejectGroupSearchFilterWithMultiplePlaceholders()
    {
        _pageModel.LdapInput = new LdapModel.LdapInputModel
        {
            Host = "ldap.example.com",
            Port = 389,
            SearchBase = "DC=example,DC=com",
            SearchFilter = "(&(objectClass=user)(uid={0}))",
            GroupSearchBase = "OU=Groups,DC=example,DC=com",
            GroupSearchFilter = "(&(objectClass=group)(member={0})(owner={1}))",
        };

        var result = _pageModel.OnPost();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        Assert.AreEqual("Group Search Filter is invalid. It must be non-empty and contain at least one {0} placeholder for the user DN.", _pageModel.TempData["LdapError"]);
        _mockGlobalSettings.DidNotReceive().LdapGroupSearchFilter = Arg.Any<string>();
    }

    [TestMethod]
    public void OnPostShouldAcceptSearchFilterWithMultipleSamePlaceholders()
    {
        _pageModel.LdapInput = new LdapModel.LdapInputModel
        {
            Host = "ldap.example.com",
            Port = 389,
            SearchBase = "DC=example,DC=com",
            SearchFilter = "(&(uid={0})(cn={0}))",
        };

        _pageModel.OnPost();

        _mockGlobalSettings.Received(1).LdapSearchFilter = "(&(uid={0})(cn={0}))";
    }
}
