using Microsoft.Web.WebView2.Wpf;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Zabrownie.Models
{
    public class BrowserTab : INotifyPropertyChanged, IDisposable
    {
        private string _title = "New Tab";
        private string _url = "about:blank";
        private bool _isActive;
        private bool _isLoading;
        private int _blockedCount;

        public int Id { get; set; }

        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Url
        {
            get => _url;
            set
            {
                if (_url != value)
                {
                    _url = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        public int BlockedCount
        {
            get => _blockedCount;
            set
            {
                if (_blockedCount != value)
                {
                    _blockedCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public WebView2? WebView { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            WebView?.Dispose();
            WebView = null;
        }
    }
}