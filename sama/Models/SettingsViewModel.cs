using System.ComponentModel.DataAnnotations;

namespace sama.Models
{
    public class SettingsViewModel
    {
        [Key]
        public int UnusedPrimaryKey { get; set; }


        [Range(5, 3600), Display(Name = "Monitor interval (seconds)")]
        public int MonitorIntervalSeconds { get; set; }

        [Range(0, 50), Display(Name = "Maximum number of retries")]
        public int MonitorMaxRetries { get; set; }

        [Range(0, 60), Display(Name = "Interval between tries (seconds)")]
        public int MonitorSecondsBetweenTries { get; set; }

        [Range(1, 600), Display(Name = "HTTP request timeout (seconds)")]
        public int MonitorRequestTimeoutSeconds { get; set; }


        [Display(Name = "Slack Webhook URL")]
        public string SlackWebHook { get; set; }


        [Display(Name = "Graphite server hostname")]
        public string GraphiteHost { get; set; }
        
        [Range(0, 65535), Display(Name = "Graphite server port")]
        public int GraphitePort { get; set; }


        [Display(Name = "Enable LDAP")]
        public bool LdapEnable { get; set; }

        [Display(Name = "LDAP server hostname")]
        public string LdapHost { get; set; }

        [Range(0, 65535), Display(Name = "LDAP server port")]
        public int LdapPort { get; set; }

        [Display(Name = "Use SSL for LDAP")]
        public bool LdapSsl { get; set; }

        [RegularExpression(@"^.*\{0\}.*$", ErrorMessage = "The string replacement token must be present."), Display(Name = "Bind DN format")]
        public string LdapBindDnFormat { get; set; }

        [Display(Name = "Base DN for user search")]
        public string LdapSearchBaseDn { get; set; }

        [RegularExpression(@"^.*\{0\}.*$", ErrorMessage = "The string replacement token must be present."), Display(Name = "LDAP search filter format")]
        public string LdapSearchFilterFormat { get; set; }

        [Display(Name = "LDAP name attribute")]
        public string LdapNameAttribute { get; set; }
    }
}
