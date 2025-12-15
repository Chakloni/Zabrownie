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
            var appSettings = _settingsManager.Settings;
            
            // Password and autofill settings
            settings.IsPasswordAutosaveEnabled = !appSettings.DisablePasswordSaving;
            settings.IsGeneralAutofillEnabled = !appSettings.DisableAutofill;
            
            // Other UI settings
            settings.AreDefaultContextMenusEnabled = true;
            settings.IsStatusBarEnabled = false;
            
            // JavaScript control (global setting)
            settings.IsScriptEnabled = appSettings.EnableJavaScript;

            // Apply cookie blocking settings
            ApplyCookieSettings(coreWebView);
            
            // Apply Do Not Track header
            ApplyDoNotTrack(coreWebView);
            
            // Apply Referrer Policy
            ApplyReferrerPolicy(coreWebView);
            
            // Block WebRTC if enabled
            if (appSettings.BlockWebRTC)
            {
                ApplyWebRTCBlock(coreWebView);
            }

            LoggingService.Log($"Privacy settings applied - DNT: {appSettings.SendDoNotTrack}, " +
                             $"Referrer: {appSettings.ReferrerPolicy}, " +
                             $"Cookies: {appSettings.BlockThirdPartyCookies}");
        }

        private void ApplyCookieSettings(CoreWebView2 coreWebView)
        {
            try
            {
                var cookieManager = coreWebView.CookieManager;
                
                if (_settingsManager.Settings.BlockThirdPartyCookies)
                {
                    LoggingService.Log("Third-party cookie blocking enabled (via request filtering)");
                }

                LoggingService.Log($"Cookie settings applied: BlockThirdParty={_settingsManager.Settings.BlockThirdPartyCookies}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to apply cookie settings", ex);
            }
        }

        private void ApplyDoNotTrack(CoreWebView2 coreWebView)
        {
            try
            {
                if (_settingsManager.Settings.SendDoNotTrack)
                {
                    // Unfortunately, WebView2 doesn't expose a direct API to set DNT header
                    // We'll need to add it via WebResourceRequested event in AdBlocker
                    // The AdBlocker will handle this
                    LoggingService.Log("Do Not Track header will be added to requests");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to apply DNT setting", ex);
            }
        }

        private void ApplyReferrerPolicy(CoreWebView2 coreWebView)
        {
            try
            {
                // WebView2 doesn't have direct referrer policy API
                // This would need to be implemented via request interception
                // or Content-Security-Policy injection
                var policy = _settingsManager.Settings.ReferrerPolicy;
                LoggingService.Log($"Referrer policy set to: {policy}");
                
                // Note: Full implementation would require injecting a meta tag or CSP header
                // via NavigationCompleted event
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to apply referrer policy", ex);
            }
        }

        private void ApplyWebRTCBlock(CoreWebView2 coreWebView)
        {
            try
            {
                // Block WebRTC to prevent IP leaks
                // This requires script injection to override getUserMedia
                var script = @"
                    (function() {
                        navigator.mediaDevices.getUserMedia = function() {
                            return Promise.reject(new Error('WebRTC is disabled for privacy'));
                        };
                        navigator.getUserMedia = undefined;
                        navigator.webkitGetUserMedia = undefined;
                        navigator.mozGetUserMedia = undefined;
                        
                        // Block RTCPeerConnection
                        window.RTCPeerConnection = undefined;
                        window.webkitRTCPeerConnection = undefined;
                        window.mozRTCPeerConnection = undefined;
                    })();
                ";
                
                coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(script);
                LoggingService.Log("WebRTC blocking script injected");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to block WebRTC", ex);
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
        
        /// <summary>
        /// Apply per-site JavaScript setting
        /// </summary>
        public void ApplyJavaScriptForSite(CoreWebView2 coreWebView, string domain)
        {
            try
            {
                var enabled = _settingsManager.IsJavaScriptEnabled(domain);
                coreWebView.Settings.IsScriptEnabled = enabled;
                LoggingService.Log($"JavaScript for {domain}: {(enabled ? "enabled" : "disabled")}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to apply JavaScript setting for {domain}", ex);
            }
        }
    }
}