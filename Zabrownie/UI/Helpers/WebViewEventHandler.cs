using Microsoft.Web.WebView2.Core;
using System;
using Zabrownie.Models;
using Zabrownie.Services;
using Zabrownie.Core;

namespace Zabrownie.UI.Helpers
{
    public class WebViewEventHandler
    {
        public event EventHandler<string>? NavigationStarted;
        public event EventHandler<(bool IsSuccess, string Title, string Url, int ErrorStatus, int BlockedCount)>? NavigationCompleted;
        public event EventHandler<string>? SourceChanged;

        private readonly BrowserTab _tab;
        private readonly AdBlocker _adBlocker;

        public WebViewEventHandler(BrowserTab tab, AdBlocker adBlocker)
        {
            _tab = tab ?? throw new ArgumentNullException(nameof(tab));
            _adBlocker = adBlocker ?? throw new ArgumentNullException(nameof(adBlocker));
        }

        public void Attach(Microsoft.Web.WebView2.Wpf.WebView2 webView)
        {
            if (webView == null) return;
            webView.NavigationStarting += WebView_NavigationStarting;
            webView.NavigationCompleted += WebView_NavigationCompleted;
            webView.SourceChanged += WebView_SourceChanged;
        }

        private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            _tab.IsLoading = true;
            LoggingService.Log($"Navigation starting: {e.Uri}");
            NavigationStarted?.Invoke(this, e.Uri);
        }

        private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            _tab.IsLoading = false;
            _tab.BlockedCount = _adBlocker.BlockedCount;

            var webView = sender as Microsoft.Web.WebView2.Wpf.WebView2;
            string title = webView?.CoreWebView2?.DocumentTitle ?? "Nueva Pestaña";
            if (string.IsNullOrEmpty(title)) title = "Nueva Pestaña";
            
            _tab.Title = title;
            LoggingService.Log($"Navigation completed: IsSuccess={e.IsSuccess}, Title={title}");

            NavigationCompleted?.Invoke(this, (e.IsSuccess, title, _tab.Url ?? "", (int)e.WebErrorStatus, _adBlocker.BlockedCount));
        }

        private void WebView_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
        {
            var webView = sender as Microsoft.Web.WebView2.Wpf.WebView2;
            var url = webView?.Source?.ToString() ?? "";
            _tab.Url = url;

            LoggingService.Log($"Source changed to: {url}");
            SourceChanged?.Invoke(this, url);
        }
    }
}
