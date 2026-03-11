using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using NSubstitute;
using SAMA.Web.Constants;
using SAMA.Web.Services;

namespace SAMA.Tests.Unit.Web.Services;

[TestClass]
public class OidcPostConfigureOptionsTests
{
    private GlobalSettingsService _mockGlobalSettings = null!;
    private OidcPostConfigureOptions _postConfigure = null!;
    private OpenIdConnectOptions _options = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockGlobalSettings = Substitute.For<GlobalSettingsService>(null!, null!, null!, null!);
        _postConfigure = new OidcPostConfigureOptions(_mockGlobalSettings);
        _options = new OpenIdConnectOptions();
        _options.Backchannel = new HttpClient();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _options.Backchannel.Dispose();
    }

    [TestMethod]
    public void PostConfigureShouldIgnoreNonOidcSchemes()
    {
        var originalAuthority = _options.Authority;

        _postConfigure.PostConfigure("Cookies", _options);

        Assert.AreEqual(originalAuthority, _options.Authority);
    }

    [TestMethod]
    public void PostConfigureShouldSetDummyConfigWhenDisabled()
    {
        _mockGlobalSettings.OidcEnabled.Returns(false);

        _postConfigure.PostConfigure(AuthConstants.OidcSource, _options);

        Assert.AreEqual("https://localhost", _options.Authority);
        Assert.AreEqual("disabled", _options.ClientId);
        Assert.AreEqual(string.Empty, _options.MetadataAddress);
        Assert.IsNotNull(_options.Configuration);
    }

    [TestMethod]
    public void PostConfigureShouldMapSettingsWhenEnabled()
    {
        _mockGlobalSettings.OidcEnabled.Returns(true);
        _mockGlobalSettings.OidcAuthority.Returns("https://login.example.com/tenant");
        _mockGlobalSettings.OidcClientId.Returns("my-client-id");
        _mockGlobalSettings.OidcClientSecret.Returns("my-secret");
        _mockGlobalSettings.OidcScopes.Returns("openid profile email");

        _postConfigure.PostConfigure(AuthConstants.OidcSource, _options);

        Assert.AreEqual("https://login.example.com/tenant", _options.Authority);
        Assert.AreEqual("my-client-id", _options.ClientId);
        Assert.AreEqual("my-secret", _options.ClientSecret);
        Assert.AreEqual(OpenIdConnectResponseType.Code, _options.ResponseType);
        Assert.AreEqual("/signin-oidc", _options.CallbackPath.Value);
        Assert.IsFalse(_options.MapInboundClaims);
        Assert.AreEqual("name", _options.TokenValidationParameters.NameClaimType);
        Assert.AreEqual("my-client-id", _options.TokenValidationParameters.ValidAudience);
    }

    [TestMethod]
    public void PostConfigureShouldSplitAndSetScopes()
    {
        _mockGlobalSettings.OidcEnabled.Returns(true);
        _mockGlobalSettings.OidcAuthority.Returns("https://login.example.com");
        _mockGlobalSettings.OidcClientId.Returns("client");
        _mockGlobalSettings.OidcClientSecret.Returns("");
        _mockGlobalSettings.OidcScopes.Returns("openid profile email groups");
        _options.Scope.Add("pre-existing");

        _postConfigure.PostConfigure(AuthConstants.OidcSource, _options);

        Assert.AreEqual(4, _options.Scope.Count);
        CollectionAssert.AreEquivalent(
            new[] { "openid", "profile", "email", "groups" },
            _options.Scope.ToArray());
    }

    [TestMethod]
    public void PostConfigureShouldHandleExtraWhitespaceInScopes()
    {
        _mockGlobalSettings.OidcEnabled.Returns(true);
        _mockGlobalSettings.OidcAuthority.Returns("https://login.example.com");
        _mockGlobalSettings.OidcClientId.Returns("client");
        _mockGlobalSettings.OidcClientSecret.Returns("");
        _mockGlobalSettings.OidcScopes.Returns("openid  profile   email");

        _postConfigure.PostConfigure(AuthConstants.OidcSource, _options);

        Assert.AreEqual(3, _options.Scope.Count);
        CollectionAssert.AreEquivalent(
            new[] { "openid", "profile", "email" },
            _options.Scope.ToArray());
    }
}
