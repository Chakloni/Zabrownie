using Zabrownie.Core;
using Zabrownie.Models;
using Zabrownie.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Windows.Media;

namespace Zabrownie.UI
{
    public partial class MainWindow : Window
    {
        private readonly SettingsManager _settingsManager;
        private readonly FilterEngine _filterEngine;
        private readonly TabManager _tabManager;
        private readonly BookmarkManager _bookmarkManager;
        private readonly WebViewFactory _webViewFactory;
        private CoreWebView2Environment? _webViewEnvironment;

        // Homepage-related fields
        private List<QuickLink> _quickLinks = [];
        private List<RecentSite> _recentSites = [];
        private DispatcherTimer? _clockTimer; // Made nullable
        private readonly Stack<BrowserTab> _closedTabs = new();

        public ICommand NewTabCommand { get; }
        public ICommand CloseTabCommand { get; }
        public ICommand NextTabCommand { get; }
        public ICommand PrevTabCommand { get; }
        public ICommand ReopenClosedTabCommand { get; }
        public ICommand FocusAddressBarCommand { get; }
        public ICommand ReloadCommand { get; }
        public ICommand GoBackCommand { get; }
        public ICommand GoForwardCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand ZoomResetCommand { get; }

        public MainWindow()
        {
            InitializeComponent();
            NewTabCommand = new RelayCommand(_ => CreateNewTabAsync().ConfigureAwait(false));
            CloseTabCommand = new RelayCommand(_ => CloseCurrentTab());
            NextTabCommand = new RelayCommand(_ => SwitchToNextTab());
            PrevTabCommand = new RelayCommand(_ => SwitchToPrevTab());
            ReopenClosedTabCommand = new RelayCommand(_ => ReopenLastClosedTab());
            FocusAddressBarCommand = new RelayCommand(_ => AddressBar.Focus());
            ReloadCommand = new RelayCommand(_ => ReloadButton_Click(null!, null!));
            GoBackCommand = new RelayCommand(_ => BackButton_Click(null!, null!));
            GoForwardCommand = new RelayCommand(_ => ForwardButton_Click(null!, null!));
            ZoomInCommand = new RelayCommand(_ => ChangeZoom(0.25));
            ZoomOutCommand = new RelayCommand(_ => ChangeZoom(-0.25));
            ZoomResetCommand = new RelayCommand(_ => ChangeZoom(0, true));
            DataContext = this;

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
                ThemeManager.ApplyAccentColor(_settingsManager.Settings.AccentColor);
                await CreateDefaultFiltersIfNeeded();

                // Load bookmarks bar
                UpdateBookmarksBar();

                // Initialize WebView2 environment FIRST
                LoggingService.Log("Initializing WebView2 Environment...");
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Zabrownie", "WebView2Data");

                LoggingService.Log("Initializing CoreWebView2Environment");

                _webViewEnvironment = await CoreWebView2Environment.CreateAsync(
                    null, userDataFolder);

                LoggingService.Log("WebView2 Environment initialized successfully");

                // Initialize homepage
                InitializeHomepage();

                // Create initial tab - use "homepage" to show the homepage
                //await CreateNewTabAsync(_settingsManager.Settings.Homepage ?? "homepage");
                await CreateNewTabAsync("homepage");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to initialize browser", ex);
                MessageBox.Show($"Error al inicializar el navegador:\n\n{ex.Message}\n\n" +
                    "¿Tienes WebView2 Runtime instalado?\n" +
                    "Descárgalo de: https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task CreateDefaultFiltersIfNeeded()
        {
            var filtersPath = FileService.GetDefaultFiltersPath();
            var filtersDirectory = Path.GetDirectoryName(filtersPath);

            // Create default filters if they don't exist
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

            // Load all filter lists
            var filterLists = new List<string> { filtersPath };

            // Add EasyList if it exists
            if (!string.IsNullOrEmpty(filtersDirectory))
            {
                var easyListPath = Path.Combine(filtersDirectory, "easylist.txt");
                if (File.Exists(easyListPath))
                {
                    filterLists.Add(easyListPath);
                }

                // Add EasyPrivacy if it exists
                var easyPrivacyPath = Path.Combine(filtersDirectory, "easyprivacy.txt");
                if (File.Exists(easyPrivacyPath))
                {
                    filterLists.Add(easyPrivacyPath);
                }
            }

            // Load custom filter lists from settings
            filterLists.AddRange(_settingsManager.Settings.CustomFilterLists);

            await _filterEngine.LoadFiltersFromMultipleSourcesAsync(filterLists);
        }

        private async System.Threading.Tasks.Task CreateNewTabAsync(string url = "homepage")
        {
            try
            {
                if (_webViewEnvironment == null)
                {
                    LoggingService.Log("ERROR: WebView2 Environment not initialized");
                    StatusText.Text = "Error: WebView2 no inicializado";
                    return;
                }

                LoggingService.Log($"Creating new tab with URL: {url}");

                var tab = _tabManager.CreateTab(url);

                var webView = new WebView2();

                WebViewContainer.Children.Add(webView);

                LoggingService.Log("Initializing WebView2 with environment...");

                // Use the pre-initialized environment
                await webView.EnsureCoreWebView2Async(_webViewEnvironment);

                webView.CoreWebView2.ContainsFullScreenElementChanged += (s, e) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (webView.CoreWebView2.ContainsFullScreenElement)
                        {
                            TitleBar.Visibility = Visibility.Collapsed;
                            BookmarksBar.Visibility = Visibility.Collapsed;
                            NavigationBar.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            TitleBar.Visibility = Visibility.Visible;
                            BookmarksBar.Visibility = Visibility.Visible;
                            NavigationBar.Visibility = Visibility.Visible;
                        }
                    });
                };

                LoggingService.Log("WebView2 initialized successfully");

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

                // Show homepage if URL is "homepage" or about:blank
                if (url == "homepage" || url == "about:blank")
                {
                    ShowHomepage(true);
                }
                else
                {
                    // Navigate if URL provided
                    if (!string.IsNullOrWhiteSpace(url) && url != "about:blank")
                    {
                        // Small optional delay for visual stability
                        await Task.Delay(100);
                        webView.CoreWebView2.Navigate(url);
                        tab.Url = url;
                        AddressBar.Text = url;
                        ShowHomepage(false);
                    }
                }

                LoggingService.Log("Tab created successfully");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to create new tab", ex);
                MessageBox.Show($"Error al crear pestaña: {ex.Message}\n\n{ex.StackTrace}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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

                // Update address bar
                if (string.IsNullOrWhiteSpace(tab.Url) || tab.Url == "about:blank" || tab.Url == "homepage")
                {
                    AddressBar.Text = "Escribe URL o busca...";
                }
                else
                {
                    AddressBar.Text = tab.Url;
                }

                UpdateNavigationButtons();
                UpdateBookmarkButton();
            }
        }

        private void NavigateToUrl(string url)
        {
            var webView = _tabManager.ActiveTab?.WebView;
            if (webView?.CoreWebView2 == null)
            {
                // Instead of failing, retry in 500ms
                Dispatcher.InvokeAsync(async () =>
                {
                    await Task.Delay(500);
                    NavigateToUrl(url);
                });
                StatusText.Text = "Esperando inicialización del navegador...";
                return;
            }
            if (_tabManager.ActiveTab?.WebView?.CoreWebView2 == null)
            {
                LoggingService.Log("Cannot navigate: WebView not initialized");
                StatusText.Text = "Error: WebView no inicializado";
                MessageBox.Show("La pestaña aún no está lista. Espera unos segundos e intenta de nuevo.",
                    "WebView2 Inicializando", MessageBoxButton.OK, MessageBoxImage.Information);
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

                // If it doesn't have a protocol
                if (!url.StartsWith("http://") &&
                    !url.StartsWith("https://") &&
                    !url.StartsWith("file://") &&
                    url != "about:blank" &&
                    url != "homepage")
                {
                    // Check if it's a search query
                    if (url.Contains(" ") || (!url.Contains(".") && !url.Contains(":")))
                    {
                        // Search on Google
                        finalUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(url)}";
                        LoggingService.Log($"Treating as search query: {finalUrl}");
                    }
                    else
                    {
                        // Treat as domain
                        finalUrl = "https://" + url;
                        LoggingService.Log($"Adding https:// prefix: {finalUrl}");
                    }
                }

                // Strip tracking parameters
                finalUrl = _settingsManager.StripTrackingParameters(finalUrl);

                LoggingService.Log($"Final URL to navigate: {finalUrl}");

                // Navigate using CoreWebView2.Navigate (more reliable)
                _tabManager.ActiveTab.WebView.CoreWebView2.Navigate(finalUrl);

                // Update tab URL and address bar
                _tabManager.ActiveTab.Url = finalUrl;
                AddressBar.Text = finalUrl;

                StatusText.Text = $"Navegando a {finalUrl}...";

                // Hide homepage when navigating
                ShowHomepage(false);

                LoggingService.Log($"Navigation command sent successfully");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Navigation error for URL: {url}", ex);
                StatusText.Text = $"Error de navegación: {ex.Message}";
                MessageBox.Show($"Error al navegar: {ex.Message}", "Error de Navegación",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is BrowserTab tab)
            {
                ShowTab(tab);

                // Show/hide homepage based on current tab's URL
                if (tab.Url == "homepage" || tab.Url == "about:blank" || string.IsNullOrWhiteSpace(tab.Url))
                {
                    ShowHomepage(true);
                }
                else
                {
                    ShowHomepage(false);
                }
            }
        }

        private async void NewTab_Click(object sender, RoutedEventArgs e)
        {
            await CreateNewTabAsync("homepage"); // Show homepage on new tab
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is BrowserTab tab)
            {
                e.Handled = true;
                _tabManager.CloseTab(tab);

                if (_tabManager.ActiveTab != null)
                {
                    ShowTab(_tabManager.ActiveTab);

                    // Show/hide homepage based on active tab's URL
                    if (_tabManager.ActiveTab.Url == "homepage" || _tabManager.ActiveTab.Url == "about:blank" || string.IsNullOrWhiteSpace(_tabManager.ActiveTab.Url))
                    {
                        ShowHomepage(true);
                    }
                    else
                    {
                        ShowHomepage(false);
                    }
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
            {
                _tabManager.ActiveTab.WebView.GoBack();
                LoggingService.Log("Navigated back");
            }
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tabManager.ActiveTab?.WebView?.CanGoForward == true)
            {
                _tabManager.ActiveTab.WebView.GoForward();
                LoggingService.Log("Navigated forward");
            }
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tabManager.ActiveTab?.WebView?.CoreWebView2 != null)
                _tabManager.ActiveTab.WebView.Reload();
        }

        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            LoggingService.Log($"Go button clicked, AddressBar.Text = '{AddressBar.Text}'");
            NavigateToUrl(AddressBar.Text);
        }

        private void AddressBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LoggingService.Log($"Enter pressed in AddressBar, text = '{AddressBar.Text}'");
                NavigateToUrl(AddressBar.Text);
            }
        }

        private void AddressBar_GotFocus(object sender, RoutedEventArgs e)
        {
            if (AddressBar.Text == "Escribe URL o busca...")
            {
                AddressBar.Text = "";
            }
        }

        private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e, BrowserTab tab)
        {
            tab.IsLoading = true;
            LoggingService.Log($"Navigation starting: {e.Uri}");

            if (tab.IsActive)
            {
                StatusText.Text = $"Cargando: {e.Uri}";
                UpdateNavigationButtons();
            }
        }

        private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e,
            BrowserTab tab, AdBlocker adBlocker)
        {
            tab.IsLoading = false;
            tab.BlockedCount = adBlocker.BlockedCount;

            LoggingService.Log($"Navigation completed: IsSuccess={e.IsSuccess}, WebErrorStatus={e.WebErrorStatus}");

            if (tab.IsActive)
            {
                if (e.IsSuccess)
                {
                    StatusText.Text = "Listo";
                }
                else
                {
                    StatusText.Text = $"Error al cargar (Error: {e.WebErrorStatus})";
                    LoggingService.LogError($"Navigation failed with status: {e.WebErrorStatus}",
                        new Exception(e.WebErrorStatus.ToString()));
                }
                UpdateNavigationButtons();
            }

            // Update title
            if (tab.WebView?.CoreWebView2 != null)
            {
                var title = tab.WebView.CoreWebView2.DocumentTitle;
                tab.Title = string.IsNullOrEmpty(title) ? "Nueva Pestaña" : title;
                LoggingService.Log($"Tab title updated: {tab.Title}");

                // Add to recent sites
                if (e.IsSuccess && !string.IsNullOrEmpty(tab.Url) && tab.Url != "about:blank" && tab.Url != "homepage")
                {
                    AddToRecentSites(tab.Title, tab.Url);
                }
            }
        }

        private void WebView_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e, BrowserTab tab)
        {
            var url = tab.WebView?.Source?.ToString() ?? "";
            tab.Url = url;

            LoggingService.Log($"Source changed to: {url}");

            if (tab.IsActive)
            {
                AddressBar.Text = url;
                UpdateBookmarkButton();

                // Show/hide homepage based on URL
                if (url == "about:blank" || url == "homepage" || string.IsNullOrWhiteSpace(url))
                {
                    ShowHomepage(true);
                }
                else
                {
                    ShowHomepage(false);
                }
            }
        }

        private void UpdateNavigationButtons()
        {
            BackButton.IsEnabled = _tabManager.ActiveTab?.WebView?.CanGoBack ?? false;
            ForwardButton.IsEnabled = _tabManager.ActiveTab?.WebView?.CanGoForward ?? false;
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
            var currentTitle = _tabManager.ActiveTab?.Title ?? "Nueva Pestaña";

            if (string.IsNullOrWhiteSpace(currentUrl) || currentUrl == "about:blank" || currentUrl == "homepage")
            {
                MessageBox.Show("No se puede marcar esta página.", "Marcador",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var existing = _bookmarkManager.FindByUrl(currentUrl);
            if (existing != null)
            {
                _bookmarkManager.RemoveBookmark(existing.Id);
                MessageBox.Show("Marcador eliminado.", "Marcador",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                _bookmarkManager.AddBookmark(currentTitle, currentUrl);
                MessageBox.Show("Marcador agregado.", "Marcador",
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

            bool? result = settingsWindow.ShowDialog();

            if (result == true)
            {
                ThemeManager.ApplyAccentColor(_settingsManager.Settings.AccentColor);
            }
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

            // Stop the clock timer
            if (_clockTimer != null)
            {
                _clockTimer.Stop();
            }
        }

        // ===== HOMEPAGE FUNCTIONALITY =====

        private void InitializeHomepage()
        {
            // Setup clock timer
            _clockTimer = new DispatcherTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += UpdateClock;
            _clockTimer.Start();

            // Load quick links
            LoadQuickLinks();

            // Load recent sites
            LoadRecentSites();

            // Update clock immediately
            UpdateClock();
        }

        private void UpdateClock(object? sender = null, EventArgs? e = null) // Made parameters nullable
        {
            var now = DateTime.Now;
            TimeText.Text = now.ToString("HH:mm");
            DateText.Text = now.ToString("dddd, MMMM dd");
            DayText.Text = $"Day {now.DayOfYear} of {now.Year}";
        }

        private void LoadQuickLinks()
        {
            // Default quick links - you can load from settings file later
            _quickLinks = new List<QuickLink>
            {
                new QuickLink { Title = "YouTube", Url = "https://youtube.com", Icon = "▶️" },
                new QuickLink { Title = "Netflix", Url = "https://netflix.com", Icon = "🎬" },
                new QuickLink { Title = "Spotify", Url = "https://spotify.com", Icon = "🎵" },
                new QuickLink { Title = "Gmail", Url = "https://gmail.com", Icon = "✉️" },
                new QuickLink { Title = "GitHub", Url = "https://github.com", Icon = "💻" },
                new QuickLink { Title = "Reddit", Url = "https://reddit.com", Icon = "📱" },
            };

            // You can load user-customized links from settings here
        }

        private void LoadRecentSites()
        {
            // Load from saved file or create empty list
            _recentSites = new List<RecentSite>();

            // Update UI
            UpdateRecentSitesUI();
        }

        private void UpdateRecentSitesUI()
        {
            RecentSitesControl.ItemsSource = _recentSites
                .OrderByDescending(r => r.VisitedAt)
                .Take(5)
                .ToList();

            NoRecentSitesText.Visibility = _recentSites.Any()
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void AddToRecentSites(string title, string url)
        {
            var existing = _recentSites.FirstOrDefault(r => r.Url == url);
            if (existing != null)
            {
                _recentSites.Remove(existing);
            }

            _recentSites.Add(new RecentSite
            {
                Title = string.IsNullOrEmpty(title) ? url : title,
                Url = url,
                VisitedAt = DateTime.Now
            });

            // Keep only last 20 sites
            if (_recentSites.Count > 20)
            {
                _recentSites = _recentSites
                    .OrderByDescending(r => r.VisitedAt)
                    .Take(20)
                    .ToList();
            }

            UpdateRecentSitesUI();
        }

        private void ShowHomepage(bool show = true)
        {
            HomepageGrid.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            WebViewContainerGrid.Visibility = show ? Visibility.Collapsed : Visibility.Visible;

            if (show)
            {
                AddressBar.Focus();
                UpdateClock(); // Update time immediately
            }
        }

        // Homepage search box handlers
        private void HomepageSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (HomepageSearchBox.Text == "Search or enter address...")
            {
                HomepageSearchBox.Text = "";
                HomepageSearchBox.Foreground = (SolidColorBrush)FindResource("TextPrimary");
            }
        }

        private void HomepageSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(HomepageSearchBox.Text))
            {
                HomepageSearchBox.Text = "Search or enter address...";
                HomepageSearchBox.Foreground = (SolidColorBrush)FindResource("TextSecondary");
            }
        }

        private void HomepageSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NavigateFromHomepage();
            }
        }

        private void HomepageSearchButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateFromHomepage();
        }

        private void NavigateFromHomepage()
        {
            var url = HomepageSearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(url) || url == "Search or enter address...")
                return;

            // Hide homepage and navigate
            ShowHomepage(false);
            NavigateToUrl(url);
        }

        // Quick link click handler
        private void QuickLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url)
            {
                ShowHomepage(false);
                NavigateToUrl(url);
            }
        }

        // Recent site click handler
        private void RecentSite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url)
            {
                ShowHomepage(false);
                NavigateToUrl(url);
            }
        }

        // Add custom link (simple implementation)
        private void AddCustomLink_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CustomLinkWindow();
            if (dialog.ShowDialog() == true)
            {
                // Add the new link
                _quickLinks.Add(new QuickLink
                {
                    Title = dialog.LinkTitle,
                    Url = dialog.LinkUrl,
                    Icon = dialog.LinkIcon
                });

                // For now, just show a message
                MessageBox.Show($"Added {dialog.LinkTitle} to quick links!",
                    "Link Added",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        // Home button handler (add this button to your navigation bar)
        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            ShowHomepage(true);
        }

        // ===== WINDOW DRAG FUNCTIONALITY =====

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // Double-click to maximize/restore
                Maximize_Click(sender, e);
            }
            else
            {
                // Single click to drag
                try
                {
                    DragMove();
                }
                catch (Exception ex)
                {
                    LoggingService.LogError("Error during window drag", ex);
                }
            }
        }

        private void Move_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                DragMove();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error during window drag", ex);
            }
        }

        private void StopDrag(object sender, MouseButtonEventArgs e)
        {
            // Prevent drag event from bubbling up
            e.Handled = true;
        }

        // ===== WINDOW CONTROLS =====

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                // Obtener el área de trabajo (excluyendo la barra de tareas)
                var workArea = SystemParameters.WorkArea;

                WindowState = WindowState.Maximized;

                // Asegurar que la ventana respeta la barra de tareas
                MaxHeight = workArea.Height + 8; // +16 para compensar bordes
                MaxWidth = workArea.Width;
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            if (WindowState == WindowState.Maximized)
            {
                var workArea = SystemParameters.WorkArea;

                Top = workArea.Top;
                Left = workArea.Left;
                Width = workArea.Width;
                Height = workArea.Height;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void CloseCurrentTab()
        {
            if (_tabManager.ActiveTab != null)
            {
                var tabToClose = _tabManager.ActiveTab;
                _closedTabs.Push(tabToClose); // Save for reopening
                _tabManager.CloseTab(tabToClose);

                if (_tabManager.Tabs.Count == 0)
                {
                    await CreateNewTabAsync();
                }
                else if (_tabManager.ActiveTab != null)
                {
                    ShowTab(_tabManager.ActiveTab);
                }
            }
        }

        private void SwitchToNextTab()
        {
            if (_tabManager.Tabs.Count <= 1 || _tabManager.ActiveTab is not { } activeTab) return;

            int index = _tabManager.Tabs.IndexOf(activeTab);
            int nextIndex = (index + 1) % _tabManager.Tabs.Count;
            ShowTab(_tabManager.Tabs[nextIndex]);
        }

        private void SwitchToPrevTab()
        {
            if (_tabManager.Tabs.Count <= 1 || _tabManager.ActiveTab is not { } activeTab) return;

            int index = _tabManager.Tabs.IndexOf(activeTab);
            int prevIndex = (index - 1 + _tabManager.Tabs.Count) % _tabManager.Tabs.Count;
            ShowTab(_tabManager.Tabs[prevIndex]);
        }

        private async void ReopenLastClosedTab()
        {
            if (_closedTabs.TryPop(out var tab))
            {
                _tabManager.Tabs.Add(tab);
                ShowTab(tab);
                if (tab.WebView?.CoreWebView2 != null)
                {
                    // Re-navigate to last URL
                    tab.WebView.CoreWebView2.Navigate(tab.Url ?? "https://www.google.com");
                }
                else
                {
                    // If WebView was destroyed, recreate
                    await CreateNewTabAsync(tab.Url);
                }
            }
        }

        private void ChangeZoom(double delta, bool reset = false)
        {
            var webView = _tabManager.ActiveTab?.WebView;
            if (webView?.CoreWebView2 == null) return;

            // In newer versions, ZoomFactor is directly in the WebView2 WPF control
            if (reset)
                webView.ZoomFactor = 1.0;
            else
                webView.ZoomFactor = Math.Clamp(webView.ZoomFactor + delta, 0.25, 5.0);
        }

        public class RelayCommand : ICommand
        {
            private readonly Action<object?> _execute;
            public RelayCommand(Action<object?> execute) => _execute = execute ?? throw new ArgumentNullException(nameof(execute));

            public bool CanExecute(object? parameter) => true;
            public void Execute(object? parameter) => _execute(parameter);

            public event EventHandler? CanExecuteChanged
            {
                add { }
                remove { }
            }
        }

        private void ResizeGrip_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragResize(WindowResizeEdge.BottomRight);
        }

        private void DragResize(WindowResizeEdge edge)
        {
            SendMessage(
                new WindowInteropHelper(this).Handle,
                0x112,
                (IntPtr)(0xF000 + edge),
                IntPtr.Zero);
        }

        private enum WindowResizeEdge
        {
            BottomRight = 8
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(
            IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private enum ResizeDirection
        {
            None,
            Left,
            Right,
            Top,
            Bottom,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Agregar hook para mensajes de Windows
            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_NCHITTEST = 0x0084;

            if (msg == WM_NCHITTEST)
            {
                Point point = PointFromScreen(new Point(
                    (short)(lParam.ToInt32() & 0xFFFF),
                    (short)((lParam.ToInt32() >> 16) & 0xFFFF)));

                ResizeDirection direction = GetResizeDirection(point);

                if (direction != ResizeDirection.None)
                {
                    handled = true;
                    return (IntPtr)GetHitTestValue(direction);
                }
                handled = false;
            }

            return IntPtr.Zero;
        }

        private ResizeDirection GetResizeDirection(Point point)
        {
            const double edgeThickness = 10;

            bool isLeft = point.X <= edgeThickness;
            bool isRight = point.X >= ActualWidth - edgeThickness;
            bool isTop = point.Y <= edgeThickness;
            bool isBottom = point.Y >= ActualHeight - edgeThickness;

            if (isTop && isLeft) return ResizeDirection.TopLeft;
            if (isTop && isRight) return ResizeDirection.TopRight;
            if (isBottom && isLeft) return ResizeDirection.BottomLeft;
            if (isBottom && isRight) return ResizeDirection.BottomRight;
            if (isLeft) return ResizeDirection.Left;
            if (isRight) return ResizeDirection.Right;
            if (isTop) return ResizeDirection.Top;
            if (isBottom) return ResizeDirection.Bottom;

            return ResizeDirection.None;
        }

        private static int GetHitTestValue(ResizeDirection direction)
        {
            return direction switch
            {
                ResizeDirection.Left => 10,      // HTLEFT
                ResizeDirection.Right => 11,     // HTRIGHT
                ResizeDirection.Top => 12,       // HTTOP
                ResizeDirection.Bottom => 15,    // HTBOTTOM
                ResizeDirection.TopLeft => 13,   // HTTOPLEFT
                ResizeDirection.TopRight => 14,  // HTTOPRIGHT
                ResizeDirection.BottomLeft => 16,// HTBOTTOMLEFT
                ResizeDirection.BottomRight => 17,// HTBOTTOMRIGHT
                _ => 1 // HTCLIENT
            };
        }

    }


    // Homepage-related classes
    public class QuickLink
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string Icon { get; set; } = "🔗";
    }

    public class RecentSite
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public DateTime VisitedAt { get; set; } = DateTime.Now;
    }
}