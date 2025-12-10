using System.Collections.Generic;

namespace Zabrownie.Models
{
    public class AppSettings
    {
        public string Homepage { get; set; } = "https://www.google.com";
        public bool EnableAdBlocking { get; set; } = true;
        public bool EnableJavaScript { get; set; } = true;
        public bool BlockThirdPartyCookies { get; set; } = true;
        public bool StripTrackingParams { get; set; } = true;
        public bool ClearDataOnClose { get; set; } = false;
        public string UserAgent { get; set; } = "";
        public List<SiteWhitelistEntry> Whitelist { get; set; } = new List<SiteWhitelistEntry>();
        public List<string> CustomFilterLists { get; set; } = new List<string>();
        public Dictionary<string, bool> PerSiteJavaScript { get; set; } = new Dictionary<string, bool>();
    }
}