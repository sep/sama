using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama.Controllers;
using sama.Models;
using sama.Services;
using System;

namespace TestSama.Controllers
{
    [TestClass]
    public class SettingsControllerTests
    {
        private IServiceProvider _provider;
        private SettingsService _settingsService;
        private SettingsController _controller;

        [TestInitialize]
        public void Setup()
        {
            _provider = TestUtility.InitDI();
            _settingsService = Substitute.For<SettingsService>((IServiceProvider)null);

            _controller = new SettingsController(_settingsService, _provider)
            {
                TempData = Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionary>()
            };
        }

        [TestMethod]
        public void ShouldValidateLdapWhenEnabled()
        {
            var vm = new SettingsViewModel
            {
                LdapEnable = true
            };

            var result = _controller.Index(vm) as ViewResult;
            Assert.IsNotNull(result);
            Assert.AreEqual(7, _controller.ModelState.ErrorCount);
            Assert.AreEqual("When LDAP is enabled, all LDAP fields are required.", _controller.ModelState[""].Errors[0].ErrorMessage);
        }

        [TestMethod]
        public void ShouldNotValidateLdapWhenDisabled()
        {
            var vm = new SettingsViewModel
            {
                LdapEnable = false
            };

            var result = _controller.Index(vm) as RedirectToActionResult;
            Assert.IsNotNull(result);
            Assert.AreEqual("success", _controller.TempData["GlobalFlashType"]);
        }

        [TestMethod]
        public void ShouldRestartScheduleWhenChangingMonitorInterval()
        {
            var vm = new SettingsViewModel
            {
                MonitorIntervalSeconds = 90
            };
            
            var result = _controller.Index(vm) as RedirectToActionResult;
            Assert.IsNotNull(result);
            _provider.GetRequiredService<MonitorJob>().Received().ReloadSchedule(_provider);
        }
    }
}
