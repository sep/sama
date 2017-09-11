using Microsoft.VisualStudio.TestTools.UnitTesting;
using Novell.Directory.Ldap;
using NSubstitute;
using sama.Services;
using System;

namespace TestSama.Services
{
    [TestClass]
    public class LdapServiceTests
    {
        private LdapService _service;
        private SettingsService _settingsService;
        private LdapAuthWrapper _ldap;

        [TestInitialize]
        public void Setup()
        {
            _settingsService = Substitute.For<SettingsService>((IServiceProvider)null);
            _ldap = Substitute.For<LdapAuthWrapper>();

            _service = new LdapService(_settingsService, _ldap);

            _settingsService.Ldap_Enable.Returns(false);
        }

        [TestMethod]
        public void IsLdapEnabledShouldReturnEnabledValue()
        {
            Assert.IsFalse(_service.IsLdapEnabled());

            SetUpLdapSettings();

            Assert.IsTrue(_service.IsLdapEnabled());
        }

        [TestMethod]
        public void AuthenticateShouldReturnApplicationUserWhenSuccessful()
        {
            SetUpLdapSettings();
            _ldap.Authenticate("host.example.com", 1234, true, "format1myuserA", "mypass", "somebasedn", "format2myuserB", "somenameattr", Arg.Any<RemoteCertificateValidationCallback>())
                .Returns(new LdapAuthWrapper.LdapUser
                {
                    DisplayName = "the user"
                });

            var result = _service.Authenticate("myuser", "mypass");

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsRemote);
            Assert.AreEqual("the user", result.UserName);
            var userId = result.Id.ToByteArray();
            Assert.AreEqual(0, userId[0]);
            Assert.AreEqual(0, userId[1]);
            Assert.AreEqual(0, userId[2]);
            Assert.AreEqual(0, userId[3]);
        }

        [TestMethod]
        public void AuthenticateShouldReturnNullWhenUnsuccessful()
        {
            SetUpLdapSettings();
            _ldap.WhenForAnyArgs(w => w.Authenticate("", 0, false, "", "", "", "", "", Arg.Any<RemoteCertificateValidationCallback>()))
                .Do(c => { throw new Exception(); });

            var result = _service.Authenticate("myuser", "mypass");

            Assert.IsNull(result);
        }

        private void SetUpLdapSettings()
        {
            _settingsService.Ldap_Enable.Returns(true);
            _settingsService.Ldap_Host.Returns("host.example.com");
            _settingsService.Ldap_Port.Returns(1234);
            _settingsService.Ldap_Ssl.Returns(true);
            _settingsService.Ldap_BindDnFormat.Returns("format1{0}A");
            _settingsService.Ldap_SearchBaseDn.Returns("somebasedn");
            _settingsService.Ldap_SearchFilterFormat.Returns("format2{0}B");
            _settingsService.Ldap_NameAttribute.Returns("somenameattr");
        }
    }
}
