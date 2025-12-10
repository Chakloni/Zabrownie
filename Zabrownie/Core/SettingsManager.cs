using Zabrownie.Models;
using Zabrownie.Services;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Zabrownie.Core
{
    public class SettingsManager
    {
        private static readonly string SettingsPath = Path.Combine(
            FileService.GetAppDataPath(), "settings.json");

        private AppSettings _settings;
        private readonly object _lock = new object();

        public AppSettings Settings
        {
            get
            {
                lock (_lock)
                {
                    return _settings;
                }
            }
        }

        public SettingsManager()
        {
            _settings = new AppSettings();
        }

        public async Task LoadAsync()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = await File.ReadAllTextAsync(SettingsPath);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    
                    lock (_lock)
                    {
                        _settings = loaded ?? new AppSettings();
                    }

                    LoggingService.Log("Settings loaded successfully");
                }
                else
                {
                    LoggingService.Log("No settings file found, using defaults");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to load settings", ex);
                lock (_lock)
                {
                    _settings = new AppSettings();
                }
            }
        }

        public async Task SaveAsync()
        {
            try
            {
                AppSettings toSave;
                lock (_lock)
                {
                    toSave = _settings;
                }

                var json = JsonSerializer.Serialize(toSave, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });

                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                    await File.WriteAllTextAsync(SettingsPath, json);
                    LoggingService.Log("Settings saved successfully");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to save settings", ex);
            }
        }

        public bool IsWhitelisted(string domain)
        {
            lock (_lock)
            {
                foreach (var entry in _settings.Whitelist)
                {
                    if (entry.Enabled && domain.Contains(entry.Domain, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }
        }

        public void AddToWhitelist(string domain)
        {
            lock (_lock)
            {
                if (!_settings.Whitelist.Exists(e => e.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase)))
                {
                    _settings.Whitelist.Add(new SiteWhitelistEntry { Domain = domain });
                }
            }
        }

        public void RemoveFromWhitelist(string domain)
        {
            lock (_lock)
            {
                _settings.Whitelist.RemoveAll(e => e.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase));
            }
        }

        public bool IsJavaScriptEnabled(string domain)
        {
            lock (_lock)
            {
                if (_settings.PerSiteJavaScript.TryGetValue(domain, out var enabled))
                    return enabled;
                return _settings.EnableJavaScript;
            }
        }

        public void SetJavaScriptForSite(string domain, bool enabled)
        {
            lock (_lock)
            {
                _settings.PerSiteJavaScript[domain] = enabled;
            }
        }

        public string StripTrackingParameters(string url)
        {
            if (!_settings.StripTrackingParams)
                return url;

            try
            {
                var uri = new Uri(url);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                
                string[] trackingParams = { 
                    "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content",
                    "gclid", "fbclid", "msclkid", "_ga", "mc_eid"
                };

                foreach (var param in trackingParams)
                {
                    query.Remove(param);
                }

                var builder = new UriBuilder(uri)
                {
                    Query = query.ToString()
                };

                return builder.Uri.ToString();
            }
            catch
            {
                return url;
            }
        }
    }
}