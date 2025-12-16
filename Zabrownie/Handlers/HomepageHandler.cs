using Zabrownie.Models;
using Zabrownie.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Zabrownie.Handlers
{
    public class HomepageHandler
    {
        private readonly Grid _homepageGrid;
        private readonly Grid _webViewContainerGrid;
        private readonly TextBox _searchBox;
        private readonly TextBlock _timeText;
        private readonly TextBlock _dateText;
        private readonly TextBlock _dayText;
        private readonly ItemsControl _recentSitesControl;
        private readonly TextBlock _noRecentSitesText;
        private readonly NavigationHandler _navigationHandler;

        private List<QuickLink> _quickLinks = new();
        private List<RecentSite> _recentSites = new();
        private DispatcherTimer? _clockTimer;

        public HomepageHandler(
            Grid homepageGrid,
            Grid webViewContainerGrid,
            TextBox searchBox,
            TextBlock timeText,
            TextBlock dateText,
            TextBlock dayText,
            ItemsControl recentSitesControl,
            TextBlock noRecentSitesText,
            NavigationHandler navigationHandler)
        {
            _homepageGrid = homepageGrid;
            _webViewContainerGrid = webViewContainerGrid;
            _searchBox = searchBox;
            _timeText = timeText;
            _dateText = dateText;
            _dayText = dayText;
            _recentSitesControl = recentSitesControl;
            _noRecentSitesText = noRecentSitesText;
            _navigationHandler = navigationHandler;
        }

        public void Initialize()
        {
            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (s, e) => UpdateClock();
            _clockTimer.Start();

            LoadQuickLinks();
            LoadRecentSites();
            UpdateClock();
        }

        public void Cleanup()
        {
            _clockTimer?.Stop();
        }

        public void Show(bool show = true)
        {
            _homepageGrid.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            _webViewContainerGrid.Visibility = show ? Visibility.Collapsed : Visibility.Visible;

            if (show)
            {
                _searchBox.Focus();
                UpdateClock();
            }
        }

        public void AddRecentSite(string title, string url)
        {
            var existing = _recentSites.FirstOrDefault(r => r.Url == url);
            if (existing != null) _recentSites.Remove(existing);

            _recentSites.Add(new RecentSite
            {
                Title = string.IsNullOrEmpty(title) ? url : title,
                Url = url,
                VisitedAt = DateTime.Now
            });

            if (_recentSites.Count > 20)
            {
                _recentSites = _recentSites
                    .OrderByDescending(r => r.VisitedAt)
                    .Take(20)
                    .ToList();
            }

            UpdateRecentSitesUI();
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            _timeText.Text = now.ToString("HH:mm");
            _dateText.Text = now.ToString("dddd, MMMM dd");
            _dayText.Text = $"Day {now.DayOfYear} of {now.Year}";
        }

        private void LoadQuickLinks()
        {
            _quickLinks = new List<QuickLink>
            {
                new() { Title = "YouTube", Url = "https://youtube.com", Icon = "▶️" },
                new() { Title = "Netflix", Url = "https://netflix.com", Icon = "🎬" },
                new() { Title = "Spotify", Url = "https://spotify.com", Icon = "🎵" },
                new() { Title = "Gmail", Url = "https://gmail.com", Icon = "✉️" },
                new() { Title = "GitHub", Url = "https://github.com", Icon = "💻" },
                new() { Title = "Reddit", Url = "https://reddit.com", Icon = "📱" }
            };
        }

        private void LoadRecentSites()
        {
            _recentSites = new List<RecentSite>();
            UpdateRecentSitesUI();
        }

        private void UpdateRecentSitesUI()
        {
            _recentSitesControl.ItemsSource = _recentSites
                .OrderByDescending(r => r.VisitedAt)
                .Take(5)
                .ToList();

            _noRecentSitesText.Visibility = _recentSites.Any() 
                ? Visibility.Collapsed 
                : Visibility.Visible;
        }

        // Event handlers
        public void OnSearchBoxGotFocus(object sender, RoutedEventArgs e)
        {
            if (_searchBox.Text == "Search or enter address...")
            {
                _searchBox.Text = "";
                _searchBox.Foreground = (SolidColorBrush)Application.Current.FindResource("TextPrimary");
            }
        }

        public void OnSearchBoxLostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_searchBox.Text))
            {
                _searchBox.Text = "Search or enter address...";
                _searchBox.Foreground = (SolidColorBrush)Application.Current.FindResource("TextSecondary");
            }
        }

        public void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) NavigateFromSearchBox();
        }

        public void OnSearchButtonClick(object sender, RoutedEventArgs e)
        {
            NavigateFromSearchBox();
        }

        public void OnQuickLinkClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url)
            {
                Show(false);
                _navigationHandler.Navigate(url);
            }
        }

        public void OnRecentSiteClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url)
            {
                Show(false);
                _navigationHandler.Navigate(url);
            }
        }

        public void OnAddCustomLinkClick(object sender, RoutedEventArgs e)
        {
            /* var dialog = new CustomLinkWindow();
            if (dialog.ShowDialog() == true)
            {
                _quickLinks.Add(new QuickLink
                {
                    Title = dialog.LinkTitle,
                    Url = dialog.LinkUrl,
                    Icon = dialog.LinkIcon
                });

                MessageBox.Show($"Added {dialog.LinkTitle} to quick links!",
                    "Link Added", MessageBoxButton.OK, MessageBoxImage.Information);
            } */
            MessageBox.Show("Esta función estará disponible próximamente.", 
            "Próximamente", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void NavigateFromSearchBox()
        {
            var url = _searchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(url) || url == "Search or enter address...")
                return;

            Show(false);
            _navigationHandler.Navigate(url);
        }
    }
}