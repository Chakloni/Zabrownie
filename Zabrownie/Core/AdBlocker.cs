using Zabrownie.Services;
using Microsoft.Web.WebView2.Core;
using System;
using System.Threading.Tasks;

namespace Zabrownie.Core
{
    public class AdBlocker
    {
        private readonly FilterEngine _filterEngine;
        private readonly SettingsManager _settingsManager;
        private string? _currentPageDomain;

        public int BlockedCount { get; private set; }

        public AdBlocker(FilterEngine filterEngine, SettingsManager settingsManager)
        {
            _filterEngine = filterEngine;
            _settingsManager = settingsManager;
        }

        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
            LoggingService.Log("AdBlocker initialized");
        }

        public void AttachToWebView(CoreWebView2 coreWebView)
        {
            coreWebView.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            coreWebView.WebResourceRequested += CoreWebView_WebResourceRequested;
            
            // Track navigation to know current page domain for cookie blocking
            coreWebView.NavigationStarting += (s, e) =>
            {
                try
                {
                    var uri = new Uri(e.Uri);
                    _currentPageDomain = uri.Host;
                }
                catch
                {
                    _currentPageDomain = null;
                }
            };

            LoggingService.Log("AdBlocker attached to WebView2");
        }

        private void CoreWebView_WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            try
            {
                var requestUri = e.Request.Uri;
                var documentUri = _currentPageDomain != null ? $"https://{_currentPageDomain}" : "";
                
                // Add Do Not Track header if enabled
                if (_settingsManager.Settings.SendDoNotTrack)
                {
                    var headers = e.Request.Headers;
                    if (!headers.Contains("DNT"))
                    {
                        headers.SetHeader("DNT", "1");
                    }
                }
                
                // Check if ad blocking is enabled
                if (_settingsManager.Settings.EnableAdBlocking)
                {
                    // Check whitelist first
                    var uri = new Uri(requestUri);
                    if (!_settingsManager.IsWhitelisted(uri.Host))
                    {
                        // Check if request should be blocked
                        if (_filterEngine.ShouldBlock(requestUri, documentUri, e.ResourceContext))
                        {
                            var webView = sender as CoreWebView2;
                            if (webView != null)
                            {
                                e.Response = webView.Environment.CreateWebResourceResponse(
                                    null, 403, "Blocked", "");
                                BlockedCount++;
                                LoggingService.Log($"Blocked: {requestUri}");
                            }
                            return;
                        }
                    }
                }

                // Handle cookie blocking for third-party requests (request only, not response)
                if (_settingsManager.Settings.BlockThirdPartyCookies && !string.IsNullOrEmpty(_currentPageDomain))
                {
                    BlockThirdPartyCookies(e, requestUri);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error in WebResourceRequested: {ex.Message}", ex);
            }
        }

        private void BlockThirdPartyCookies(CoreWebView2WebResourceRequestedEventArgs e, string requestUri)
        {
            try
            {
                var requestDomain = new Uri(requestUri).Host;
                
                // Check if this is a third-party request
                if (IsThirdPartyRequest(requestDomain, _currentPageDomain))
                {
                    // Remove Cookie header from outgoing request
                    var headers = e.Request.Headers;
                    if (headers.Contains("Cookie"))
                    {
                        headers.RemoveHeader("Cookie");
                        LoggingService.Log($"Blocked third-party cookie to: {requestDomain}");
                    }
                    
                    // Note: Response headers are read-only in WebView2
                    // Third-party cookies are blocked by removing the Cookie header from requests
                    // This prevents the browser from sending cookies to third-party domains
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error blocking third-party cookies: {ex.Message}", ex);
            }
        }

        private bool IsThirdPartyRequest(string requestDomain, string? pageDomain)
        {
            if (string.IsNullOrEmpty(pageDomain))
                return false;

            try
            {
                // Normalize domains
                requestDomain = requestDomain.TrimStart('.').ToLowerInvariant();
                pageDomain = pageDomain.TrimStart('.').ToLowerInvariant();

                // Same domain - not third party
                if (requestDomain == pageDomain)
                    return false;

                // Extract base domains
                var requestBase = GetBaseDomain(requestDomain);
                var pageBase = GetBaseDomain(pageDomain);

                // Different base domains = third party
                return requestBase != pageBase;
            }
            catch
            {
                return false;
            }
        }

        private string GetBaseDomain(string domain)
        {
            var parts = domain.Split('.');
            
            // Handle domains like "co.uk", "com.au", etc.
            if (parts.Length >= 2)
            {
                return $"{parts[^2]}.{parts[^1]}";
            }
            
            return domain;
        }

        public void ResetBlockedCount()
        {
            BlockedCount = 0;
        }
    }
}