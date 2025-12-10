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
        private int _blockedCount;

        public int BlockedCount => _blockedCount;

        public AdBlocker(FilterEngine filterEngine, SettingsManager settingsManager)
        {
            _filterEngine = filterEngine;
            _settingsManager = settingsManager;
        }

        public async Task InitializeAsync()
        {
            var defaultFiltersPath = FileService.GetDefaultFiltersPath();
            await _filterEngine.LoadFiltersAsync(defaultFiltersPath);
            
            var customLists = _settingsManager.Settings.CustomFilterLists;
            if (customLists != null && customLists.Count > 0)
            {
                await _filterEngine.LoadFiltersFromMultipleSourcesAsync(customLists);
            }
        }

        public void AttachToWebView(CoreWebView2 coreWebView)
        {
            coreWebView.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            coreWebView.WebResourceRequested += OnWebResourceRequested;
        }

        public void DetachFromWebView(CoreWebView2 coreWebView)
        {
            coreWebView.WebResourceRequested -= OnWebResourceRequested;
        }

        private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            try
            {
                if (!_settingsManager.Settings.EnableAdBlocking)
                    return;

                var url = e.Request.Uri;
                var requestUri = new Uri(url);
                var domain = requestUri.Host;

                if (_settingsManager.IsWhitelisted(domain))
                    return;

                if (_filterEngine.ShouldBlock(url))
                {
                    var coreWebView = sender as CoreWebView2;
                    if (coreWebView != null)
                    {
                        e.Response = coreWebView.Environment.CreateWebResourceResponse(
                            null, 403, "Blocked", "");
                        
                        _blockedCount++;
                        LoggingService.Log($"Blocked: {url}");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error in AdBlocker.OnWebResourceRequested", ex);
            }
        }

        public void ResetBlockedCount()
        {
            _blockedCount = 0;
        }
    }
}