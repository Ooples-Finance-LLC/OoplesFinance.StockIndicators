using OoplesFinance.TradingApp.Maui.Services;
using System.Diagnostics;

namespace OoplesFinance.TradingApp.Maui;

/// <summary>
/// Main application class for the Ooples Trading App.
/// </summary>
public partial class App : Application
{
    private readonly ISettingsService _settingsService;
    private static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OoplesTrading",
        "error_log.txt");

    public App(ISettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;

        // Set up global exception handling
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // Ensure log directory exists
        var logDir = Path.GetDirectoryName(LogFilePath);
        if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        LogError("App", "Application started");
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        LogError("UnhandledException", ex?.ToString() ?? "Unknown error");
        ShowErrorDialog("Unhandled Exception", ex);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogError("UnobservedTaskException", e.Exception.ToString());
        e.SetObserved(); // Prevent the app from crashing
        ShowErrorDialog("Task Exception", e.Exception);
    }

    public static void LogError(string source, string message)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] [{source}] {message}\n";
            File.AppendAllText(LogFilePath, logMessage);
            Debug.WriteLine(logMessage);
        }
        catch
        {
            // Ignore logging errors
        }
    }

    public static void LogException(string source, Exception ex)
    {
        LogError(source, $"{ex.GetType().Name}: {ex.Message}\nStack: {ex.StackTrace}");
        if (ex.InnerException is not null)
        {
            LogError(source, $"Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
        }
    }

    private async void ShowErrorDialog(string title, Exception? ex)
    {
        try
        {
            if (Windows.Count > 0 && Windows[0].Page is not null)
            {
                await Windows[0].Page.DisplayAlert(
                    title,
                    $"{ex?.GetType().Name}: {ex?.Message}\n\nLog saved to: {LogFilePath}",
                    "OK");
            }
        }
        catch
        {
            // Ignore UI errors when showing error dialog
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        try
        {
            LogError("CreateWindow", "Creating main window");
            var window = new Window(new AppShell());

            // Set window properties for desktop
#if WINDOWS || MACCATALYST
            window.Title = "Ooples Trading";
            window.MinimumWidth = 400;
            window.MinimumHeight = 600;
#endif

            LogError("CreateWindow", "Window created successfully");
            return window;
        }
        catch (Exception ex)
        {
            LogException("CreateWindow", ex);
            throw;
        }
    }

    protected override async void OnStart()
    {
        try
        {
            base.OnStart();
            LogError("OnStart", "Application starting");

            // Apply saved theme
            var theme = await _settingsService.GetThemeAsync();
            Application.Current!.UserAppTheme = theme switch
            {
                "Light" => AppTheme.Light,
                "Dark" => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };

            LogError("OnStart", $"Theme applied: {theme}");

            // Check if onboarding has been completed
            bool onboardingComplete = Preferences.Get("onboarding_complete", false);
            LogError("OnStart", $"Onboarding complete: {onboardingComplete}");

            if (!onboardingComplete)
            {
                // Navigate to onboarding flow
                LogError("OnStart", "Navigating to onboarding...");
                await Shell.Current.GoToAsync("WelcomePage");
            }
        }
        catch (Exception ex)
        {
            LogException("OnStart", ex);
        }
    }
}
