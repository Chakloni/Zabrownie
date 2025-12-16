using Zabrownie.Core;
using Zabrownie.Models;
using Zabrownie.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Zabrownie.Handlers
{
    public class TabHandler
    {
        private readonly TabManager _tabManager;
        private readonly FilterEngine _filterEngine;
        private readonly SettingsManager _settingsManager;
        private readonly WebViewFactory _webViewFactory;
        private readonly Panel _webViewContainer;
        private readonly TextBox _addressBar;
        private readonly Window _mainWindow;
        private readonly NavigationHandler _navigationHandler;
        private readonly Stack<BrowserTab> _closedTabs = new();
        
        private CoreWebView2Environment? _webViewEnvironment;

        public TabHandler(
            TabManager tabManager,
            FilterEngine filterEngine,
            SettingsManager settingsManager,
            WebViewFactory webViewFactory,
            Panel webViewContainer,
            TextBox addressBar,
            Window mainWindow,
            NavigationHandler navigationHandler)
        {
            _tabManager = tabManager;
            _filterEngine = filterEngine;
            _settingsManager = settingsManager;
            _webViewFactory = webViewFactory;
            _webViewContainer = webViewContainer;
            _addressBar = addressBar;
            _mainWindow = mainWindow;
            _navigationHandler = navigationHandler;
        }

        public void SetWebViewEnvironment(CoreWebView2Environment environment)
        {
            _webViewEnvironment = environment;
        }

        public async Task CreateNewTabAsync(string url = "homepage")
        {
            try
            {
                if (_webViewEnvironment == null)
                {
                    LoggingService.Log("ERROR: WebView2 Environment not initialized");
                    return;
                }

                LoggingService.Log($"Creating new tab with URL: {url}");

                var tab = _tabManager.CreateTab(url);
                var webView = new WebView2();

                _webViewContainer.Children.Add(webView);

                // Initialize WebView2
                await webView.EnsureCoreWebView2Async(_webViewEnvironment);

                // Fullscreen handling
                webView.CoreWebView2.ContainsFullScreenElementChanged += (s, e) =>
                {
                    _mainWindow.Dispatcher.Invoke(() =>
                    {
                        var titleBar = _mainWindow.FindName("TitleBar") as UIElement;
                        var bookmarksBar = _mainWindow.FindName("BookmarksBar") as UIElement;
                        var navigationBar = _mainWindow.FindName("NavigationBar") as UIElement;

                        if (webView.CoreWebView2.ContainsFullScreenElement)
                        {
                            if (titleBar != null) titleBar.Visibility = Visibility.Collapsed;
                            if (bookmarksBar != null) bookmarksBar.Visibility = Visibility.Collapsed;
                            if (navigationBar != null) navigationBar.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            if (titleBar != null) titleBar.Visibility = Visibility.Visible;
                            if (bookmarksBar != null) bookmarksBar.Visibility = Visibility.Visible;
                            if (navigationBar != null) navigationBar.Visibility = Visibility.Visible;
                        }
                    });
                };

                // Create AdBlocker for this tab
                var adBlocker = new AdBlocker(_filterEngine, _settingsManager);
                await adBlocker.InitializeAsync();

                _webViewFactory.ApplyPrivacySettings(webView.CoreWebView2);
                adBlocker.AttachToWebView(webView.CoreWebView2);

                // Event handlers
                webView.NavigationStarting += (s, e) => OnNavigationStarting(s, e, tab);
                webView.NavigationCompleted += (s, e) => OnNavigationCompleted(s, e, tab, adBlocker);
                webView.SourceChanged += (s, e) => OnSourceChanged(s, e, tab);

                // User agent
                if (!string.IsNullOrEmpty(_settingsManager.Settings.UserAgent))
                {
                    webView.CoreWebView2.Settings.UserAgent = _settingsManager.Settings.UserAgent;
                }

                tab.WebView = webView;

                // Show tab
                ShowTab(tab);

                // Navigate or show homepage
                if (url == "homepage" || url == "about:blank")
                {
                    RaiseHomepageVisibilityRequest(true);
                }
                else if (!string.IsNullOrWhiteSpace(url))
                {
                    await Task.Delay(100);
                    webView.CoreWebView2.Navigate(url);
                    tab.Url = url;
                    _addressBar.Text = url;
                    RaiseHomepageVisibilityRequest(false);
                }

                LoggingService.Log("Tab created successfully");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to create new tab", ex);
                MessageBox.Show($"Error al crear pestaña: {ex.Message}\n\n{ex.StackTrace}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ShowTab(BrowserTab tab)
        {
            _tabManager.SetActiveTab(tab);
            _webViewContainer.Children.Clear();

            if (tab.WebView != null)
            {
                _webViewContainer.Children.Add(tab.WebView);

                if (string.IsNullOrWhiteSpace(tab.Url) || tab.Url == "about:blank" || tab.Url == "homepage")
                {
                    _addressBar.Text = "Escribe URL o busca...";
                }
                else
                {
                    _addressBar.Text = tab.Url;
                }

                RaiseNavigationStateChanged();
            }
        }

        public void CloseCurrentTab()
        {
            if (_tabManager.ActiveTab != null)
            {
                var tabToClose = _tabManager.ActiveTab;
                _closedTabs.Push(tabToClose);
                _tabManager.CloseTab(tabToClose);

                if (_tabManager.Tabs.Count == 0)
                {
                    CreateNewTabAsync().ConfigureAwait(false);
                }
                else if (_tabManager.ActiveTab != null)
                {
                    ShowTab(_tabManager.ActiveTab);
                }
            }
        }

        public void SwitchToNextTab()
        {
            if (_tabManager.Tabs.Count <= 1 || _tabManager.ActiveTab is not { } activeTab) return;

            int index = _tabManager.Tabs.IndexOf(activeTab);
            int nextIndex = (index + 1) % _tabManager.Tabs.Count;
            ShowTab(_tabManager.Tabs[nextIndex]);
        }

        public void SwitchToPrevTab()
        {
            if (_tabManager.Tabs.Count <= 1 || _tabManager.ActiveTab is not { } activeTab) return;

            int index = _tabManager.Tabs.IndexOf(activeTab);
            int prevIndex = (index - 1 + _tabManager.Tabs.Count) % _tabManager.Tabs.Count;
            ShowTab(_tabManager.Tabs[prevIndex]);
        }

        public async void ReopenLastClosedTab()
        {
            if (_closedTabs.TryPop(out var tab))
            {
                _tabManager.Tabs.Add(tab);
                ShowTab(tab);
                
                if (tab.WebView?.CoreWebView2 != null)
                {
                    tab.WebView.CoreWebView2.Navigate(tab.Url ?? "https://www.google.com");
                }
                else
                {
                    await CreateNewTabAsync(tab.Url);
                }
            }
        }

        // Event handlers
        private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e, BrowserTab tab)
        {
            tab.IsLoading = true;
            LoggingService.Log($"Navigation starting: {e.Uri}");

            if (tab.IsActive)
            {
                var statusText = _mainWindow.FindName("StatusText") as TextBlock;
                if (statusText != null) statusText.Text = $"Cargando: {e.Uri}";
                RaiseNavigationStateChanged();
            }
        }

        private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e, 
            BrowserTab tab, AdBlocker adBlocker)
        {
            tab.IsLoading = false;
            tab.BlockedCount = adBlocker.BlockedCount;

            LoggingService.Log($"Navigation completed: IsSuccess={e.IsSuccess}");

            if (tab.IsActive)
            {
                var statusText = _mainWindow.FindName("StatusText") as TextBlock;
                if (statusText != null)
                {
                    statusText.Text = e.IsSuccess ? "Listo" : $"Error al cargar (Error: {e.WebErrorStatus})";
                }
                RaiseNavigationStateChanged();
            }

            if (tab.WebView?.CoreWebView2 != null)
            {
                var title = tab.WebView.CoreWebView2.DocumentTitle;
                tab.Title = string.IsNullOrEmpty(title) ? "Nueva Pestaña" : title;

                if (e.IsSuccess && !string.IsNullOrEmpty(tab.Url) && 
                    tab.Url != "about:blank" && tab.Url != "homepage")
                {
                    RaiseRecentSiteAdded(tab.Title, tab.Url);
                }
            }
        }

        private void OnSourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e, BrowserTab tab)
        {
            var url = tab.WebView?.Source?.ToString() ?? "";
            tab.Url = url;

            LoggingService.Log($"Source changed to: {url}");

            if (tab.IsActive)
            {
                _addressBar.Text = url;
                RaiseBookmarkStateChanged();

                bool showHomepage = url == "about:blank" || url == "homepage" || string.IsNullOrWhiteSpace(url);
                RaiseHomepageVisibilityRequest(showHomepage);
            }
        }

        // UI event handlers
        public void OnTabClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is BrowserTab tab)
            {
                ShowTab(tab);

                bool showHomepage = tab.Url == "homepage" || tab.Url == "about:blank" || 
                                   string.IsNullOrWhiteSpace(tab.Url);
                RaiseHomepageVisibilityRequest(showHomepage);
            }
        }

        public async void OnNewTabClick(object sender, RoutedEventArgs e)
        {
            await CreateNewTabAsync("homepage");
        }

        public void OnCloseTabClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is BrowserTab tab)
            {
                ((RoutedEventArgs)e).Handled = true;
                _tabManager.CloseTab(tab);

                if (_tabManager.ActiveTab != null)
                {
                    ShowTab(_tabManager.ActiveTab);
                }
                else if (_tabManager.Tabs.Count > 0)
                {
                    ShowTab(_tabManager.Tabs[0]);
                }
            }
        }

        // Events for communication with MainWindow
        public event Action? NavigationStateChanged;
        public event Action? BookmarkStateChanged;
        public event Action<bool>? HomepageVisibilityRequest;
        public event Action<string, string>? RecentSiteAdded;

        private void RaiseNavigationStateChanged() => NavigationStateChanged?.Invoke();
        private void RaiseBookmarkStateChanged() => BookmarkStateChanged?.Invoke();
        private void RaiseHomepageVisibilityRequest(bool show) => HomepageVisibilityRequest?.Invoke(show);
        private void RaiseRecentSiteAdded(string title, string url) => RecentSiteAdded?.Invoke(title, url);
    }
}