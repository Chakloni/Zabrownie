using Zabrownie.Models;
using System.Windows;

namespace Zabrownie.UI
{
    public partial class EditBookmarkDialog : Window
    {
        public string BookmarkTitle => TitleTextBox.Text;
        public string BookmarkUrl => UrlTextBox.Text;
        public string BookmarkFolder => FolderComboBox.Text;

        public EditBookmarkDialog(Bookmark bookmark)
        {
            InitializeComponent();

            TitleTextBox.Text = bookmark.Title;
            UrlTextBox.Text = bookmark.Url;
            
            FolderComboBox.Items.Add("Bookmarks Bar");
            FolderComboBox.Items.Add("Unsorted");
            FolderComboBox.Items.Add("Reading List");
            FolderComboBox.Items.Add("Work");
            FolderComboBox.Items.Add("Personal");
            
            FolderComboBox.Text = bookmark.Folder;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TitleTextBox.Text) || 
                string.IsNullOrWhiteSpace(UrlTextBox.Text))
            {
                MessageBox.Show("Title and URL are required.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
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