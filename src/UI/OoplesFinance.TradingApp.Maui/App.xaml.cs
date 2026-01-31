using OoplesFinance.TradingApp.Maui.Services;

namespace OoplesFinance.TradingApp.Maui;

/// <summary>
/// Main application class for the Ooples Trading App.
/// </summary>
public partial class App : Application
{
    private readonly ISettingsService _settingsService;

    public App(ISettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());

        // Set window properties for desktop
#if WINDOWS || MACCATALYST
        window.Title = "Ooples Trading";
        window.MinimumWidth = 400;
        window.MinimumHeight = 600;
#endif

        return window;
    }

    protected override async void OnStart()
    {
        base.OnStart();

        // Apply saved theme
        var theme = await _settingsService.GetThemeAsync();
        Application.Current!.UserAppTheme = theme switch
        {
            "Light" => AppTheme.Light,
            "Dark" => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }
}
