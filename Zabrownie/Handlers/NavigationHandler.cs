using Zabrownie.Core;
using Zabrownie.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;

namespace Zabrownie.Handlers
{
    public class NavigationHandler
    {
        private readonly TabManager _tabManager;
        private readonly SettingsManager _settingsManager;
        private readonly TextBox _addressBar;
        private readonly TextBlock _statusText;

        public NavigationHandler(
            TabManager tabManager,
            SettingsManager settingsManager,
            TextBox addressBar,
            TextBlock statusText)
        {
            _tabManager = tabManager;
            _settingsManager = settingsManager;
            _addressBar = addressBar;
            _statusText = statusText;
        }

        public void Navigate(string url)
        {
            var webView = _tabManager.ActiveTab?.WebView;
            if (webView?.CoreWebView2 == null)
            {
                Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await Task.Delay(500);
                    Navigate(url);
                });
                _statusText.Text = "Esperando inicialización del navegador...";
                return;
            }

            // Ignore placeholder text
            if (string.IsNullOrWhiteSpace(url) || url == "Escribe URL o busca...")
            {
                LoggingService.Log("Cannot navigate: Empty URL");
                return;
            }

            url = url.Trim();
            LoggingService.Log($"NavigateToUrl called with: {url}");

            try
            {
                string finalUrl = url;

                // Add protocol if missing
                if (!url.StartsWith("http://") &&
                    !url.StartsWith("https://") &&
                    !url.StartsWith("file://") &&
                    url != "about:blank" &&
                    url != "homepage")
                {
                    // Check if it's a search query
                    if (url.Contains(" ") || (!url.Contains(".") && !url.Contains(":")))
                    {
                        finalUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(url)}";
                        LoggingService.Log($"Treating as search query: {finalUrl}");
                    }
                    else
                    {
                        finalUrl = "https://" + url;
                        LoggingService.Log($"Adding https:// prefix: {finalUrl}");
                    }
                }

                // Strip tracking parameters
                finalUrl = _settingsManager.StripTrackingParameters(finalUrl);

                LoggingService.Log($"Final URL to navigate: {finalUrl}");

                // Navigate
                _tabManager.ActiveTab.WebView.CoreWebView2.Navigate(finalUrl);

                // Update tab URL and address bar
                _tabManager.ActiveTab.Url = finalUrl;
                _addressBar.Text = finalUrl;
                _statusText.Text = $"Navegando a {finalUrl}...";

                LoggingService.Log($"Navigation command sent successfully");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Navigation error for URL: {url}", ex);
                _statusText.Text = $"Error de navegación: {ex.Message}";
                MessageBox.Show($"Error al navegar: {ex.Message}", "Error de Navegación",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void GoBack()
        {
            if (_tabManager.ActiveTab?.WebView?.CanGoBack == true)
            {
                _tabManager.ActiveTab.WebView.GoBack();
                LoggingService.Log("Navigated back");
            }
        }

        public void GoForward()
        {
            if (_tabManager.ActiveTab?.WebView?.CanGoForward == true)
            {
                _tabManager.ActiveTab.WebView.GoForward();
                LoggingService.Log("Navigated forward");
            }
        }

        public void Reload()
        {
            if (_tabManager.ActiveTab?.WebView?.CoreWebView2 != null)
            {
                _tabManager.ActiveTab.WebView.Reload();
            }
        }

        public void ChangeZoom(double delta, bool reset = false)
        {
            var webView = _tabManager.ActiveTab?.WebView;
            if (webView?.CoreWebView2 == null) return;

            if (reset)
                webView.ZoomFactor = 1.0;
            else
                webView.ZoomFactor = Math.Clamp(webView.ZoomFactor + delta, 0.25, 5.0);
        }

        public void UpdateNavigationButtons(Button backButton, Button forwardButton)
        {
            backButton.IsEnabled = _tabManager.ActiveTab?.WebView?.CanGoBack ?? false;
            forwardButton.IsEnabled = _tabManager.ActiveTab?.WebView?.CanGoForward ?? false;
        }

        public void OnAddressBarKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox textBox)
            {
                LoggingService.Log($"Enter pressed in AddressBar, text = '{textBox.Text}'");
                Navigate(textBox.Text);
            }
        }

        public void OnAddressBarGotFocus(object sender, RoutedEventArgs e)
        {
            if (_addressBar.Text == "Escribe URL o busca...")
            {
                _addressBar.Text = "";
            }
        }
    }
}