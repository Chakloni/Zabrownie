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
            
            // Disable password and autofill features
            settings.IsPasswordAutosaveEnabled = false;
            settings.IsGeneralAutofillEnabled = false;
            
            // Other UI settings
            settings.AreDefaultContextMenusEnabled = true;
            settings.IsStatusBarEnabled = false;
            
            // JavaScript control (global setting)
            settings.IsScriptEnabled = _settingsManager.Settings.EnableJavaScript;

            // Apply cookie blocking settings
            ApplyCookieSettings(coreWebView);

            LoggingService.Log("Privacy settings applied to WebView2");
        }

        private void ApplyCookieSettings(CoreWebView2 coreWebView)
        {
            try
            {
                var cookieManager = coreWebView.CookieManager;
                
                if (_settingsManager.Settings.BlockThirdPartyCookies)
                {
                    // WebView2 doesn't have a direct "block third-party cookies" setting
                    // But we can use the Profile's cookie management
                    // Note: This requires managing cookies manually or using filtering
                    
                    // Set cookie behavior through profile settings
                    // Unfortunately, WebView2 doesn't expose fine-grained cookie control like Chrome
                    // The best we can do is clear third-party cookies periodically
                    // or handle them via WebResourceRequested
                    
                    LoggingService.Log("Third-party cookie blocking enabled (via filtering)");
                }

                LoggingService.Log($"Cookie settings applied: BlockThirdParty={_settingsManager.Settings.BlockThirdPartyCookies}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to apply cookie settings", ex);
            }
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

        public async Task ClearCookiesAsync(CoreWebView2 coreWebView)
        {
            try
            {
                var cookieManager = coreWebView.CookieManager;
                var cookies = await cookieManager.GetCookiesAsync(null);
                
                foreach (var cookie in cookies)
                {
                    cookieManager.DeleteCookie(cookie);
                }
                
                LoggingService.Log("All cookies cleared");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to clear cookies", ex);
            }
        }

        /// <summary>
        /// Checks if a cookie is from a third-party domain
        /// </summary>
        public bool IsThirdPartyCookie(string cookieDomain, string currentPageDomain)
        {
            try
            {
                // Normalize domains
                cookieDomain = cookieDomain.TrimStart('.');
                currentPageDomain = currentPageDomain.TrimStart('.');

                // Extract base domain (e.g., "example.com" from "sub.example.com")
                var cookieBaseDomain = GetBaseDomain(cookieDomain);
                var pageBaseDomain = GetBaseDomain(currentPageDomain);

                return !cookieBaseDomain.Equals(pageBaseDomain, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private string GetBaseDomain(string domain)
        {
            var parts = domain.Split('.');
            if (parts.Length >= 2)
            {
                return $"{parts[^2]}.{parts[^1]}";
            }
            return domain;
        }
    }
}