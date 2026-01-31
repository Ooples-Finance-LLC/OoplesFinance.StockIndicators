using OoplesFinance.TradingApp.Maui.Views;
using OoplesFinance.TradingApp.Maui.Views.Onboarding;

namespace OoplesFinance.TradingApp.Maui;

/// <summary>
/// Application shell with tab-based navigation for mobile trading.
/// </summary>
public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register additional routes for navigation
        Routing.RegisterRoute("orderHistory", typeof(OrderHistoryPage));
        Routing.RegisterRoute("alerts", typeof(AlertsPage));
        Routing.RegisterRoute("chart", typeof(ChartPage));
        Routing.RegisterRoute("strategyMonitor", typeof(StrategyMonitorPage));
        Routing.RegisterRoute("brokerConnection", typeof(BrokerConnectionPage));
        Routing.RegisterRoute("positionDetail", typeof(PositionDetailPage));
        Routing.RegisterRoute("settings", typeof(SettingsPage));
        Routing.RegisterRoute("addAlert", typeof(AlertsPage)); // Reuse alerts page for adding

        // Onboarding routes
        Routing.RegisterRoute(nameof(WelcomePage), typeof(WelcomePage));
        Routing.RegisterRoute(nameof(RiskQuizPage), typeof(RiskQuizPage));
        Routing.RegisterRoute(nameof(GoalSetupPage), typeof(GoalSetupPage));
        Routing.RegisterRoute(nameof(RiskSliderPage), typeof(RiskSliderPage));
    }
}
