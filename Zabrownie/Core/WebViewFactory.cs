using Zabrownie.Services;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Zabrownie.Core
{
    public class WebViewFactory
    {
        private readonly SettingsManager _settingsManager;
        private readonly AdBlocker _adBlocker;
        private static readonly string UserDataFolder = Path.Combine(
            FileService.GetAppDataPath(), "WebView2Data");

        public WebViewFactory(SettingsManager settingsManager, AdBlocker adBlocker)
        {
            _settingsManager = settingsManager;
            _adBlocker = adBlocker;
        }

        public void ApplyPrivacySettings(CoreWebView2 coreWebView)
        {
            var settings = coreWebView.Settings;
            
            settings.IsPasswordAutosaveEnabled = false;
            settings.IsGeneralAutofillEnabled = false;
            settings.AreDefaultContextMenusEnabled = true;
            settings.IsStatusBarEnabled = false;
            settings.IsScriptEnabled = _settingsManager.Settings.EnableJavaScript;

            LoggingService.Log("Privacy settings applied to WebView2");
        }

        public async Task ClearBrowsingDataAsync(CoreWebView2 coreWebView)
        {
            try
            {
                await coreWebView.Profile.ClearBrowsingDataAsync();
                LoggingService.Log("Browsing data cleared");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to clear browsing data", ex);
            }
        }
    }
}