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

    [TestInitialize]
    public void Setup()
    {
        _mockGlobalSettings = Substitute.For<GlobalSettingsService>(null!, null!, null!, null!);
        _postConfigure = new OidcPostConfigureOptions(_mockGlobalSettings);
    }

    [TestMethod]
    public void PostConfigureShouldIgnoreNonOidcSchemes()
    {
        var options = new OpenIdConnectOptions();
        var originalAuthority = options.Authority;

        _postConfigure.PostConfigure("Cookies", options);

        Assert.AreEqual(originalAuthority, options.Authority);
    }

    [TestMethod]
    public void PostConfigureShouldSetDummyConfigWhenDisabled()
    {
        _mockGlobalSettings.OidcEnabled.Returns(false);
        var options = new OpenIdConnectOptions();

        _postConfigure.PostConfigure(AuthConstants.OidcSource, options);

        Assert.AreEqual("https://localhost", options.Authority);
        Assert.AreEqual("disabled", options.ClientId);
        Assert.AreEqual(string.Empty, options.MetadataAddress);
        Assert.IsNotNull(options.Configuration);
    }

    [TestMethod]
    public void PostConfigureShouldMapSettingsWhenEnabled()
    {
        _mockGlobalSettings.OidcEnabled.Returns(true);
        _mockGlobalSettings.OidcAuthority.Returns("https://login.example.com/tenant");
        _mockGlobalSettings.OidcClientId.Returns("my-client-id");
        _mockGlobalSettings.OidcClientSecret.Returns("my-secret");
        _mockGlobalSettings.OidcScopes.Returns("openid profile email");
        var options = new OpenIdConnectOptions();

        _postConfigure.PostConfigure(AuthConstants.OidcSource, options);

        Assert.AreEqual("https://login.example.com/tenant", options.Authority);
        Assert.AreEqual("my-client-id", options.ClientId);
        Assert.AreEqual("my-secret", options.ClientSecret);
        Assert.AreEqual(OpenIdConnectResponseType.Code, options.ResponseType);
        Assert.AreEqual("/signin-oidc", options.CallbackPath.Value);
        Assert.IsFalse(options.MapInboundClaims);
        Assert.AreEqual("name", options.TokenValidationParameters.NameClaimType);
        Assert.AreEqual("my-client-id", options.TokenValidationParameters.ValidAudience);
    }

    [TestMethod]
    public void PostConfigureShouldSplitAndSetScopes()
    {
        _mockGlobalSettings.OidcEnabled.Returns(true);
        _mockGlobalSettings.OidcAuthority.Returns("https://login.example.com");
        _mockGlobalSettings.OidcClientId.Returns("client");
        _mockGlobalSettings.OidcClientSecret.Returns("");
        _mockGlobalSettings.OidcScopes.Returns("openid profile email groups");
        var options = new OpenIdConnectOptions();
        options.Scope.Add("pre-existing");

        _postConfigure.PostConfigure(AuthConstants.OidcSource, options);

        Assert.AreEqual(4, options.Scope.Count);
        CollectionAssert.AreEquivalent(
            new[] { "openid", "profile", "email", "groups" },
            options.Scope.ToArray());
    }

    [TestMethod]
    public void PostConfigureShouldHandleExtraWhitespaceInScopes()
    {
        _mockGlobalSettings.OidcEnabled.Returns(true);
        _mockGlobalSettings.OidcAuthority.Returns("https://login.example.com");
        _mockGlobalSettings.OidcClientId.Returns("client");
        _mockGlobalSettings.OidcClientSecret.Returns("");
        _mockGlobalSettings.OidcScopes.Returns("openid  profile   email");
        var options = new OpenIdConnectOptions();

        _postConfigure.PostConfigure(AuthConstants.OidcSource, options);

        Assert.AreEqual(3, options.Scope.Count);
        CollectionAssert.AreEquivalent(
            new[] { "openid", "profile", "email" },
            options.Scope.ToArray());
    }
}
