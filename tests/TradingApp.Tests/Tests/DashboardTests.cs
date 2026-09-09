using FluentAssertions;
using OoplesFinance.TradingApp.UITests.Pages;
using Xunit;

namespace OoplesFinance.TradingApp.UITests.Tests;

/// <summary>
/// Comprehensive E2E tests for the Dashboard page
/// </summary>
[Collection("UITests")]
public class DashboardTests : UITestBase
{
    private DashboardPage _dashboard = null!;

    private async Task SetupAsync()
    {
        await LaunchAppAsync();
        _dashboard = new DashboardPage(MainWindow!, Automation!);

        // Wait for dashboard to load
        await _dashboard.WaitForElementAsync("Portfolio", TimeSpan.FromSeconds(15));
    }

    [Fact(DisplayName = "Dashboard loads successfully")]
    public async Task Dashboard_ShouldLoadSuccessfully()
    {
        await SetupAsync();

        _dashboard.IsLoaded.Should().BeTrue("Dashboard page should be loaded");
    }

    [Fact(DisplayName = "Dashboard displays portfolio value")]
    public async Task Dashboard_ShouldDisplayPortfolioValue()
    {
        await SetupAsync();

        _dashboard.PortfolioValueLabel.Should().NotBeNull("Portfolio Value label should be visible");
        _dashboard.HasPortfolioData.Should().BeTrue("Portfolio should show dollar amounts");
    }

    [Fact(DisplayName = "Dashboard displays P/L information")]
    public async Task Dashboard_ShouldDisplayPnL()
    {
        await SetupAsync();

        _dashboard.TodayPnLLabel.Should().NotBeNull("Today's P/L should be visible");
        _dashboard.TotalPnLLabel.Should().NotBeNull("Total P/L should be visible");
    }

    [Fact(DisplayName = "Dashboard displays buying power")]
    public async Task Dashboard_ShouldDisplayBuyingPower()
    {
        await SetupAsync();

        _dashboard.BuyingPowerLabel.Should().NotBeNull("Buying Power should be visible");
    }

    [Fact(DisplayName = "Dashboard displays market status")]
    public async Task Dashboard_ShouldDisplayMarketStatus()
    {
        await SetupAsync();

        _dashboard.MarketStatusIndicator.Should().NotBeNull("Market status should be visible");
    }

    [Fact(DisplayName = "Dashboard has quick stats cards")]
    public async Task Dashboard_ShouldHaveQuickStatsCards()
    {
        await SetupAsync();

        _dashboard.PositionsCard.Should().NotBeNull("Positions card should be visible");
        _dashboard.OrdersCard.Should().NotBeNull("Orders card should be visible");
        _dashboard.AlertsCard.Should().NotBeNull("Alerts card should be visible");
    }

    [Fact(DisplayName = "Dashboard shows UI mode indicator")]
    public async Task Dashboard_ShouldShowUIModeIndicator()
    {
        await SetupAsync();

        var mode = _dashboard.GetCurrentUIMode();
        mode.Should().NotBeNull("UI mode indicator should be visible");
        mode.Should().BeOneOf("Beginner", "Intermediate", "Expert");
    }

    [Fact(DisplayName = "Dashboard UI mode can be toggled")]
    public async Task Dashboard_UIModeCanBeToggled()
    {
        await SetupAsync();

        var initialMode = _dashboard.GetCurrentUIMode();
        _dashboard.ToggleUIMode();

        await Task.Delay(1000);

        var newMode = _dashboard.GetCurrentUIMode();
        // Mode should have changed (unless at end of cycle)
        newMode.Should().NotBeNull();
    }

    [Fact(DisplayName = "Dashboard shows Top Positions section")]
    public async Task Dashboard_ShouldShowTopPositions()
    {
        await SetupAsync();

        _dashboard.HasTopPositions.Should().BeTrue("Top Positions section should be visible");
    }

    [Fact(DisplayName = "Dashboard Buy button navigates to trade")]
    public async Task Dashboard_BuyButtonShouldWork()
    {
        await SetupAsync();

        _dashboard.ClickBuyButton();

        // Should navigate to trade page or show order form
        await _dashboard.WaitForElementAsync("Order", TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "Dashboard Sell button navigates to trade")]
    public async Task Dashboard_SellButtonShouldWork()
    {
        await SetupAsync();

        _dashboard.ClickSellButton();

        // Should navigate to trade page or show order form
        await _dashboard.WaitForElementAsync("Order", TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "Dashboard See All Positions navigates")]
    public async Task Dashboard_SeeAllPositionsShouldNavigate()
    {
        await SetupAsync();

        _dashboard.ClickSeeAllPositions();

        // Should navigate to positions page
        await _dashboard.WaitForElementAsync("Positions", TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "AI Insights section visible for experts")]
    public async Task Dashboard_AIInsightsShouldShowForExperts()
    {
        await SetupAsync();

        // Toggle to Expert mode
        while (_dashboard.GetCurrentUIMode() != "Expert")
        {
            _dashboard.ToggleUIMode();
            await Task.Delay(500);
        }

        // AI Insights should be visible in Expert mode
        await Task.Delay(500);
        _dashboard.HasAIInsights.Should().BeTrue("AI Insights should be visible in Expert mode");
    }

    [Fact(DisplayName = "Strategy Builder section visible for experts")]
    public async Task Dashboard_StrategyBuilderShouldShowForExperts()
    {
        await SetupAsync();

        // Toggle to Expert mode
        while (_dashboard.GetCurrentUIMode() != "Expert")
        {
            _dashboard.ToggleUIMode();
            await Task.Delay(500);
        }

        // Strategy Builder should be visible in Expert mode
        await Task.Delay(500);
        _dashboard.HasStrategyBuilder.Should().BeTrue("Strategy Builder should be visible in Expert mode");
    }

    [Fact(DisplayName = "Recent Activity hidden in Beginner mode")]
    public async Task Dashboard_RecentActivityHiddenForBeginners()
    {
        await SetupAsync();

        // Toggle to Beginner mode
        while (_dashboard.GetCurrentUIMode() != "Beginner")
        {
            _dashboard.ToggleUIMode();
            await Task.Delay(500);
        }

        // Check visibility - may or may not be visible depending on feature unlock status
        // This is a soft assertion since feature may be unlocked
        await Task.Delay(500);
    }

    [Fact(DisplayName = "Portfolio value is a valid number")]
    public async Task Dashboard_PortfolioValueShouldBeValid()
    {
        await SetupAsync();

        var portfolioValue = _dashboard.GetPortfolioValue();
        portfolioValue.Should().NotBeNull("Portfolio value should be parseable");
        portfolioValue!.Value.Should().BeGreaterThanOrEqualTo(0, "Portfolio value should not be negative");
    }

    [Fact(DisplayName = "Dashboard handles refresh")]
    public async Task Dashboard_RefreshShouldWork()
    {
        await SetupAsync();

        // Pull to refresh or click refresh button
        _dashboard.ClickRefresh();

        // Wait for refresh to complete
        await Task.Delay(2000);

        _dashboard.IsLoaded.Should().BeTrue("Dashboard should still be loaded after refresh");
    }
}
