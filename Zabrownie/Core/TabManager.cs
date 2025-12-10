using Zabrownie.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace Zabrownie.Core
{
    public class TabManager
    {
        public ObservableCollection<BrowserTab> Tabs { get; }
        private int _nextTabId = 1;

        public BrowserTab? ActiveTab { get; private set; }

        public TabManager()
        {
            Tabs = new ObservableCollection<BrowserTab>();
        }

        public BrowserTab CreateTab(string url = "about:blank")
        {
            var tab = new BrowserTab
            {
                Id = _nextTabId++,
                Title = "New Tab",
                Url = url,
                IsActive = Tabs.Count == 0
            };

            Tabs.Add(tab);

            if (tab.IsActive)
            {
                SetActiveTab(tab);
            }

            return tab;
        }

        public void CloseTab(BrowserTab tab)
        {
            if (Tabs.Count <= 1)
                return; // Don't close last tab

            var index = Tabs.IndexOf(tab);
            Tabs.Remove(tab);

            // If we closed the active tab, activate another
            if (tab.IsActive && Tabs.Count > 0)
            {
                var newActiveIndex = index >= Tabs.Count ? Tabs.Count - 1 : index;
                SetActiveTab(Tabs[newActiveIndex]);
            }

            tab.Dispose();
        }

        public void SetActiveTab(BrowserTab tab)
        {
            if (ActiveTab != null)
            {
                ActiveTab.IsActive = false;
            }

            ActiveTab = tab;
            tab.IsActive = true;

            foreach (var t in Tabs.Where(t => t != tab))
            {
                t.IsActive = false;
            }
        }

        public void CloseAllTabs()
        {
            foreach (var tab in Tabs.ToList())
            {
                tab.Dispose();
            }
            Tabs.Clear();
            ActiveTab = null;
        }
    }
}