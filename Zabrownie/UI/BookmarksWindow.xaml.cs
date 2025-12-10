using Zabrownie.Core;
using Zabrownie.Models;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Zabrownie.UI
{
    public partial class BookmarksWindow : Window
    {
        private readonly BookmarkManager _bookmarkManager;

        public BookmarksWindow(BookmarkManager bookmarkManager)
        {
            InitializeComponent();
            _bookmarkManager = bookmarkManager;
            LoadBookmarks();
            LoadFolders();
        }

        private void LoadFolders()
        {
            var folders = _bookmarkManager.GetFolders();
            
            FolderComboBox.Items.Clear();
            FolderComboBox.Items.Add("Bookmarks Bar");
            FolderComboBox.Items.Add("Unsorted");
            foreach (var folder in folders)
            {
                if (folder != "Bookmarks Bar" && folder != "Unsorted")
                {
                    FolderComboBox.Items.Add(folder);
                }
            }
            FolderComboBox.SelectedIndex = 0;

            FilterFolderComboBox.Items.Clear();
            FilterFolderComboBox.Items.Add("All");
            foreach (var item in FolderComboBox.Items)
            {
                FilterFolderComboBox.Items.Add(item);
            }
            FilterFolderComboBox.SelectedIndex = 0;
        }

        private void LoadBookmarks(string? folderFilter = null)
        {
            var bookmarks = _bookmarkManager.Bookmarks.AsEnumerable();
            
            if (!string.IsNullOrEmpty(folderFilter) && folderFilter != "All")
            {
                bookmarks = bookmarks.Where(b => b.Folder == folderFilter);
            }
            
            BookmarksGrid.ItemsSource = bookmarks.ToList();
        }

        private async void AddBookmark_Click(object sender, RoutedEventArgs e)
        {
            var title = NewBookmarkTitle.Text.Trim();
            var url = NewBookmarkUrl.Text.Trim();
            var folder = FolderComboBox.Text.Trim();

            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(url))
            {
                MessageBox.Show("Please enter both title and URL.", "Invalid Input", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _bookmarkManager.AddBookmark(title, url, folder);
            await _bookmarkManager.SaveAsync();

            NewBookmarkTitle.Clear();
            NewBookmarkUrl.Clear();
            
            LoadFolders();
            LoadBookmarks();
            
            MessageBox.Show("Bookmark added successfully.", "Success", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void EditBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Bookmark bookmark)
            {
                var dialog = new EditBookmarkDialog(bookmark);
                dialog.Owner = this;
                
                if (dialog.ShowDialog() == true)
                {
                    _bookmarkManager.UpdateBookmark(
                        bookmark.Id,
                        dialog.BookmarkTitle,
                        dialog.BookmarkUrl,
                        dialog.BookmarkFolder);
                    
                    await _bookmarkManager.SaveAsync();
                    LoadFolders();
                    LoadBookmarks();
                }
            }
        }

        private async void DeleteBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Bookmark bookmark)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete '{bookmark.Title}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _bookmarkManager.RemoveBookmark(bookmark.Id);
                    await _bookmarkManager.SaveAsync();
                    LoadFolders();
                    LoadBookmarks();
                }
            }
        }

        private void FilterFolder_Changed(object sender, SelectionChangedEventArgs e)
        {
            var selectedFolder = FilterFolderComboBox.SelectedItem?.ToString();
            LoadBookmarks(selectedFolder);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}