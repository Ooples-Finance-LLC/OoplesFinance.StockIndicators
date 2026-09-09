using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace OoplesFinance.TradingApp.UITests.Pages;

/// <summary>
/// Page object for the Chart page
/// </summary>
public class ChartPage : BasePage
{
    public ChartPage(Window window, UIA3Automation automation) : base(window, automation) { }

    // Elements
    public AutomationElement? SymbolHeader => FindByText("AAPL") ?? FindByText("MSFT") ?? FindByText("Stock");
    public AutomationElement? LastPriceDisplay => FindByText("$");
    public AutomationElement? ChangeDisplay => FindByText("%") ?? FindByText("+") ?? FindByText("-");
    public AutomationElement? AISignalPanel => FindByText("AI Signal");
    public AutomationElement? ChartArea => FindByAutomationId("ChartArea") ?? FindByText("Chart");

    // Timeframe buttons
    public Button? Day1Button => FindButton("1D");
    public Button? Week1Button => FindButton("1W");
    public Button? Month1Button => FindButton("1M");
    public Button? Month3Button => FindButton("3M");
    public Button? Year1Button => FindButton("1Y");
    public Button? AllButton => FindButton("ALL");

    // Indicator buttons
    public Button? SmaButton => FindButton("SMA");
    public Button? EmaButton => FindButton("EMA");
    public Button? BollingerButton => FindButton("BB");
    public Button? RsiButton => FindButton("RSI");
    public Button? MacdButton => FindButton("MACD");

    // Quote details
    public AutomationElement? OpenLabel => FindByText("Open");
    public AutomationElement? HighLabel => FindByText("High");
    public AutomationElement? LowLabel => FindByText("Low");
    public AutomationElement? VolumeLabel => FindByText("Volume");
    public AutomationElement? BidLabel => FindByText("Bid");
    public AutomationElement? AskLabel => FindByText("Ask");

    // Action buttons
    public Button? BuyButton => FindButton("Buy");
    public Button? SellButton => FindButton("Sell");
    public Button? RefreshAIButton => FindButton("Refresh");

    // Actions
    public void SelectTimeframe(string timeframe)
    {
        ClickButton(timeframe);
        Thread.Sleep(500);
    }

    public void ToggleIndicator(string indicator)
    {
        ClickButton(indicator);
        Thread.Sleep(300);
    }

    public void ClickBuy() => BuyButton?.Click();
    public void ClickSell() => SellButton?.Click();
    public void RefreshAISignal() => RefreshAIButton?.Click();

    // Verifications
    public bool IsLoaded => LastPriceDisplay != null || SymbolHeader != null;
    public bool HasAISignal => AISignalPanel != null;
    public bool HasChart => ChartArea != null;
    public bool HasQuoteDetails => OpenLabel != null && HighLabel != null && LowLabel != null;
    public bool HasIndicatorButtons => SmaButton != null || EmaButton != null || RsiButton != null;
    public bool HasTimeframeButtons => Day1Button != null || Week1Button != null || Month1Button != null;
    public bool HasActionButtons => BuyButton != null && SellButton != null;

    public string? GetCurrentSymbol()
    {
        // Find the symbol in the header area
        var elements = Window.FindAllDescendants();
        var symbolElement = elements.FirstOrDefault(e =>
            e.Name?.Length >= 2 && e.Name?.Length <= 5 &&
            e.Name?.All(char.IsUpper) == true);
        return symbolElement?.Name;
    }

    public string? GetAISignalText()
    {
        // Find AI signal indicator (BUY, SELL, HOLD)
        if (FindByText("BUY", TimeSpan.FromSeconds(1)) != null) return "BUY";
        if (FindByText("SELL", TimeSpan.FromSeconds(1)) != null) return "SELL";
        if (FindByText("HOLD", TimeSpan.FromSeconds(1)) != null) return "HOLD";
        return null;
    }
}
