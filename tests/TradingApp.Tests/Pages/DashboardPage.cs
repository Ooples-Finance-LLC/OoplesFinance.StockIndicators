using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace OoplesFinance.TradingApp.UITests.Pages;

/// <summary>
/// Page object for the Dashboard page
/// </summary>
public class DashboardPage : BasePage
{
    public DashboardPage(Window window, UIA3Automation automation) : base(window, automation) { }

    // Elements
    public AutomationElement? PortfolioValueLabel => FindByText("Portfolio Value");
    public AutomationElement? TodayPnLLabel => FindByText("Today's P/L");
    public AutomationElement? TotalPnLLabel => FindByText("Total P/L");
    public AutomationElement? BuyingPowerLabel => FindByText("Buying Power");
    public AutomationElement? PositionsCard => FindByText("Positions");
    public AutomationElement? OrdersCard => FindByText("Orders");
    public AutomationElement? AlertsCard => FindByText("Alerts");
    public AutomationElement? MarketStatusIndicator => FindByText("Market");
    public AutomationElement? UIModeIndicator => FindByText("Beginner") ?? FindByText("Intermediate") ?? FindByText("Expert");
    public AutomationElement? TopPositionsSection => FindByText("Top Positions");
    public AutomationElement? RecentActivitySection => FindByText("Recent Activity");
    public AutomationElement? AIInsightsSection => FindByText("AI Insights");
    public AutomationElement? StrategyBuilderSection => FindByText("Strategy Builder");

    // Actions
    public void ClickBuyButton() => ClickButton("Buy");
    public void ClickSellButton() => ClickButton("Sell");
    public void ClickDepositButton() => ClickButton("Deposit");
    public void ClickRefresh() => ClickButton("Refresh");
    public void ClickSeeAllPositions() => ClickButton("See All");

    public void ToggleUIMode()
    {
        var modeIndicator = UIModeIndicator;
        modeIndicator?.Click();
        Thread.Sleep(500);
    }

    // Verifications
    public bool IsLoaded => PortfolioValueLabel != null;
    public bool HasPortfolioData => FindByText("$") != null;
    public bool HasTopPositions => TopPositionsSection != null;
    public bool HasRecentActivity => RecentActivitySection != null;
    public bool HasAIInsights => AIInsightsSection != null;
    public bool HasStrategyBuilder => StrategyBuilderSection != null;

    public string? GetCurrentUIMode()
    {
        if (FindByText("Beginner", TimeSpan.FromSeconds(1)) != null) return "Beginner";
        if (FindByText("Intermediate", TimeSpan.FromSeconds(1)) != null) return "Intermediate";
        if (FindByText("Expert", TimeSpan.FromSeconds(1)) != null) return "Expert";
        return null;
    }

    public decimal? GetPortfolioValue()
    {
        // Find element with dollar amount pattern
        var elements = Window.FindAllDescendants();
        var valueElement = elements.FirstOrDefault(e =>
            e.Name?.StartsWith("$") == true &&
            decimal.TryParse(e.Name.Replace("$", "").Replace(",", ""), out _));

        if (valueElement?.Name != null)
        {
            var valueText = valueElement.Name.Replace("$", "").Replace(",", "");
            if (decimal.TryParse(valueText, out var value))
                return value;
        }
        return null;
    }
}
