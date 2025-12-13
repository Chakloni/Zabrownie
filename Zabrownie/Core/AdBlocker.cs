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
        private string _lastDocumentUrl = "";

        public int BlockedCount => _blockedCount;

        public AdBlocker(FilterEngine filterEngine, SettingsManager settingsManager)
        {
            _filterEngine = filterEngine;
            _settingsManager = settingsManager;
        }

        public async Task InitializeAsync()
        {
            try
            {
                var defaultFiltersPath = FileService.GetDefaultFiltersPath();
                await _filterEngine.LoadFiltersAsync(defaultFiltersPath);

                var customLists = _settingsManager.Settings.CustomFilterLists;
                if (customLists != null && customLists.Count > 0)
                {
                    await _filterEngine.LoadFiltersFromMultipleSourcesAsync(customLists);
                }

                LoggingService.Log($"AdBlocker initialized with {_filterEngine.TotalRules} rules");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to initialize AdBlocker", ex);
            }
        }

        public void AttachToWebView(CoreWebView2 coreWebView)
        {
            coreWebView.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            coreWebView.WebResourceRequested += OnWebResourceRequested;
            
            // Actualizar la URL del documento cuando navegue
            coreWebView.SourceChanged += (s, e) =>
            {
                _lastDocumentUrl = coreWebView.Source?.ToString() ?? "";
            };
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

                if (string.IsNullOrEmpty(url))
                    return;

                // Validar URL
                if (!Uri.TryCreate(url, UriKind.Absolute, out var requestUri))
                    return;

                var domain = requestUri.Host;

                // Verificar whitelist
                if (_settingsManager.IsWhitelisted(domain))
                    return;

                // Obtener la URL del documento principal
                var coreWebView = sender as CoreWebView2;
                var documentUrl = coreWebView?.Source?.ToString() ?? _lastDocumentUrl;

                // Verificar si debe bloquearse
                if (_filterEngine.ShouldBlock(url, documentUrl, e.ResourceContext))
                {
                    // Bloquear la solicitud
                    if (coreWebView?.Environment != null)
                    {
                        e.Response = coreWebView.Environment.CreateWebResourceResponse(
                            null, 403, "Blocked by AdBlocker", "");
                        
                        _blockedCount++;
                        
                        // Log solo para recursos importantes
                        if (e.ResourceContext == CoreWebView2WebResourceContext.Script ||
                            e.ResourceContext == CoreWebView2WebResourceContext.Document ||
                            e.ResourceContext == CoreWebView2WebResourceContext.Stylesheet)
                        {
                            LoggingService.Log($"Blocked [{e.ResourceContext}]: {url}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error in AdBlocker.OnWebResourceRequested for {e.Request.Uri}", ex);
            }
        }

        public void ResetBlockedCount()
        {
            _blockedCount = 0;
        }
    }
}