using Zabrownie.Core;
using Zabrownie.Models;
using Zabrownie.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Zabrownie.UI
{
    public partial class MainWindow : Window
    {
        private readonly SettingsManager _settingsManager;
        private readonly FilterEngine _filterEngine;
        private readonly TabManager _tabManager;
        private readonly BookmarkManager _bookmarkManager;
        private readonly WebViewFactory _webViewFactory;

        public MainWindow()
        {
            InitializeComponent();
            
            _settingsManager = new SettingsManager();
            _filterEngine = new FilterEngine();
            _tabManager = new TabManager();
            _bookmarkManager = new BookmarkManager();
            
            // Use a single AdBlocker shared across all tabs
            var adBlocker = new AdBlocker(_filterEngine, _settingsManager);
            _webViewFactory = new WebViewFactory(_settingsManager, adBlocker);
            
            TabsControl.DataContext = _tabManager.Tabs;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await _settingsManager.LoadAsync();
                await _bookmarkManager.LoadAsync();
                await CreateDefaultFiltersIfNeeded();
                
                // Load bookmarks bar
                UpdateBookmarksBar();
                
                // Create initial tab
                await CreateNewTabAsync(_settingsManager.Settings.Homepage);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to initialize browser", ex);
                MessageBox.Show($"Failed to initialize browser: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task CreateDefaultFiltersIfNeeded()
        {
            var filtersPath = FileService.GetDefaultFiltersPath();
            if (!System.IO.File.Exists(filtersPath))
            {
                var defaultRules = new[]
                {
                    "! Default ad-blocking rules",
                    "||doubleclick.net^",
                    "||googleadservices.com^",
                    "||googlesyndication.com^",
                    "||google-analytics.com^",
                    "||facebook.com/tr^",
                    "||facebook.net/tr^",
                    "/ads.js",
                    "/advertisement.",
                    "/banner.",
                    "ad-banner",
                    "ad_banner",
                    "/adserver.",
                    "||ads.twitter.com^",
                    "||static.ads-twitter.com^"
                };

                await FileService.SaveTextFileAsync(filtersPath, defaultRules);
            }
            
            await _filterEngine.LoadFiltersAsync(filtersPath);
        }

        private async System.Threading.Tasks.Task CreateNewTabAsync(string url = "about:blank")
        {
            var tab = _tabManager.CreateTab(url);
            
            var webView = new WebView2();
            await webView.EnsureCoreWebView2Async();
            
            // Create dedicated AdBlocker for this tab
            var adBlocker = new AdBlocker(_filterEngine, _settingsManager);
            await adBlocker.InitializeAsync();
            
            _webViewFactory.ApplyPrivacySettings(webView.CoreWebView2);
            adBlocker.AttachToWebView(webView.CoreWebView2);
            
            webView.NavigationStarting += (s, e) => WebView_NavigationStarting(s, e, tab);
            webView.NavigationCompleted += (s, e) => WebView_NavigationCompleted(s, e, tab, adBlocker);
            webView.SourceChanged += (s, e) => WebView_SourceChanged(s, e, tab);
            
            if (!string.IsNullOrEmpty(_settingsManager.Settings.UserAgent))
            {
                webView.CoreWebView2.Settings.UserAgent = _settingsManager.Settings.UserAgent;
            }
            
            tab.WebView = webView;
            
            // Show this tab
            ShowTab(tab);
            
            // Navigate
            if (!string.IsNullOrWhiteSpace(url) && url != "about:blank")
            {
                NavigateToUrl(url);
            }
        }

        private void ShowTab(BrowserTab tab)
        {
            _tabManager.SetActiveTab(tab);
            
            // Clear container
            WebViewContainer.Children.Clear();
            
            // Add active tab's WebView
            if (tab.WebView != null)
            {
                WebViewContainer.Children.Add(tab.WebView);
                AddressBar.Text = tab.Url;
                UpdateNavigationButtons();
                UpdateBlockedCount(tab);
                UpdateBookmarkButton();
            }
        }

        private void NavigateToUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || _tabManager.ActiveTab?.WebView == null)
                return;

            if (!url.StartsWith("http://") && !url.StartsWith("https://") && url != "about:blank")
                url = "https://" + url;

            url = _settingsManager.StripTrackingParameters(url);
            _tabManager.ActiveTab.WebView.Source = new Uri(url);
            _tabManager.ActiveTab.Url = url;
            AddressBar.Text = url;
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is BrowserTab tab)
            {
                ShowTab(tab);
            }
        }

        private async void NewTab_Click(object sender, RoutedEventArgs e)
        {
            await CreateNewTabAsync();
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is BrowserTab tab)
            {
                e.Handled = true; // Prevent tab selection
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

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tabManager.ActiveTab?.WebView?.CanGoBack == true)
                _tabManager.ActiveTab.WebView.GoBack();
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tabManager.ActiveTab?.WebView?.CanGoForward == true)
                _tabManager.ActiveTab.WebView.GoForward();
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            _tabManager.ActiveTab?.WebView?.Reload();
        }

        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToUrl(AddressBar.Text);
        }

        private void AddressBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                NavigateToUrl(AddressBar.Text);
        }

        private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e, BrowserTab tab)
        {
            tab.IsLoading = true;
            if (tab.IsActive)
            {
                StatusText.Text = "Loading...";
                UpdateNavigationButtons();
            }
        }

        private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e, 
            BrowserTab tab, AdBlocker adBlocker)
        {
            tab.IsLoading = false;
            tab.BlockedCount = adBlocker.BlockedCount;
            
            if (tab.IsActive)
            {
                StatusText.Text = e.IsSuccess ? "Ready" : "Failed to load";
                UpdateNavigationButtons();
                UpdateBlockedCount(tab);
            }
            
            // Update title
            if (tab.WebView?.CoreWebView2 != null)
            {
                tab.Title = string.IsNullOrEmpty(tab.WebView.CoreWebView2.DocumentTitle) 
                    ? "New Tab" 
                    : tab.WebView.CoreWebView2.DocumentTitle;
            }
        }

        private void WebView_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e, BrowserTab tab)
        {
            var url = tab.WebView?.Source?.ToString() ?? "";
            tab.Url = url;
            
            if (tab.IsActive)
            {
                AddressBar.Text = url;
                UpdateBookmarkButton();
            }
        }

        private void UpdateNavigationButtons()
        {
            BackButton.IsEnabled = _tabManager.ActiveTab?.WebView?.CanGoBack ?? false;
            ForwardButton.IsEnabled = _tabManager.ActiveTab?.WebView?.CanGoForward ?? false;
        }

        private void UpdateBlockedCount(BrowserTab tab)
        {
            BlockedCountText.Text = $"Blocked: {tab.BlockedCount}";
        }

        private void UpdateBookmarkButton()
        {
            var currentUrl = _tabManager.ActiveTab?.Url ?? "";
            var isBookmarked = _bookmarkManager.FindByUrl(currentUrl) != null;
            BookmarkButton.Content = isBookmarked ? "★" : "☆";
        }

        private async void BookmarkButton_Click(object sender, RoutedEventArgs e)
        {
            var currentUrl = _tabManager.ActiveTab?.Url ?? "";
            var currentTitle = _tabManager.ActiveTab?.Title ?? "New Tab";
            
            if (string.IsNullOrWhiteSpace(currentUrl) || currentUrl == "about:blank")
            {
                MessageBox.Show("Cannot bookmark this page.", "Bookmark", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            var existing = _bookmarkManager.FindByUrl(currentUrl);
            if (existing != null)
            {
                _bookmarkManager.RemoveBookmark(existing.Id);
                MessageBox.Show("Bookmark removed.", "Bookmark", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                _bookmarkManager.AddBookmark(currentTitle, currentUrl);
                MessageBox.Show("Bookmark added.", "Bookmark", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            
            await _bookmarkManager.SaveAsync();
            UpdateBookmarksBar();
            UpdateBookmarkButton();
        }

        private void UpdateBookmarksBar()
        {
            BookmarksBarControl.ItemsSource = _bookmarkManager.Bookmarks
                .Where(b => b.Folder == "Bookmarks Bar")
                .ToList();
            
            BookmarksBar.Visibility = BookmarksBarControl.HasItems 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        }

        private void BookmarkBarItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url)
            {
                NavigateToUrl(url);
            }
        }

        private void ManageBookmarks_Click(object sender, RoutedEventArgs e)
        {
            var bookmarksWindow = new BookmarksWindow(_bookmarkManager);
            bookmarksWindow.Owner = this;
            bookmarksWindow.ShowDialog();
            UpdateBookmarksBar();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(_settingsManager, _filterEngine);
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }

        private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            await _settingsManager.SaveAsync();
            await _bookmarkManager.SaveAsync();

            if (_settingsManager.Settings.ClearDataOnClose)
            {
                foreach (var tab in _tabManager.Tabs)
                {
                    if (tab.WebView?.CoreWebView2 != null)
                    {
                        await _webViewFactory.ClearBrowsingDataAsync(tab.WebView.CoreWebView2);
                    }
                }
            }
            
            _tabManager.CloseAllTabs();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

    }
}

