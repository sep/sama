using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using sama.Models;
using sama.Services;
using System;

namespace sama.Controllers
{
    [Authorize]
    [ResponseCache(NoStore = true)]
    public class SettingsController : Controller
    {
        private readonly SettingsService _settingsService;
        private readonly IServiceProvider _provider;

        public SettingsController(SettingsService settingsService, IServiceProvider provider)
        {
            _settingsService = settingsService;
            _provider = provider;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = new SettingsViewModel
            {
                MonitorIntervalSeconds = _settingsService.Monitor_IntervalSeconds,
                MonitorMaxRetries = _settingsService.Monitor_MaxRetries,
                MonitorSecondsBetweenTries = _settingsService.Monitor_SecondsBetweenTries,
                MonitorRequestTimeoutSeconds = _settingsService.Monitor_RequestTimeoutSeconds,

                SlackWebHook = _settingsService.Notifications_Slack_WebHook,

                GraphiteHost = _settingsService.Notifications_Graphite_Host,
                GraphitePort = _settingsService.Notifications_Graphite_Port,

                SqlServerConnection = _settingsService.Notifications_SqlServer_Connection,
                SqlServerTable = _settingsService.Notifications_SqlServer_TableName,
                SqlServerCreateTableScript = SqlServerNotificationService.CREATE_TABLE_SCRIPT.Trim(),

                LdapEnable = _settingsService.Ldap_Enable,
                LdapHost = _settingsService.Ldap_Host,
                LdapPort = _settingsService.Ldap_Port,
                LdapSsl = _settingsService.Ldap_Ssl,
                LdapBindDnFormat = _settingsService.Ldap_BindDnFormat,
                LdapSearchBaseDn = _settingsService.Ldap_SearchBaseDn,
                LdapSearchFilterFormat = _settingsService.Ldap_SearchFilterFormat,
                LdapNameAttribute = _settingsService.Ldap_NameAttribute,
                LdapSslIgnoreValidity = _settingsService.Ldap_SslIgnoreValidity,
                LdapSslValidCert = _settingsService.Ldap_SslValidCert,
            };

            return View(model);
        }

        [HttpPost]
        public IActionResult Index(SettingsViewModel vm)
        {
            if (vm.LdapEnable)
            {
                if (string.IsNullOrWhiteSpace(vm.LdapHost)) ModelState.AddModelError(nameof(vm.LdapHost), "This field cannot be blank when LDAP is enabled.");
                if (vm.LdapPort < 1) ModelState.AddModelError(nameof(vm.LdapPort), "This field must be a valid port number when LDAP is enabled.");
                if (string.IsNullOrWhiteSpace(vm.LdapBindDnFormat)) ModelState.AddModelError(nameof(vm.LdapBindDnFormat), "This field cannot be blank when LDAP is enabled.");
                if (string.IsNullOrWhiteSpace(vm.LdapSearchBaseDn)) ModelState.AddModelError(nameof(vm.LdapSearchBaseDn), "This field cannot be blank when LDAP is enabled.");
                if (string.IsNullOrWhiteSpace(vm.LdapSearchFilterFormat)) ModelState.AddModelError(nameof(vm.LdapSearchFilterFormat), "This field cannot be blank when LDAP is enabled.");
                if (string.IsNullOrWhiteSpace(vm.LdapNameAttribute)) ModelState.AddModelError(nameof(vm.LdapNameAttribute), "This field cannot be blank when LDAP is enabled.");

                if (ModelState.ErrorCount > 0)
                {
                    return View(vm);
                }
            }

            if (ModelState.IsValid)
            {
                if (_settingsService.Monitor_IntervalSeconds != vm.MonitorIntervalSeconds)
                {
                    _settingsService.Monitor_IntervalSeconds = vm.MonitorIntervalSeconds;
                    _provider.GetRequiredService<MonitorJob>().ReloadSchedule(_provider);
                }

                _settingsService.Monitor_MaxRetries = vm.MonitorMaxRetries;
                _settingsService.Monitor_SecondsBetweenTries = vm.MonitorSecondsBetweenTries;
                _settingsService.Monitor_RequestTimeoutSeconds = vm.MonitorRequestTimeoutSeconds;

                _settingsService.Notifications_Slack_WebHook = vm.SlackWebHook;

                _settingsService.Notifications_Graphite_Host = vm.GraphiteHost;
                _settingsService.Notifications_Graphite_Port = vm.GraphitePort;

                _settingsService.Notifications_SqlServer_Connection = vm.SqlServerConnection;
                _settingsService.Notifications_SqlServer_TableName = vm.SqlServerTable;

                _settingsService.Ldap_Enable = vm.LdapEnable;
                _settingsService.Ldap_Host = vm.LdapHost;
                _settingsService.Ldap_Port = vm.LdapPort;
                _settingsService.Ldap_Ssl = vm.LdapSsl;
                _settingsService.Ldap_BindDnFormat = vm.LdapBindDnFormat;
                _settingsService.Ldap_SearchBaseDn = vm.LdapSearchBaseDn;
                _settingsService.Ldap_SearchFilterFormat = vm.LdapSearchFilterFormat;
                _settingsService.Ldap_NameAttribute = vm.LdapNameAttribute;
                _settingsService.Ldap_SslIgnoreValidity = vm.LdapSslIgnoreValidity;
                _settingsService.Ldap_SslValidCert = vm.LdapSslValidCert;

                TempData["GlobalFlashType"] = "success";
                TempData["GlobalFlashMessage"] = "Settings were saved successfully!";
                return RedirectToAction(nameof(Index));
            }

            return View(vm);
        }
    }
}
