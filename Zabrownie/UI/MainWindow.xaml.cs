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

                // Create initial tab
                await CreateNewTabAsync(_settingsManager.Settings.Homepage ?? "https://www.google.com");
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

                // Navigate if URL provided
                if (!string.IsNullOrWhiteSpace(url) && url != "about:blank")
                {
                    // Pequeño retraso opcional para estabilidad visual
                    await Task.Delay(100);
                    webView.CoreWebView2.Navigate(url);
                    tab.Url = url;
                    AddressBar.Text = url;
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
                if (string.IsNullOrWhiteSpace(tab.Url) || tab.Url == "about:blank")
                {
                    AddressBar.Text = "Escribe URL o busca...";
                }
                else
                {
                    AddressBar.Text = tab.Url;
                }

                UpdateNavigationButtons();
                UpdateBlockedCount(tab);
                UpdateBookmarkButton();
            }
        }

        private void NavigateToUrl(string url)
        {
            var webView = _tabManager.ActiveTab?.WebView;
            if (webView?.CoreWebView2 == null)
            {
                // En lugar de fallar, reintentar en 500ms
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
                    url != "about:blank")
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
                e.Handled = true;
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
                UpdateBlockedCount(tab);
            }

            // Update title
            if (tab.WebView?.CoreWebView2 != null)
            {
                var title = tab.WebView.CoreWebView2.DocumentTitle;
                tab.Title = string.IsNullOrEmpty(title) ? "Nueva Pestaña" : title;
                LoggingService.Log($"Tab title updated: {tab.Title}");
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
            }
        }

        private void UpdateNavigationButtons()
        {
            BackButton.IsEnabled = _tabManager.ActiveTab?.WebView?.CanGoBack ?? false;
            ForwardButton.IsEnabled = _tabManager.ActiveTab?.WebView?.CanGoForward ?? false;
        }

        private void UpdateBlockedCount(BrowserTab tab)
        {
            BlockedCountText.Text = $"Bloqueados: {tab.BlockedCount}";
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

            if (string.IsNullOrWhiteSpace(currentUrl) || currentUrl == "about:blank")
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
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
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
                _closedTabs.Push(tabToClose); // Guardamos para reabrir
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

        private Stack<BrowserTab> _closedTabs = new();

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
                    // Si el WebView se destruyó, recrear
                    await CreateNewTabAsync(tab.Url);
                }
            }
        }

        private void ChangeZoom(double delta, bool reset = false)
        {
            var webView = _tabManager.ActiveTab?.WebView;
            if (webView?.CoreWebView2 == null) return;

            // En versiones nuevas, ZoomFactor está en el WebView2 WPF directamente
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
    }
}