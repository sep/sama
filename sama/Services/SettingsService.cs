using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace sama.Services
{
    public class SettingsService
    {
        private const string CACHE_NAME_SEPARATOR = "%%%CACHESEPARATOR12345%%%";

        private readonly ConcurrentDictionary<string, object> _cache = new ConcurrentDictionary<string, object>();
        private readonly IServiceProvider _provider;

        public SettingsService(IServiceProvider provider)
        {
            _provider = provider;
        }



        public virtual string Notifications_Slack_WebHook
        {
            get { return GetSetting("SlackNotifications", "WebHook", ""); }
            set { SetSetting("SlackNotifications", "WebHook", value); }
        }

        public virtual string Notifications_Graphite_Host
        {
            get { return GetSetting("GraphiteNotifications", "ServerHost", ""); }
            set { SetSetting("GraphiteNotifications", "ServerHost", value); }
        }

        public virtual int Notifications_Graphite_Port
        {
            get { return GetSetting("GraphiteNotifications", "ServerPort", 0); }
            set { SetSetting("GraphiteNotifications", "ServerPort", value); }
        }

        public virtual int Monitor_IntervalSeconds
        {
            get { return GetSetting("Monitor", "IntervalSeconds", 90); }
            set { SetSetting("Monitor", "IntervalSeconds", value); }
        }

        public virtual int Monitor_MaxRetries
        {
            get { return GetSetting("Monitor", "MaxRetries", 1); }
            set { SetSetting("Monitor", "MaxRetries", value); }
        }

        public virtual int Monitor_SecondsBetweenTries
        {
            get { return GetSetting("Monitor", "SecondsBetweenTries", 1); }
            set { SetSetting("Monitor", "SecondsBetweenTries", value); }
        }

        public virtual int Monitor_RequestTimeoutSeconds
        {
            get { return GetSetting("Monitor", "RequestTimeoutSeconds", 15); }
            set { SetSetting("Monitor", "RequestTimeoutSeconds", value); }
        }

        public virtual bool Ldap_Enable
        {
            get { return GetSetting("LDAP", "Enable", false); }
            set { SetSetting("LDAP", "Enable", value); }
        }

        public virtual string Ldap_Host
        {
            get { return GetSetting("LDAP", "ServerHost", ""); }
            set { SetSetting("LDAP", "ServerHost", value); }
        }

        public virtual int Ldap_Port
        {
            get { return GetSetting("LDAP", "ServerPort", 0); }
            set { SetSetting("LDAP", "ServerPort", value); }
        }

        public virtual bool Ldap_Ssl
        {
            get { return GetSetting("LDAP", "SSL", true); }
            set { SetSetting("LDAP", "SSL", value); }
        }

        public virtual string Ldap_BindDnFormat
        {
            get { return GetSetting("LDAP", "BindDNFormat", ""); }
            set { SetSetting("LDAP", "BindDNFormat", value); }
        }

        public virtual string Ldap_SearchBaseDn
        {
            get { return GetSetting("LDAP", "SearchBaseDN", ""); }
            set { SetSetting("LDAP", "SearchBaseDN", value); }
        }

        public virtual string Ldap_SearchFilterFormat
        {
            get { return GetSetting("LDAP", "SearchFilterFormat", ""); }
            set { SetSetting("LDAP", "SearchFilterFormat", value); }
        }

        public virtual string Ldap_NameAttribute
        {
            get { return GetSetting("LDAP", "NameAttribute", ""); }
            set { SetSetting("LDAP", "NameAttribute", value); }
        }

        public virtual bool Ldap_SslIgnoreValidity
        {
            get { return GetSetting("LDAP", "SSLIgnoreValidity", false); }
            set { SetSetting("LDAP", "SSLIgnoreValidity", value); }
        }

        public virtual string Ldap_SslValidCert
        {
            get { return GetSetting("LDAP", "SSLValidCert", ""); }
            set { SetSetting("LDAP", "SSLValidCert", value); }
        }



        private T GetSetting<T>(string section, string name, T defaultValue)
        {
            if (_cache.TryGetValue(section + CACHE_NAME_SEPARATOR + name, out object value))
            {
                return (T)value;
            }

            using (var scope = _provider.CreateScope())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
            {
                var setting = dbContext.Settings.FirstOrDefault(s => s.Section == section.ToLowerInvariant() && s.Name == name.ToLowerInvariant());
                if (setting != null)
                {
                    var returnValue = Convert.ChangeType(setting.Value, typeof(T));
                    _cache.AddOrUpdate(section + CACHE_NAME_SEPARATOR + name, returnValue, (key, oldValue) => returnValue);
                    return (T)returnValue;
                }

                return defaultValue;
            }
        }

        private void SetSetting<T>(string section, string name, T value)
        {
            var valueString = (string)Convert.ChangeType(value, TypeCode.String);

            if (string.IsNullOrWhiteSpace(valueString))
            {
                _cache.TryRemove(section + CACHE_NAME_SEPARATOR + name, out object _);
            }
            else
            {
                _cache.AddOrUpdate(section + CACHE_NAME_SEPARATOR + name, value, (key, oldValue) => value);
            }

            using (var scope = _provider.CreateScope())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
            {
                var setting = dbContext.Settings.FirstOrDefault(s => s.Section == section.ToLowerInvariant() && s.Name == name.ToLowerInvariant());
                if (setting != null)
                {
                    if (!string.IsNullOrWhiteSpace(valueString))
                    {
                        setting.Value = valueString;
                        dbContext.Settings.Update(setting);
                    }
                    else
                    {
                        dbContext.Settings.Remove(setting);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(valueString))
                {
                    setting = new Models.Setting { Id = Guid.NewGuid(), Section = section.ToLowerInvariant(), Name = name.ToLowerInvariant(), Value = valueString };
                    dbContext.Settings.Add(setting);
                }

                dbContext.SaveChanges();
            }
        }
    }
}
