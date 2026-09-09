using Microsoft.UI.Xaml;
using System;
using System.IO;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace OoplesFinance.TradingApp.Maui.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
    private static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OoplesTrading",
        "startup_log.txt");

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        LogMessage("App constructor starting");
        try
        {
            this.InitializeComponent();
            LogMessage("App InitializeComponent completed");
        }
        catch (Exception ex)
        {
            LogException("App InitializeComponent", ex);
            throw;
        }
    }

    protected override MauiApp CreateMauiApp()
    {
        LogMessage("CreateMauiApp starting");
        try
        {
            var app = OoplesFinance.TradingApp.Maui.MauiProgram.CreateMauiApp();
            LogMessage("CreateMauiApp completed successfully");
            return app;
        }
        catch (Exception ex)
        {
            LogException("CreateMauiApp", ex);
            throw;
        }
    }

    private static void LogMessage(string message)
    {
        try
        {
            var logDir = Path.GetDirectoryName(LogFilePath);
            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            File.AppendAllText(LogFilePath, $"[{timestamp}] {message}\n");
        }
        catch
        {
            // Ignore logging errors
        }
    }

    private static void LogException(string source, Exception ex)
    {
        LogMessage($"EXCEPTION in {source}: {ex.GetType().Name}: {ex.Message}");
        LogMessage($"Stack trace: {ex.StackTrace}");
        if (ex.InnerException is not null)
        {
            LogMessage($"Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            LogMessage($"Inner stack: {ex.InnerException.StackTrace}");
        }
    }
}
