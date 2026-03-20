$xamlPath = "c:\Users\edgar\Documents\palCredito\interfaz\Zabrownie\Zabrownie\UI\MainWindow.xaml"
$csPath = "c:\Users\edgar\Documents\palCredito\interfaz\Zabrownie\Zabrownie\UI\MainWindow.xaml.cs"

# 1. Update XAML
$xamlLines = Get-Content $xamlPath -Raw

# Add xmlns:local
$xamlLines = $xamlLines -replace 'xmlns:wv2="clr-namespace:Microsoft\.Web\.WebView2\.Wpf;assembly=Microsoft\.Web\.WebView2\.Wpf"', 
  "xmlns:wv2=`"clr-namespace:Microsoft.Web.WebView2.Wpf;assembly=Microsoft.Web.WebView2.Wpf`"`r`n        xmlns:local=`"clr-namespace:Zabrownie.UI`""

# Replace HomepageGrid with HomepageControl
$pattern = '(?s)[ \t]*<Grid x:Name="HomepageGrid".*?<!-- WebView Container'
$replacement = @"
                <local:HomepageControl x:Name="HomepageView" 
                                       NavigateRequested="HomepageView_NavigateRequested" 
                                       Visibility="Collapsed" />

                <!-- WebView Container
"@
$xamlLines = [regex]::Replace($xamlLines, $pattern, $replacement)

Set-Content -Path $xamlPath -Value $xamlLines -Encoding UTF8

# 2. Update CS
$csLines = Get-Content $csPath -Raw

# Fields to remove
$fieldsPattern = '(?s)[ \t]*// Homepage-related fields.*?private Stack<BrowserTab> _closedTabs = new\(\);'
$csLines = [regex]::Replace($csLines, $fieldsPattern, '        private Stack<BrowserTab> _closedTabs = new();')

# InitializeHomepage call to remove
$csLines = $csLines -replace '\s*// Initialize homepage\s*InitializeHomepage\(\);', ''

# AddToRecentSites inside WebView_NavigationCompleted
$csLines = $csLines -replace 'AddToRecentSites\(tab\.Title, tab\.Url\);', 'HomepageView?.AddToRecentSites(tab.Title, tab.Url);'

# Homepage functionality methods
# We will match from "// ===== HOMEPAGE FUNCTIONALITY =====" up to "private void HomeButton_Click... }"
# But we MUST leave ShowHomepage, or we can just replace ALL of it and inject our new ShowHomepage and HomepageView_NavigateRequested.
$methodsPattern = '(?s)[ \t]*// ===== HOMEPAGE FUNCTIONALITY =====.*?private void HomeButton_Click[^}]+}'

$newMethods = @"
        // ===== HOMEPAGE FUNCTIONALITY =====

        private void ShowHomepage(bool show = true)
        {
            if (HomepageView != null)
                HomepageView.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            
            if (WebViewContainerGrid != null)
                WebViewContainerGrid.Visibility = show ? Visibility.Collapsed : Visibility.Visible;

            if (show && HomepageView != null)
            {
                HomepageView.FocusSearchBox();
            }
        }

        private void HomepageView_NavigateRequested(object? sender, string url)
        {
            ShowHomepage(false);
            NavigateToUrl(url);
        }
"@
$csLines = [regex]::Replace($csLines, $methodsPattern, $newMethods)

# Furthermore, fixing the Window_Closing issue where _clockTimer was stopped - not needed anymore.
$closingPattern = '(?s)[ \t]*// Stop the clock timer.*?\}'
$csLines = [regex]::Replace($csLines, $closingPattern, '')

Set-Content -Path $csPath -Value $csLines -Encoding UTF8

Write-Host "Modifications complete."
