using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Zabrownie.Core;
using Zabrownie.Models;

namespace Zabrownie.UI
{
    public partial class HistoryWindow : Window
    {
        private readonly HistoryManager _historyManager;

        public string? SelectedUrl { get; private set; }

        public HistoryWindow(HistoryManager historyManager)
        {
            InitializeComponent();
            _historyManager = historyManager;
            LoadHistory();
        }

        private void LoadHistory()
        {
            HistoryGrid.ItemsSource = null;
            HistoryGrid.ItemsSource = _historyManager.History;
        }

        private async void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("¿Estás seguro de que deseas eliminar todo el historial de navegación?", "Limpiar Historial", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                await _historyManager.ClearAllAsync();
                LoadHistory();
            }
        }

        private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = HistoryGrid.SelectedItems.Cast<HistoryItem>().ToList();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Por favor selecciona los elementos que deseas eliminar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var idsToRemove = selectedItems.Select(h => h.Id).ToList();
            await _historyManager.RemoveEntriesAsync(idsToRemove);
            LoadHistory();
        }

        private void Visit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is HistoryItem item)
            {
                SelectedUrl = item.Url;
                DialogResult = true;
                Close();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                this.DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
