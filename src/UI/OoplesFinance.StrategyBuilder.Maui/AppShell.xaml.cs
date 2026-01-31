using OoplesFinance.StrategyBuilder.Maui.Views;

namespace OoplesFinance.StrategyBuilder.Maui;

/// <summary>
/// Application shell defining the navigation structure.
/// </summary>
public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for navigation
        Routing.RegisterRoute(nameof(BacktestResultsPage), typeof(BacktestResultsPage));
    }
}
