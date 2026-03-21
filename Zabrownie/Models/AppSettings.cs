using System.Collections.Generic;

namespace Zabrownie.Models
{
    public class AppSettings
    {
        public string Homepage { get; set; } = "https://www.google.com";
        public bool EnableAdBlocking { get; set; } = true;
        public bool EnableTrackerBlocking { get; set; } = true;
        public bool EnableJavaScript { get; set; } = true;
        public bool BookmarksBarShow { get; set; } = true;
        public bool BlockThirdPartyCookies { get; set; } = true;
        public bool StripTrackingParams { get; set; } = true;
        public bool ClearDataOnClose { get; set; } = false;
        public string UserAgent { get; set; } = "";
        public string AccentColor { get; set; } = "#FF006B";
        
        // New Privacy Settings
        public bool SendDoNotTrack { get; set; } = true;
        public bool DisablePasswordSaving { get; set; } = true;
        public bool DisableAutofill { get; set; } = true;
        public string ReferrerPolicy { get; set; } = "no-referrer-when-downgrade"; // Options: "no-referrer", "no-referrer-when-downgrade", "origin", "same-origin", "strict-origin"
        public bool BlockWebRTC { get; set; } = false;
        
        public List<SiteWhitelistEntry> Whitelist { get; set; } = [];
        public List<string> CustomFilterLists { get; set; } = [];
        public Dictionary<string, bool> PerSiteJavaScript { get; set; } = [];
    }
}