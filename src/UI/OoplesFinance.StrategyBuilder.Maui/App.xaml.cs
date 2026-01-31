namespace OoplesFinance.StrategyBuilder.Maui;

/// <summary>
/// Main application class for the Ooples Strategy Builder MAUI app.
/// </summary>
public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell())
        {
            Title = "Ooples Strategy Builder",
            MinimumWidth = 1200,
            MinimumHeight = 800
        };
    }
}
