using Zabrownie.Core;
using Zabrownie.Models;
using System.Linq;
using System.Windows;

namespace Zabrownie.UI
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsManager _settingsManager;
        private readonly FilterEngine _filterEngine;

        public SettingsWindow(SettingsManager settingsManager, FilterEngine filterEngine)
        {
            InitializeComponent();
            
            _settingsManager = settingsManager;
            _filterEngine = filterEngine;
            
            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = _settingsManager.Settings;
            
            EnableAdBlockingCheckBox.IsChecked = settings.EnableAdBlocking;
            StripTrackingParamsCheckBox.IsChecked = settings.StripTrackingParams;
            BlockThirdPartyCookiesCheckBox.IsChecked = settings.BlockThirdPartyCookies;
            ClearDataOnCloseCheckBox.IsChecked = settings.ClearDataOnClose;
            EnableJavaScriptCheckBox.IsChecked = settings.EnableJavaScript;
            HomepageTextBox.Text = settings.Homepage;
            UserAgentTextBox.Text = settings.UserAgent;

            WhitelistListBox.Items.Clear();
            foreach (var entry in settings.Whitelist)
            {
                WhitelistListBox.Items.Add(entry.Domain);
            }

            FilterStatsText.Text = $"Total filter rules loaded: {_filterEngine.TotalRules}";
        }

        private void AddWhitelistButton_Click(object sender, RoutedEventArgs e)
        {
            var domain = WhitelistDomainTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(domain))
            {
                _settingsManager.AddToWhitelist(domain);
                WhitelistListBox.Items.Add(domain);
                WhitelistDomainTextBox.Clear();
            }
        }

        private void RemoveWhitelistButton_Click(object sender, RoutedEventArgs e)
        {
            if (WhitelistListBox.SelectedItem != null)
            {
                var domain = WhitelistListBox.SelectedItem.ToString();
                _settingsManager.RemoveFromWhitelist(domain);
                WhitelistListBox.Items.Remove(WhitelistListBox.SelectedItem);
            }
        }

        private void ClearCacheButton_Click(object sender, RoutedEventArgs e)
        {
            _filterEngine.ClearCache();
            MessageBox.Show("Decision cache cleared.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = _settingsManager.Settings;
            
            settings.EnableAdBlocking = EnableAdBlockingCheckBox.IsChecked ?? true;
            settings.StripTrackingParams = StripTrackingParamsCheckBox.IsChecked ?? true;
            settings.BlockThirdPartyCookies = BlockThirdPartyCookiesCheckBox.IsChecked ?? true;
            settings.ClearDataOnClose = ClearDataOnCloseCheckBox.IsChecked ?? false;
            settings.EnableJavaScript = EnableJavaScriptCheckBox.IsChecked ?? true;
            settings.Homepage = HomepageTextBox.Text;
            settings.UserAgent = UserAgentTextBox.Text;

            await _settingsManager.SaveAsync();
            
            MessageBox.Show("Settings saved successfully. Some changes may require a restart.", 
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}