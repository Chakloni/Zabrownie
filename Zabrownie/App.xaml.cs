using System.Configuration;
using System.Data;
using System.Windows;
using Zabrownie.Services;

namespace Zabrownie;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LoggingService.LogError("Unhandled WPF Exception", e.Exception);
        try 
        {
            MessageBox.Show($"FATAL ERROR:\n{e.Exception.Message}\n\nSee logs.txt for details.", "Crash", MessageBoxButton.OK, MessageBoxImage.Error);
        } catch { }
        e.Handled = false;
    }
}
