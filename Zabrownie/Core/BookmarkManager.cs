    using Zabrownie.Models;
using Zabrownie.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Zabrownie.Core
{
    public class BookmarkManager
    {
        private static readonly string BookmarksPath = Path.Combine(
            FileService.GetAppDataPath(), "bookmarks.json");

        public ObservableCollection<Bookmark> Bookmarks { get; }

        public BookmarkManager()
        {
            Bookmarks = new ObservableCollection<Bookmark>();
        }

        public async Task LoadAsync()
        {
            try
            {
                if (File.Exists(BookmarksPath))
                {
                    var json = await File.ReadAllTextAsync(BookmarksPath);
                    var bookmarks = JsonSerializer.Deserialize<Bookmark[]>(json);

                    Bookmarks.Clear();
                    if (bookmarks != null)
                    {
                        foreach (var bookmark in bookmarks)
                        {
                            Bookmarks.Add(bookmark);
                        }
                    }

                    LoggingService.Log($"Loaded {Bookmarks.Count} bookmarks");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to load bookmarks", ex);
            }
        }

        public async Task SaveAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(Bookmarks.ToArray(), new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var directory = Path.GetDirectoryName(BookmarksPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                    await File.WriteAllTextAsync(BookmarksPath, json);
                    LoggingService.Log("Bookmarks saved successfully");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to save bookmarks", ex);
            }
        }

        public void AddBookmark(string title, string url, string? folder = null)
        {
            var bookmark = new Bookmark
            {
                Id = Guid.NewGuid().ToString(),
                Title = title,
                Url = url,
                Folder = folder ?? "Unsorted",
                DateAdded = DateTime.Now
            };

            Bookmarks.Add(bookmark);
        }

        public void RemoveBookmark(string id)
        {
            var bookmark = Bookmarks.FirstOrDefault(b => b.Id == id);
            if (bookmark != null)
            {
                Bookmarks.Remove(bookmark);
            }
        }

        public void UpdateBookmark(string id, string title, string url, string? folder = null)
        {
            var bookmark = Bookmarks.FirstOrDefault(b => b.Id == id);
            if (bookmark != null)
            {
                bookmark.Title = title;
                bookmark.Url = url;
                bookmark.Folder = folder ?? bookmark.Folder;
            }
        }

        public Bookmark? FindByUrl(string url)
        {
            return Bookmarks.FirstOrDefault(b => b.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
        }

        public string[] GetFolders()
        {
            return Bookmarks.Select(b => b.Folder)
                           .Distinct()
                           .OrderBy(f => f)
                           .ToArray();
        }
    }
}