using System.Windows;
using System.Windows.Controls;

namespace Zabrownie.UI
{
    public partial class CustomLinkWindow : Window
    {
        private TextBox? _titleTextBox;
        private TextBox? _urlTextBox;
        
        public string LinkTitle => _titleTextBox?.Text?.Trim() ?? "";
        public string LinkUrl => _urlTextBox?.Text?.Trim() ?? "";
        public string LinkIcon => "🔗";

        public CustomLinkWindow()
        {
            InitializeComponent();
            
            // Find controls by name
            _titleTextBox = FindName("TitleTextBox") as TextBox;
            _urlTextBox = FindName("UrlTextBox") as TextBox;
            
            if (_titleTextBox != null)
            {
                _titleTextBox.Focus();
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(LinkTitle) || string.IsNullOrWhiteSpace(LinkUrl))
            {
                MessageBox.Show("Please enter both title and URL.", 
                    "Missing Information", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}