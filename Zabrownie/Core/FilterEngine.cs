using Zabrownie.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Zabrownie.Core
{
    public class FilterEngine
    {
        private readonly HashSet<string> _exactMatchRules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _substringRules = new List<string>();
        private readonly Dictionary<string, List<string>> _domainRules = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Regex> _regexRules = new List<Regex>();
        private readonly Dictionary<string, bool> _decisionCache = new Dictionary<string, bool>();
        private const int MaxCacheSize = 10000;
        private readonly object _lock = new object();

        public int TotalRules { get; private set; }

        public async Task LoadFiltersAsync(string filePath)
        {
            try
            {
                var lines = await FileService.LoadTextFileAsync(filePath);
                ParseRules(lines);
                LoggingService.Log($"Loaded {TotalRules} filter rules from {filePath}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to load filters from {filePath}", ex);
            }
        }

        public async Task LoadFiltersFromMultipleSourcesAsync(List<string> filePaths)
        {
            foreach (var path in filePaths)
            {
                await LoadFiltersAsync(path);
            }
        }

        private void ParseRules(string[] lines)
        {
            lock (_lock)
            {
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("!") || trimmed.StartsWith("["))
                        continue;

                    try
                    {
                        // Domain-specific rules: ||domain.com^
                        if (trimmed.StartsWith("||") && trimmed.Contains("^"))
                        {
                            var domain = trimmed.Substring(2, trimmed.IndexOf('^') - 2);
                            if (!_domainRules.ContainsKey(domain))
                                _domainRules[domain] = new List<string>();
                            _domainRules[domain].Add(trimmed);
                            TotalRules++;
                        }
                        // Regex rules (contains special regex characters)
                        else if (trimmed.Contains("*") || trimmed.Contains("?") || trimmed.Contains("["))
                        {
                            var pattern = ConvertToRegex(trimmed);
                            _regexRules.Add(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
                            TotalRules++;
                        }
                        // Exact match
                        else if (!trimmed.Contains("/") && !trimmed.Contains("."))
                        {
                            _exactMatchRules.Add(trimmed);
                            TotalRules++;
                        }
                        // Substring match
                        else
                        {
                            _substringRules.Add(trimmed.ToLowerInvariant());
                            TotalRules++;
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogError($"Failed to parse rule: {trimmed}", ex);
                    }
                }
            }
        }

        private string ConvertToRegex(string rule)
        {
            var pattern = Regex.Escape(rule)
                .Replace("\\*", ".*")
                .Replace("\\^", "([^\\w\\d_\\-.%]|$)")
                .Replace("\\|\\|", "^https?://([^/]+\\.)?");
            
            return pattern;
        }

        public bool ShouldBlock(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            lock (_lock)
            {
                if (_decisionCache.TryGetValue(url, out var cachedDecision))
                    return cachedDecision;

                var decision = CheckRules(url);
                
                if (_decisionCache.Count >= MaxCacheSize)
                {
                    var toRemove = _decisionCache.Keys.Take(MaxCacheSize / 4).ToList();
                    foreach (var key in toRemove)
                        _decisionCache.Remove(key);
                }

                _decisionCache[url] = decision;
                return decision;
            }
        }

        private bool CheckRules(string url)
        {
            var lowerUrl = url.ToLowerInvariant();

            // Check exact matches
            var segments = url.Split(new[] { '/', '?', '&', '=' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                if (_exactMatchRules.Contains(segment))
                    return true;
            }

            // Check domain-specific rules
            try
            {
                var uri = new Uri(url);
                var domain = uri.Host;
                
                foreach (var knownDomain in _domainRules.Keys)
                {
                    if (domain.Contains(knownDomain))
                        return true;
                }
            }
            catch { }

            // Check substring rules
            foreach (var rule in _substringRules)
            {
                if (lowerUrl.Contains(rule))
                    return true;
            }

            // Check regex rules (expensive, last resort)
            foreach (var regex in _regexRules)
            {
                if (regex.IsMatch(url))
                    return true;
            }

            return false;
        }

        public void ClearCache()
        {
            lock (_lock)
            {
                _decisionCache.Clear();
            }
        }
    }
}