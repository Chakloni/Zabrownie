using Zabrownie.Core;
using Zabrownie.Services;
using Zabrownie.Handlers;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Zabrownie.UI
{
    public partial class MainWindow : Window
    {
        // Core managers
        private readonly SettingsManager _settingsManager;
        private readonly FilterEngine _filterEngine;
        private readonly TabManager _tabManager;
        private readonly BookmarkManager _bookmarkManager;
        private readonly WebViewFactory _webViewFactory;
        private CoreWebView2Environment? _webViewEnvironment;

        // Handlers (delegate responsibilities)
        private NavigationHandler _navigationHandler;
        private TabHandler _tabHandler;
        private HomepageHandler _homepageHandler;
        private WindowControlsHandler _windowControlsHandler;
        private BookmarkHandler _bookmarkHandler;

        // Commands
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

            // Initialize core managers
            _settingsManager = new SettingsManager();
            _filterEngine = new FilterEngine();
            _tabManager = new TabManager();
            _bookmarkManager = new BookmarkManager();

            var adBlocker = new AdBlocker(_filterEngine, _settingsManager);
            _webViewFactory = new WebViewFactory(_settingsManager, adBlocker);

            // Initialize handlers
            InitializeHandlers();

            // Setup commands
            NewTabCommand = new RelayCommand(_ => _tabHandler.CreateNewTabAsync().ConfigureAwait(false));
            CloseTabCommand = new RelayCommand(_ => _tabHandler.CloseCurrentTab());
            NextTabCommand = new RelayCommand(_ => _tabHandler.SwitchToNextTab());
            PrevTabCommand = new RelayCommand(_ => _tabHandler.SwitchToPrevTab());
            ReopenClosedTabCommand = new RelayCommand(_ => _tabHandler.ReopenLastClosedTab());
            FocusAddressBarCommand = new RelayCommand(_ => AddressBar.Focus());
            ReloadCommand = new RelayCommand(_ => _navigationHandler.Reload());
            GoBackCommand = new RelayCommand(_ => _navigationHandler.GoBack());
            GoForwardCommand = new RelayCommand(_ => _navigationHandler.GoForward());
            ZoomInCommand = new RelayCommand(_ => _navigationHandler.ChangeZoom(0.25));
            ZoomOutCommand = new RelayCommand(_ => _navigationHandler.ChangeZoom(-0.25));
            ZoomResetCommand = new RelayCommand(_ => _navigationHandler.ChangeZoom(0, true));

            DataContext = this;
            TabsControl.DataContext = _tabManager.Tabs;
        }

        private void InitializeHandlers()
        {
            _navigationHandler = new NavigationHandler(
                _tabManager, _settingsManager, AddressBar, StatusText);

            _tabHandler = new TabHandler(
                _tabManager, _filterEngine, _settingsManager, _webViewFactory,
                WebViewContainer, AddressBar, this, _navigationHandler);

            _homepageHandler = new HomepageHandler(
                HomepageGrid, WebViewContainerGrid, HomepageSearchBox,
                TimeText, DateText, DayText, RecentSitesControl, NoRecentSitesText,
                _navigationHandler);

            _windowControlsHandler = new WindowControlsHandler(this);

            _bookmarkHandler = new BookmarkHandler(
                _bookmarkManager, _tabManager, BookmarksBarControl,
                BookmarksBar, BookmarkButton, _navigationHandler);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await _settingsManager.LoadAsync();
                await _bookmarkManager.LoadAsync();
                ThemeManager.ApplyAccentColor(_settingsManager.Settings.AccentColor);
                await CreateDefaultFiltersIfNeeded();

                _bookmarkHandler.UpdateBookmarksBar();

                // Initialize WebView2 environment
                LoggingService.Log("Initializing WebView2 Environment...");
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Zabrownie", "WebView2Data");

                _webViewEnvironment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                _tabHandler.SetWebViewEnvironment(_webViewEnvironment);

                LoggingService.Log("WebView2 Environment initialized successfully");

                // Initialize homepage
                _homepageHandler.Initialize();

                // Create initial tab
                await _tabHandler.CreateNewTabAsync("homepage");
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

        private async Task CreateDefaultFiltersIfNeeded()
        {
            var filtersPath = FileService.GetDefaultFiltersPath();
            var filtersDirectory = Path.GetDirectoryName(filtersPath);

            if (!File.Exists(filtersPath))
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

            // Load filter lists
            var filterLists = new System.Collections.Generic.List<string> { filtersPath };

            if (!string.IsNullOrEmpty(filtersDirectory))
            {
                var easyListPath = Path.Combine(filtersDirectory, "easylist.txt");
                if (File.Exists(easyListPath)) filterLists.Add(easyListPath);

                var easyPrivacyPath = Path.Combine(filtersDirectory, "easyprivacy.txt");
                if (File.Exists(easyPrivacyPath)) filterLists.Add(easyPrivacyPath);
            }

            filterLists.AddRange(_settingsManager.Settings.CustomFilterLists);
            await _filterEngine.LoadFiltersFromMultipleSourcesAsync(filterLists);
        }

        // ===== EVENT HANDLERS (Delegate to handlers) =====

        private void Tab_Click(object sender, RoutedEventArgs e)
            => _tabHandler.OnTabClick(sender, e);

        private void NewTab_Click(object sender, RoutedEventArgs e)
            => _tabHandler.OnNewTabClick(sender, e);

        private void CloseTab_Click(object sender, RoutedEventArgs e)
            => _tabHandler.OnCloseTabClick(sender, e);

        private void BackButton_Click(object sender, RoutedEventArgs e)
            => _navigationHandler.GoBack();

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
            => _navigationHandler.GoForward();

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
            => _navigationHandler.Reload();

        private void GoButton_Click(object sender, RoutedEventArgs e)
            => _navigationHandler.Navigate(AddressBar.Text);

        private void AddressBar_KeyDown(object sender, KeyEventArgs e)
            => _navigationHandler.OnAddressBarKeyDown(sender, e);

        private void AddressBar_GotFocus(object sender, RoutedEventArgs e)
            => _navigationHandler.OnAddressBarGotFocus(sender, e);

        private void BookmarkButton_Click(object sender, RoutedEventArgs e)
            => _bookmarkHandler.OnBookmarkButtonClick(sender, e);

        private void BookmarkBarItem_Click(object sender, RoutedEventArgs e)
            => _bookmarkHandler.OnBookmarkBarItemClick(sender, e);

        private void ManageBookmarks_Click(object sender, RoutedEventArgs e)
            => _bookmarkHandler.OnManageBookmarksClick(sender, e, this);

        private void HomeButton_Click(object sender, RoutedEventArgs e)
            => _homepageHandler.Show();

        private void HomepageSearchBox_GotFocus(object sender, RoutedEventArgs e)
            => _homepageHandler.OnSearchBoxGotFocus(sender, e);

        private void HomepageSearchBox_LostFocus(object sender, RoutedEventArgs e)
            => _homepageHandler.OnSearchBoxLostFocus(sender, e);

        private void HomepageSearchBox_KeyDown(object sender, KeyEventArgs e)
            => _homepageHandler.OnSearchBoxKeyDown(sender, e);

        private void HomepageSearchButton_Click(object sender, RoutedEventArgs e)
            => _homepageHandler.OnSearchButtonClick(sender, e);

        private void QuickLink_Click(object sender, RoutedEventArgs e)
            => _homepageHandler.OnQuickLinkClick(sender, e);

        private void RecentSite_Click(object sender, RoutedEventArgs e)
            => _homepageHandler.OnRecentSiteClick(sender, e);

        private void AddCustomLink_Click(object sender, RoutedEventArgs e)
            => _homepageHandler.OnAddCustomLinkClick(sender, e);

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => _windowControlsHandler.OnTitleBarMouseDown(sender, e);

        private void Move_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => _windowControlsHandler.OnMoveMouseDown(sender, e);

        private void StopDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => e.Handled = true;

        private void Minimize_Click(object sender, RoutedEventArgs e)
            => _windowControlsHandler.Minimize();

        private void Maximize_Click(object sender, RoutedEventArgs e)
            => _windowControlsHandler.Maximize();

        private void Close_Click(object sender, RoutedEventArgs e)
            => Close();

        private void ResizeGrip_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => _windowControlsHandler.OnResizeGripMouseDown(sender, e);

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(_settingsManager, _filterEngine);
            settingsWindow.Owner = this;

            if (settingsWindow.ShowDialog() == true)
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
            _homepageHandler.Cleanup();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _windowControlsHandler.OnSourceInitialized(e);
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            _windowControlsHandler.OnStateChanged(e);
        }

        // Relay command helper
        public class RelayCommand : ICommand
        {
            private readonly Action<object?> _execute;
            public RelayCommand(Action<object?> execute) 
                => _execute = execute ?? throw new ArgumentNullException(nameof(execute));

            public bool CanExecute(object? parameter) => true;
            public void Execute(object? parameter) => _execute(parameter);
            public event EventHandler? CanExecuteChanged { add { } remove { } }
        }
    }
}