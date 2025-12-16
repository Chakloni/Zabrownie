using Zabrownie.Core;
using Zabrownie.UI;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Zabrownie.Handlers
{
    public class BookmarkHandler
    {
        private readonly BookmarkManager _bookmarkManager;
        private readonly TabManager _tabManager;
        private readonly ItemsControl _bookmarksBarControl;
        private readonly UIElement _bookmarksBar;
        private readonly Button _bookmarkButton;
        private readonly NavigationHandler _navigationHandler;

        public BookmarkHandler(
            BookmarkManager bookmarkManager,
            TabManager tabManager,
            ItemsControl bookmarksBarControl,
            UIElement bookmarksBar,
            Button bookmarkButton,
            NavigationHandler navigationHandler)
        {
            _bookmarkManager = bookmarkManager;
            _tabManager = tabManager;
            _bookmarksBarControl = bookmarksBarControl;
            _bookmarksBar = bookmarksBar;
            _bookmarkButton = bookmarkButton;
            _navigationHandler = navigationHandler;
        }

        public void UpdateBookmarksBar()
        {
            _bookmarksBarControl.ItemsSource = _bookmarkManager.Bookmarks
                .Where(b => b.Folder == "Bookmarks Bar")
                .ToList();

            _bookmarksBar.Visibility = _bookmarksBarControl.HasItems
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public void UpdateBookmarkButton()
        {
            var currentUrl = _tabManager.ActiveTab?.Url ?? "";
            var isBookmarked = _bookmarkManager.FindByUrl(currentUrl) != null;
            _bookmarkButton.Content = isBookmarked ? "★" : "☆";
        }

        public async void OnBookmarkButtonClick(object sender, RoutedEventArgs e)
        {
            var currentUrl = _tabManager.ActiveTab?.Url ?? "";
            var currentTitle = _tabManager.ActiveTab?.Title ?? "Nueva Pestaña";

            if (string.IsNullOrWhiteSpace(currentUrl) || 
                currentUrl == "about:blank" || 
                currentUrl == "homepage")
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

        public void OnBookmarkBarItemClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url)
            {
                _navigationHandler.Navigate(url);
            }
        }

        public void OnManageBookmarksClick(object sender, RoutedEventArgs e, Window owner)
        {
            var bookmarksWindow = new BookmarksWindow(_bookmarkManager)
            {
                Owner = owner
            };
            bookmarksWindow.ShowDialog();
            UpdateBookmarksBar();
        }
    }
}