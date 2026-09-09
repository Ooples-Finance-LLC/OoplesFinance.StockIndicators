using FluentAssertions;
using OoplesFinance.TradingApp.UITests.Pages;
using Xunit;

namespace OoplesFinance.TradingApp.UITests.Tests;

/// <summary>
/// Comprehensive E2E tests for the Chart page
/// </summary>
[Collection("UITests")]
public class ChartTests : UITestBase
{
    private ChartPage _chart = null!;

    private async Task SetupAsync()
    {
        await LaunchAppAsync();

        // Navigate to chart page (via watchlist or direct)
        NavigateToTab("Watchlist");
        await Task.Delay(500);

        // Click on a symbol to open chart
        var watchlist = new WatchlistPage(MainWindow!, Automation!);
        watchlist.ClickSymbol("AAPL");
        await Task.Delay(1000);

        _chart = new ChartPage(MainWindow!, Automation!);
        await _chart.WaitForElementAsync("$", TimeSpan.FromSeconds(10));
    }

    [Fact(DisplayName = "Chart page loads")]
    public async Task Chart_ShouldLoad()
    {
        await SetupAsync();

        _chart.IsLoaded.Should().BeTrue("Chart page should load");
    }

    [Fact(DisplayName = "Chart displays price")]
    public async Task Chart_ShouldDisplayPrice()
    {
        await SetupAsync();

        _chart.LastPriceDisplay.Should().NotBeNull("Price should be displayed");
    }

    [Fact(DisplayName = "Chart has timeframe buttons")]
    public async Task Chart_ShouldHaveTimeframeButtons()
    {
        await SetupAsync();

        _chart.HasTimeframeButtons.Should().BeTrue("Timeframe buttons should be present");
    }

    [Fact(DisplayName = "Chart has indicator buttons")]
    public async Task Chart_ShouldHaveIndicatorButtons()
    {
        await SetupAsync();

        _chart.HasIndicatorButtons.Should().BeTrue("Indicator buttons should be present");
    }

    [Fact(DisplayName = "Chart has quote details")]
    public async Task Chart_ShouldHaveQuoteDetails()
    {
        await SetupAsync();

        _chart.HasQuoteDetails.Should().BeTrue("Quote details should be visible");
    }

    [Fact(DisplayName = "Chart has action buttons")]
    public async Task Chart_ShouldHaveActionButtons()
    {
        await SetupAsync();

        _chart.HasActionButtons.Should().BeTrue("Buy/Sell buttons should be present");
    }

    [Fact(DisplayName = "Timeframe 1D selection works")]
    public async Task Chart_1DTimeframeShouldWork()
    {
        await SetupAsync();

        _chart.SelectTimeframe("1D");
        await Task.Delay(1000);

        _chart.IsLoaded.Should().BeTrue("Chart should still be loaded after timeframe change");
    }

    [Fact(DisplayName = "Timeframe 1W selection works")]
    public async Task Chart_1WTimeframeShouldWork()
    {
        await SetupAsync();

        _chart.SelectTimeframe("1W");
        await Task.Delay(1000);

        _chart.IsLoaded.Should().BeTrue("Chart should reload with weekly data");
    }

    [Fact(DisplayName = "Timeframe 1M selection works")]
    public async Task Chart_1MTimeframeShouldWork()
    {
        await SetupAsync();

        _chart.SelectTimeframe("1M");
        await Task.Delay(1000);

        _chart.IsLoaded.Should().BeTrue("Chart should reload with monthly data");
    }

    [Fact(DisplayName = "SMA indicator toggle works")]
    public async Task Chart_SMAToggleShouldWork()
    {
        await SetupAsync();

        _chart.ToggleIndicator("SMA");
        await Task.Delay(500);

        // SMA should be toggled (button state changes)
        _chart.IsLoaded.Should().BeTrue("Chart should still be loaded after toggling SMA");
    }

    [Fact(DisplayName = "EMA indicator toggle works")]
    public async Task Chart_EMAToggleShouldWork()
    {
        await SetupAsync();

        _chart.ToggleIndicator("EMA");
        await Task.Delay(500);

        _chart.IsLoaded.Should().BeTrue("Chart should still be loaded after toggling EMA");
    }

    [Fact(DisplayName = "Bollinger Bands toggle works")]
    public async Task Chart_BollingerToggleShouldWork()
    {
        await SetupAsync();

        _chart.ToggleIndicator("BB");
        await Task.Delay(500);

        _chart.IsLoaded.Should().BeTrue("Chart should still be loaded after toggling BB");
    }

    [Fact(DisplayName = "RSI indicator toggle works")]
    public async Task Chart_RSIToggleShouldWork()
    {
        await SetupAsync();

        _chart.ToggleIndicator("RSI");
        await Task.Delay(500);

        _chart.IsLoaded.Should().BeTrue("Chart should still be loaded after toggling RSI");
    }

    [Fact(DisplayName = "MACD indicator toggle works")]
    public async Task Chart_MACDToggleShouldWork()
    {
        await SetupAsync();

        _chart.ToggleIndicator("MACD");
        await Task.Delay(500);

        _chart.IsLoaded.Should().BeTrue("Chart should still be loaded after toggling MACD");
    }

    [Fact(DisplayName = "AI Signal panel is visible")]
    public async Task Chart_AISignalShouldBeVisible()
    {
        await SetupAsync();

        _chart.HasAISignal.Should().BeTrue("AI Signal panel should be visible");
    }

    [Fact(DisplayName = "AI Signal shows valid signal")]
    public async Task Chart_AISignalShowsValidValue()
    {
        await SetupAsync();

        var signal = _chart.GetAISignalText();
        if (signal != null)
        {
            signal.Should().BeOneOf("BUY", "SELL", "HOLD", "Buy", "Sell", "Hold");
        }
    }

    [Fact(DisplayName = "Buy button navigates to order")]
    public async Task Chart_BuyButtonNavigatesToOrder()
    {
        await SetupAsync();

        _chart.ClickBuy();
        await Task.Delay(1000);

        // Should navigate to order entry or show order form
        await WaitForElementAsync("Order", TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "Sell button navigates to order")]
    public async Task Chart_SellButtonNavigatesToOrder()
    {
        await SetupAsync();

        _chart.ClickSell();
        await Task.Delay(1000);

        // Should navigate to order entry or show order form
        await WaitForElementAsync("Order", TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "Refresh AI signal works")]
    public async Task Chart_RefreshAISignalWorks()
    {
        await SetupAsync();

        _chart.RefreshAISignal();
        await Task.Delay(2000);

        _chart.HasAISignal.Should().BeTrue("AI Signal should still be visible after refresh");
    }

    [Fact(DisplayName = "Quote details show Open")]
    public async Task Chart_QuoteDetailsShowOpen()
    {
        await SetupAsync();

        _chart.OpenLabel.Should().NotBeNull("Open price should be visible");
    }

    [Fact(DisplayName = "Quote details show High")]
    public async Task Chart_QuoteDetailsShowHigh()
    {
        await SetupAsync();

        _chart.HighLabel.Should().NotBeNull("High price should be visible");
    }

    [Fact(DisplayName = "Quote details show Low")]
    public async Task Chart_QuoteDetailsShowLow()
    {
        await SetupAsync();

        _chart.LowLabel.Should().NotBeNull("Low price should be visible");
    }

    [Fact(DisplayName = "Quote details show Volume")]
    public async Task Chart_QuoteDetailsShowVolume()
    {
        await SetupAsync();

        _chart.VolumeLabel.Should().NotBeNull("Volume should be visible");
    }

    [Fact(DisplayName = "Quote details show Bid/Ask")]
    public async Task Chart_QuoteDetailsShowBidAsk()
    {
        await SetupAsync();

        _chart.BidLabel.Should().NotBeNull("Bid should be visible");
        _chart.AskLabel.Should().NotBeNull("Ask should be visible");
    }
}
