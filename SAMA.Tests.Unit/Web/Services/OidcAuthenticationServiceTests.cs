using NSubstitute;
using SAMA.Web.Services;

namespace SAMA.Tests.Unit.Web.Services;

[TestClass]
public class OidcAuthenticationServiceTests
{
    private GlobalSettingsService _mockGlobalSettings = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockGlobalSettings = Substitute.For<GlobalSettingsService>(null!, null!, null!, null!);
    }

    [TestMethod]
    public void IsOidcEnabledShouldReturnTrueWhenAllConditionsMet()
    {
        _mockGlobalSettings.OidcEnabled.Returns(true);
        _mockGlobalSettings.OidcAuthority.Returns("https://login.example.com/tenant");
        _mockGlobalSettings.OidcClientId.Returns("my-client-id");
        var service = new OidcAuthenticationService(_mockGlobalSettings, null!, null!, null!);

        Assert.IsTrue(service.IsOidcEnabled);
    }

    [TestMethod]
    public void IsOidcEnabledShouldReturnFalseWhenDisabled()
    {
        _mockGlobalSettings.OidcEnabled.Returns(false);
        _mockGlobalSettings.OidcAuthority.Returns("https://login.example.com/tenant");
        _mockGlobalSettings.OidcClientId.Returns("my-client-id");
        var service = new OidcAuthenticationService(_mockGlobalSettings, null!, null!, null!);

        Assert.IsFalse(service.IsOidcEnabled);
    }

    [TestMethod]
    public void IsOidcEnabledShouldReturnFalseWhenAuthorityEmpty()
    {
        _mockGlobalSettings.OidcEnabled.Returns(true);
        _mockGlobalSettings.OidcAuthority.Returns("");
        _mockGlobalSettings.OidcClientId.Returns("my-client-id");
        var service = new OidcAuthenticationService(_mockGlobalSettings, null!, null!, null!);

        Assert.IsFalse(service.IsOidcEnabled);
    }

    [TestMethod]
    public void IsOidcEnabledShouldReturnFalseWhenClientIdEmpty()
    {
        _mockGlobalSettings.OidcEnabled.Returns(true);
        _mockGlobalSettings.OidcAuthority.Returns("https://login.example.com/tenant");
        _mockGlobalSettings.OidcClientId.Returns("  ");
        var service = new OidcAuthenticationService(_mockGlobalSettings, null!, null!, null!);

        Assert.IsFalse(service.IsOidcEnabled);
    }
}
