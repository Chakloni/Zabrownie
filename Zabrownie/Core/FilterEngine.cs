using DistillNET;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Zabrownie.Services;

namespace Zabrownie.Core
{
    public class FilterEngine
    {
        private readonly List<Filter> _filters = new List<Filter>();
        private readonly object _lock = new object();

        public int TotalRules { get; private set; }

        public async Task LoadFiltersAsync(string filePath)
        {
            try
            {
                var lines = await FileService.LoadTextFileAsync(filePath);
                await LoadFromLinesAsync(lines);
                LoggingService.Log($"Loaded {TotalRules} filter rules from {filePath}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to load filters from {filePath}", ex);
            }
        }

        public async Task LoadFiltersFromMultipleSourcesAsync(List<string> filePaths)
        {
            var allLines = new List<string>();
            
            foreach (var path in filePaths)
            {
                try
                {
                    var lines = await FileService.LoadTextFileAsync(path);
                    allLines.AddRange(lines);
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"Failed to load filters from {path}", ex);
                }
            }
            
            if (allLines.Count > 0)
            {
                await LoadFromLinesAsync(allLines.ToArray());
            }
        }

        private Task LoadFromLinesAsync(string[] lines)
        {
            return Task.Run(() =>
            {
                lock (_lock)
                {
                    try
                    {
                        _filters.Clear();

                        // Filtrar líneas válidas
                        var validRules = lines
                            .Select(line => line.Trim())
                            .Where(line => !string.IsNullOrWhiteSpace(line) && 
                                         !line.StartsWith("!") && 
                                         !line.StartsWith("["))
                            .ToList();

                        TotalRules = validRules.Count;

                        if (validRules.Count == 0)
                        {
                            LoggingService.Log("No valid filter rules found");
                            return;
                        }

                        // Parsear y agregar reglas usando AbpFormatRuleParser
                        var parser = new AbpFormatRuleParser();
                        int addedCount = 0;

                        foreach (var rule in validRules)
                        {
                            try
                            {
                                // ParseAbpFormattedRule(string rule, short categoryId)
                                var parsedFilter = parser.ParseAbpFormattedRule(rule, 1);
                                
                                if (parsedFilter != null)
                                {
                                    _filters.Add(parsedFilter);
                                    addedCount++;
                                }
                            }
                            catch
                            {
                                // Ignorar reglas inválidas silenciosamente
                            }
                        }

                        LoggingService.Log($"DistillNET initialized with {addedCount} valid filters from {TotalRules} rules");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogError("Failed to initialize DistillNET", ex);
                    }
                }
            });
        }

        public bool ShouldBlock(string requestUrl, string documentUrl, CoreWebView2WebResourceContext resourceContext)
        {
            if (_filters.Count == 0 || string.IsNullOrEmpty(requestUrl))
                return false;

            lock (_lock)
            {
                try
                {
                    // Validar URLs
                    if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out var requestUri))
                        return false;

                    Uri? documentUri = null;
                    if (!string.IsNullOrEmpty(documentUrl))
                    {
                        Uri.TryCreate(documentUrl, UriKind.Absolute, out documentUri);
                    }

                    var requestUrlLower = requestUri.AbsoluteUri.ToLowerInvariant();
                    var sourceHost = documentUri?.Host ?? requestUri.Host;

                    // Verificar filtros de excepción primero (whitelist)
                    foreach (var filter in _filters.Where(f => f.IsException))
                    {
                        if (filter is UrlFilter urlFilter && MatchesUrlFilter(urlFilter, requestUrlLower, sourceHost))
                        {
                            return false; // Está en whitelist, no bloquear
                        }
                    }

                    // Verificar filtros de bloqueo
                    foreach (var filter in _filters.Where(f => !f.IsException))
                    {
                        if (filter is UrlFilter urlFilter && MatchesUrlFilter(urlFilter, requestUrlLower, sourceHost))
                        {
                            return true; // Debe bloquearse
                        }
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"Error checking filter for {requestUrl}", ex);
                    return false;
                }
            }
        }

        private bool MatchesUrlFilter(UrlFilter filter, string url, string sourceHost)
        {
            try
            {
                // Obtener la regla original
                var rule = filter.OriginalRule.ToLowerInvariant();
                
                // Remover caracteres especiales de ABP para matching simple
                var cleanRule = rule
                    .Replace("||", "")
                    .Replace("^", "")
                    .Replace("@@", "")
                    .Trim();

                // Si está vacío después de limpiar, ignorar
                if (string.IsNullOrEmpty(cleanRule))
                    return false;

                // Si la regla tiene wildcards
                if (cleanRule.Contains("*"))
                {
                    try
                    {
                        // Convertir a regex simple
                        var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(cleanRule)
                            .Replace("\\*", ".*");
                        
                        return System.Text.RegularExpressions.Regex.IsMatch(url, pattern);
                    }
                    catch
                    {
                        // Si falla el regex, usar substring
                        cleanRule = cleanRule.Replace("*", "");
                        return url.Contains(cleanRule);
                    }
                }
                
                // Matching simple por substring
                return url.Contains(cleanRule);
            }
            catch
            {
                return false;
            }
        }

        public void ClearCache()
        {
            LoggingService.Log("Cache clear requested");
        }
    }
}