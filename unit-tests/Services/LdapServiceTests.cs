using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Novell.Directory.Ldap;
using NSubstitute;
using sama.Services;
using System;
using System.Collections.Generic;

namespace TestSama.Services
{
    [TestClass]
    public class LdapServiceTests
    {
        private LdapService _service;
        private IConfigurationRoot _configRoot;
        private LdapAuthWrapper _ldap;

        [TestInitialize]
        public void Setup()
        {
            _configRoot = Substitute.For<IConfigurationRoot>();
            _ldap = Substitute.For<LdapAuthWrapper>();

            _service = new LdapService(_configRoot, _ldap);

            _configRoot.GetSection("SAMA").Returns(GetSamaConfigWithLdapDisabled());
        }

        [TestMethod]
        public void IsLdapEnabledShouldReturnEnabledValue()
        {
            Assert.IsFalse(_service.IsLdapEnabled());

            _configRoot.GetSection("SAMA").Returns(GetSamaConfigWithLdapEnabled());

            Assert.IsTrue(_service.IsLdapEnabled());
        }

        [TestMethod]
        public void AuthenticateShouldReturnApplicationUserWhenSuccessful()
        {
            _configRoot.GetSection("SAMA").Returns(GetSamaConfigWithLdapEnabled());
            _ldap.Authenticate("host.example.com", 1234, true, "format1myuserA", "mypass", "somebasedn", "format2myuserB", "somenameattr", Arg.Any<RemoteCertificateValidationCallback>())
                .Returns(new LdapAuthWrapper.LdapUser
                {
                    DisplayName = "the user"
                });

            var result = _service.Authenticate("myuser", "mypass");

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsRemote);
            Assert.AreEqual("the user", result.UserName);
        }

        [TestMethod]
        public void AuthenticateShouldReturnNullWhenUnsuccessful()
        {
            _configRoot.GetSection("SAMA").Returns(GetSamaConfigWithLdapEnabled());
            _ldap.WhenForAnyArgs(w => w.Authenticate("host.example.com", 1234, true, "format1myuserA", "mypass", "somebasedn", "format2myuserB", "somenameattr", Arg.Any<RemoteCertificateValidationCallback>()))
                .Do(c => { throw new Exception(); });

            var result = _service.Authenticate("myuser", "mypass");

            Assert.IsNull(result);
        }

        private IConfigurationSection GetSamaConfigWithLdapDisabled()
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "SAMA:LDAP:Enabled", "false" },
                })
                .Build()
                .GetSection("SAMA");
        }

        private IConfigurationSection GetSamaConfigWithLdapEnabled()
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "SAMA:LDAP:Enabled", "true" },
                    { "SAMA:LDAP:Host", "host.example.com" },
                    { "SAMA:LDAP:Port", "1234" },
                    { "SAMA:LDAP:SSL", "true" },
                    { "SAMA:LDAP:BindDnFormat", "format1{0}A" },
                    { "SAMA:LDAP:SearchBaseDn", "somebasedn" },
                    { "SAMA:LDAP:SearchFilterFormat", "format2{0}B" },
                    { "SAMA:LDAP:NameAttribute", "somenameattr" },
                })
                .Build()
                .GetSection("SAMA");
        }
    }
}
