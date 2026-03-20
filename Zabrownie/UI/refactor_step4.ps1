$csPath = "c:\Users\edgar\Documents\palCredito\interfaz\Zabrownie\Zabrownie\UI\MainWindow.xaml.cs"
$csLines = Get-Content $csPath -Raw

# 1. Add WindowChromeHelper.Attach(this); in Window_Loaded
# Wait, searching for ThemeManager.ApplyAccentColor...
$csLines = $csLines -replace 'ThemeManager\.ApplyAccentColor\(_settingsManager\.Settings\.AccentColor\);', "ThemeManager.ApplyAccentColor(_settingsManager.Settings.AccentColor);`r`n                Helpers.WindowChromeHelper.Attach(this);"

# 2. Update CreateNewTabAsync with WebViewEventHandler
$oldNavEvents = '(?s)webView\.NavigationStarting \+=.*?webView\.SourceChanged \+=.*?\(s, e, tab\);'
$newNavEvents = @"
                var webViewHandler = new Helpers.WebViewEventHandler(tab, adBlocker);
                webViewHandler.NavigationStarted += (sender, uri) =>
                {
                    if (tab.IsActive)
                    {
                        StatusText.Text = `"Cargando: `" + uri;
                        UpdateNavigationButtons();
                    }
                };
                webViewHandler.NavigationCompleted += (sender, result) =>
                {
                    if (tab.IsActive)
                    {
                        if (result.IsSuccess)
                            StatusText.Text = `"Listo`";
                        else
                            StatusText.Text = `"Error al cargar (Error: `" + result.ErrorStatus + `")`";
                        UpdateNavigationButtons();
                    }
                    if (result.IsSuccess && !string.IsNullOrEmpty(result.Url) && result.Url != `"about:blank`" && result.Url != `"homepage`")
                    {
                        HomepageView?.AddToRecentSites(result.Title, result.Url);
                    }
                };
                webViewHandler.SourceChanged += (sender, url) =>
                {
                    if (tab.IsActive)
                    {
                        AddressBar.Text = url;
                        UpdateBookmarkButton();
                        ShowHomepage(url == `"about:blank`" || url == `"homepage`" || string.IsNullOrWhiteSpace(url));
                    }
                };
                webViewHandler.Attach(webView);
"@
$csLines = [regex]::Replace($csLines, $oldNavEvents, $newNavEvents)

# 3. Remove old WebView event handler methods
$oldWebViewMethodsPattern = '(?s)[ \t]*private void WebView_NavigationStarting.*?([ \t]*private void UpdateNavigationButtons\(\))'
$csLines = [regex]::Replace($csLines, $oldWebViewMethodsPattern, "`$1")

# 4. Replace ResizeGrip_MouseDown and remove all old Chrome logic
$oldChromeCode = '(?s)[ \t]*private void ResizeGrip_MouseDown.*?}\s*}\s*$'
$newChromeCode = @"
        private void ResizeGrip_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                Helpers.WindowChromeHelper.DragResize(this);
        }
    }
}
"@
$csLines = [regex]::Replace($csLines, $oldChromeCode, $newChromeCode)

Set-Content -Path $csPath -Value $csLines -Encoding UTF8
Write-Host "Modifications complete."
