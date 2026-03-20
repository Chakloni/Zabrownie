using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Zabrownie.Models;

namespace Zabrownie.UI
{
    public partial class HomepageControl : UserControl
    {
        private DispatcherTimer? _clockTimer;
        private List<QuickLink> _quickLinks = new();
        private List<RecentSite> _recentSites = new();

        public event EventHandler<string>? NavigateRequested;

        public HomepageControl()
        {
            InitializeComponent();
            Loaded += HomepageControl_Loaded;
            Unloaded += HomepageControl_Unloaded;
        }

        private void HomepageControl_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeHomepage();
        }

        private void HomepageControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_clockTimer != null)
            {
                _clockTimer.Stop();
            }
        }

        public void FocusSearchBox()
        {
            HomepageSearchBox.Focus();
            UpdateClock();
        }

        private void InitializeHomepage()
        {
            if (_clockTimer == null)
            {
                _clockTimer = new DispatcherTimer();
                _clockTimer.Interval = TimeSpan.FromSeconds(1);
                _clockTimer.Tick += UpdateClock;
                _clockTimer.Start();
            }

            LoadQuickLinks();
            LoadRecentSites();
            UpdateClock();
        }

        private void UpdateClock(object? sender = null, EventArgs? e = null)
        {
            var now = DateTime.Now;
            TimeText.Text = now.ToString("HH:mm");
            DateText.Text = now.ToString("dddd, MMMM dd");
            DayText.Text = $"Day {now.DayOfYear} of {now.Year}";
        }

        private void LoadQuickLinks()
        {
            _quickLinks = new List<QuickLink>
            {
                new QuickLink { Title = "YouTube", Url = "https://youtube.com", Icon = "▶️" },
                new QuickLink { Title = "Netflix", Url = "https://netflix.com", Icon = "🎬" },
                new QuickLink { Title = "Spotify", Url = "https://spotify.com", Icon = "🎵" },
                new QuickLink { Title = "Gmail", Url = "https://gmail.com", Icon = "✉️" },
                new QuickLink { Title = "GitHub", Url = "https://github.com", Icon = "💻" },
                new QuickLink { Title = "Reddit", Url = "https://reddit.com", Icon = "📱" },
            };
        }

        private void LoadRecentSites()
        {
            _recentSites = new List<RecentSite>();
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

        public void AddToRecentSites(string title, string url)
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

            if (_recentSites.Count > 20)
            {
                _recentSites = _recentSites
                    .OrderByDescending(r => r.VisitedAt)
                    .Take(20)
                    .ToList();
            }

            UpdateRecentSitesUI();
        }

        private void HomepageSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (HomepageSearchBox.Text == "Search or enter address...")
            {
                HomepageSearchBox.Text = "";
                HomepageSearchBox.Foreground = (System.Windows.Media.SolidColorBrush)FindResource("TextPrimary");
            }
        }

        private void HomepageSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(HomepageSearchBox.Text))
            {
                HomepageSearchBox.Text = "Search or enter address...";
                HomepageSearchBox.Foreground = (System.Windows.Media.SolidColorBrush)FindResource("TextSecondary");
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

            NavigateRequested?.Invoke(this, url);
        }

        private void QuickLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url)
            {
                NavigateRequested?.Invoke(this, url);
            }
        }

        private void RecentSite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url)
            {
                NavigateRequested?.Invoke(this, url);
            }
        }

        // Add custom link (simple implementation)
        private void AddCustomLink_Click(object sender, RoutedEventArgs e)
        {
            /*
            var dialog = new CustomLinkWindow();
            if (dialog.ShowDialog() == true)
            {
                _quickLinks.Add(new QuickLink
                {
                    Title = dialog.LinkTitle,
                    Url = dialog.LinkUrl,
                    Icon = dialog.LinkIcon
                });

                MessageBox.Show($"Added {dialog.LinkTitle} to quick links!",
                    "Link Added",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            */
            MessageBox.Show("Functionality to be implemented or hooked up.");
        }
    }
}
