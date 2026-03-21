using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Zabrownie.Models;
using Zabrownie.Services;

namespace Zabrownie.Core
{
    public class HistoryManager
    {
        public List<HistoryItem> History { get; private set; } = new List<HistoryItem>();
        private readonly string _historyFilePath;
        private bool _isLoaded = false;

        public HistoryManager()
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Zabrownie");
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            _historyFilePath = Path.Combine(appDataPath, "history.json");
        }

        public async Task LoadAsync()
        {
            if (_isLoaded) return;

            if (File.Exists(_historyFilePath))
            {
                try
                {
                    var loadedHistory = await FileService.LoadJsonAsync<List<HistoryItem>>(_historyFilePath);
                    if (loadedHistory != null)
                    {
                        History = loadedHistory.OrderByDescending(h => h.VisitDate).ToList();
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Log($"Error loading history: {ex.Message}");
                    History = new List<HistoryItem>();
                }
            }
            _isLoaded = true;
        }

        public async Task SaveAsync()
        {
            try
            {
                // Limitar el historial a 5000 elementos para evitar lentitud
                if (History.Count > 5000)
                {
                    History = History.OrderByDescending(h => h.VisitDate).Take(5000).ToList();
                }

                await FileService.SaveJsonAsync(_historyFilePath, History);
            }
            catch (Exception ex)
            {
                LoggingService.Log($"Error saving history: {ex.Message}");
            }
        }

        public async Task AddEntryAsync(string title, string url)
        {
            // Evitar guardar URLs especiales o vacías
            if (string.IsNullOrEmpty(url) || url.StartsWith("chrome-error://") || url == "about:blank" || url == "homepage")
            {
                return;
            }

            var entry = new HistoryItem
            {
                Title = string.IsNullOrEmpty(title) ? url : title,
                Url = url,
                VisitDate = DateTime.Now
            };

            History.Insert(0, entry);
            await SaveAsync();
        }

        public async Task ClearAllAsync()
        {
            History.Clear();
            await SaveAsync();
        }

        public async Task RemoveEntriesAsync(List<string> idsToRemove)
        {
            History.RemoveAll(h => idsToRemove.Contains(h.Id));
            await SaveAsync();
        }
        
        public List<HistoryItem> GetRecentUniqueSites(int count)
        {
            return History
                .GroupBy(h => h.Url)
                .Select(g => g.First())
                .OrderByDescending(h => h.VisitDate)
                .Take(count)
                .ToList();
        }
    }
}
