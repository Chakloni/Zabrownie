using Zabrownie.Core;
using Zabrownie.Models;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Zabrownie.UI
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsManager _settingsManager;
        private readonly FilterEngine _filterEngine;
        private int _totalBlockedCount;

        public SettingsWindow(SettingsManager settingsManager, FilterEngine filterEngine, int blockedCount = 0)
        {
            InitializeComponent();
            
            _settingsManager = settingsManager;
            _filterEngine = filterEngine;
            _totalBlockedCount = blockedCount;
            
            LoadSettings();
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            var accentColor = _settingsManager.Settings.AccentColor;
            if (!string.IsNullOrEmpty(accentColor))
            {
                ThemeManager.ApplyAccentColor(accentColor);
            }
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
            CustomColorTextBox.Text = settings.AccentColor;

            WhitelistListBox.Items.Clear();
            foreach (var entry in settings.Whitelist)
            {
                WhitelistListBox.Items.Add(entry.Domain);
            }

            FilterStatsText.Text = $"Reglas de filtro cargadas: {_filterEngine.TotalRules}";
            BlockedCountText.Text = $"🛡️ Total de anuncios bloqueados: {_totalBlockedCount}";
        }

        private void PresetColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string color)
            {
                CustomColorTextBox.Text = color;
                ThemeManager.ApplyAccentColor(color);
            }
        }

        private void ApplyCustomColor_Click(object sender, RoutedEventArgs e)
        {
            var color = CustomColorTextBox.Text.Trim();
            
            if (!color.StartsWith("#"))
                color = "#" + color;
            
            if (ThemeManager.IsValidHexColor(color))
            {
                ThemeManager.ApplyAccentColor(color);
                MessageBox.Show("Color aplicado correctamente", "Éxito", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Color hexadecimal inválido. Use el formato: #RRGGBB", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
                if (!string.IsNullOrEmpty(domain))
                {
                    _settingsManager.RemoveFromWhitelist(domain);
                    WhitelistListBox.Items.Remove(WhitelistListBox.SelectedItem);
                }
            }
        }

        private void ClearCacheButton_Click(object sender, RoutedEventArgs e)
        {
            _filterEngine.ClearCache();
            MessageBox.Show("Caché de decisiones limpiado.", "Éxito", 
                MessageBoxButton.OK, MessageBoxImage.Information);
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
            
            var color = CustomColorTextBox.Text.Trim();
            if (!color.StartsWith("#"))
                color = "#" + color;
            
            if (ThemeManager.IsValidHexColor(color))
            {
                settings.AccentColor = color;
                ThemeManager.ApplyAccentColor(color);
            }

            await _settingsManager.SaveAsync();
            
            MessageBox.Show("Configuración guardada correctamente. Algunos cambios pueden requerir reiniciar.", 
                "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            
            // CAMBIADO: Establecer DialogResult y cerrar solo esta ventana
            DialogResult = true;
            this.Close(); // Cierra solo la ventana de configuración, NO el navegador
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // CAMBIADO: Cerrar solo esta ventana sin guardar
            DialogResult = false;
            this.Close(); // Cierra solo la ventana de configuración, NO el navegador
        }
    }
}