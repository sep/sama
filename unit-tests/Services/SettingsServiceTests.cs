using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using sama;
using sama.Services;
using System;
using System.Linq;

namespace TestSama.Services
{
    [TestClass]
    public class SettingsServiceTests
    {
        private IServiceProvider _provider;
        private SettingsService _service;

        [TestInitialize]
        public void Setup()
        {
            _provider = TestUtility.InitDI();
            _service = new SettingsService(_provider);
        }

        [TestMethod]
        public void ShouldGetDefaultSettings()
        {
            Assert.AreEqual("", _service.Notifications_Slack_WebHook);
            Assert.AreEqual("", _service.Notifications_Graphite_Host);
            Assert.AreEqual(0, _service.Notifications_Graphite_Port);
            Assert.AreEqual(90, _service.Monitor_IntervalSeconds);
            Assert.AreEqual(1, _service.Monitor_MaxRetries);
            Assert.AreEqual(15, _service.Monitor_RequestTimeoutSeconds);
            Assert.AreEqual(1, _service.Monitor_SecondsBetweenTries);
            Assert.AreEqual(false, _service.Ldap_Enable);
            Assert.AreEqual("", _service.Ldap_Host);
            Assert.AreEqual(0, _service.Ldap_Port);
            Assert.AreEqual(true, _service.Ldap_Ssl);
            Assert.AreEqual("", _service.Ldap_BindDnFormat);
            Assert.AreEqual("", _service.Ldap_SearchBaseDn);
            Assert.AreEqual("", _service.Ldap_SearchFilterFormat);
            Assert.AreEqual("", _service.Ldap_NameAttribute);
        }

        [TestMethod]
        public void ShouldGetChangedSettings()
        {
            _service.Notifications_Slack_WebHook = "a";
            _service.Notifications_Graphite_Host = "abc";
            _service.Notifications_Graphite_Port = 5000;
            _service.Monitor_IntervalSeconds = 100;
            _service.Monitor_MaxRetries = 200;
            _service.Monitor_RequestTimeoutSeconds = 300;
            _service.Monitor_SecondsBetweenTries = 400;
            _service.Ldap_Enable = true;
            _service.Ldap_Host = "b";
            _service.Ldap_Port = 500;
            _service.Ldap_Ssl = false;
            _service.Ldap_BindDnFormat = "c";
            _service.Ldap_SearchBaseDn = "d";
            _service.Ldap_SearchFilterFormat = "e";
            _service.Ldap_NameAttribute = "f";

            Assert.AreEqual("a", _service.Notifications_Slack_WebHook);
            Assert.AreEqual("abc", _service.Notifications_Graphite_Host);
            Assert.AreEqual(5000, _service.Notifications_Graphite_Port);
            Assert.AreEqual(100, _service.Monitor_IntervalSeconds);
            Assert.AreEqual(200, _service.Monitor_MaxRetries);
            Assert.AreEqual(300, _service.Monitor_RequestTimeoutSeconds);
            Assert.AreEqual(400, _service.Monitor_SecondsBetweenTries);
            Assert.AreEqual(true, _service.Ldap_Enable);
            Assert.AreEqual("b", _service.Ldap_Host);
            Assert.AreEqual(500, _service.Ldap_Port);
            Assert.AreEqual(false, _service.Ldap_Ssl);
            Assert.AreEqual("c", _service.Ldap_BindDnFormat);
            Assert.AreEqual("d", _service.Ldap_SearchBaseDn);
            Assert.AreEqual("e", _service.Ldap_SearchFilterFormat);
            Assert.AreEqual("f", _service.Ldap_NameAttribute);
        }

        [TestMethod]
        public void ShouldCacheSettingsOnRetrieval()
        {
            using (var scope = _provider.CreateScope())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
            {
                dbContext.Settings.Add(new sama.Models.Setting { Section = "slacknotifications", Name = "webhook", Value = "asdf" });
                dbContext.SaveChanges();
            }

            Assert.AreEqual("asdf", _service.Notifications_Slack_WebHook);

            using (var scope = _provider.CreateScope())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
            {
                dbContext.Settings.RemoveRange(dbContext.Settings.ToList());
                dbContext.Settings.Add(new sama.Models.Setting { Section = "slacknotifications", Name = "webhook", Value = "fdsa" });
                dbContext.SaveChanges();
            }

            Assert.AreEqual("asdf", _service.Notifications_Slack_WebHook);
        }

        [TestMethod]
        public void ShouldCacheSettingsOnSave()
        {
            _service.Monitor_IntervalSeconds = 123;

            using (var scope = _provider.CreateScope())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
            {
                var setting = dbContext.Settings.First();
                Assert.AreEqual("monitor", setting.Section);
                Assert.AreEqual("intervalseconds", setting.Name);
                Assert.AreEqual("123", setting.Value);

                setting.Value = "321";
                dbContext.Settings.Update(setting);
                dbContext.SaveChanges();
            }

            Assert.AreEqual(123, _service.Monitor_IntervalSeconds);
        }

        [TestMethod]
        public void ShouldUpdateCache()
        {
            _service.Notifications_Slack_WebHook = "asdf";

            Assert.AreEqual("asdf", _service.Notifications_Slack_WebHook);

            _service.Notifications_Slack_WebHook = "";

            Assert.AreEqual("", _service.Notifications_Slack_WebHook);

            _service.Notifications_Slack_WebHook = "fdsa";

            Assert.AreEqual("fdsa", _service.Notifications_Slack_WebHook);
        }
    }
}
