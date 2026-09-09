using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace OoplesFinance.TradingApp.UITests.Pages;

/// <summary>
/// Page object for the Positions page
/// </summary>
public class PositionsPage : BasePage
{
    public PositionsPage(Window window, UIA3Automation automation) : base(window, automation) { }

    // Elements
    public AutomationElement? PageTitle => FindByText("Positions");
    public AutomationElement? PageHeader => FindByText("Positions") ?? FindByText("My Positions");
    public AutomationElement? TotalValueLabel => FindByText("Total Value") ?? FindByText("Market Value");
    public AutomationElement? TotalPnLLabel => FindByText("P/L") ?? FindByText("Profit") ?? FindByText("Total P/L");
    public AutomationElement? EmptyStateMessage => FindByText("No positions") ?? FindByText("empty");
    public Button? AddPositionButton => FindButton("Add") ?? FindButton("Buy") ?? FindButton("+");
    public Button? RefreshButton => FindButton("Refresh") ?? FindByAutomationId("RefreshButton") as Button;
    public Button? FilterButton => FindButton("Filter") ?? FindByAutomationId("FilterButton") as Button;
    public Button? SortButton => FindButton("Sort") ?? FindByAutomationId("SortButton") as Button;

    // Properties
    public bool IsLoaded => PageTitle != null || FindByText("Position") != null;
    public bool HasPositionsList => GetPositionCount() > 0 || FindByAutomationId("PositionsList") != null;
    public bool HasPositions => GetPositionCount() > 0;
    public bool HasFilterButton => FilterButton != null;
    public bool HasSortButton => SortButton != null;

    // Actions
    public void ClickPosition(int index)
    {
        var position = GetPositionByIndex(index);
        position?.Click();
        Thread.Sleep(500);
    }

    public void ClickPosition(string symbol)
    {
        var position = FindByText(symbol);
        position?.Click();
        Thread.Sleep(500);
    }

    public void ClickClosePosition(int index)
    {
        var closeButton = GetCloseButton(index);
        closeButton?.Click();
        Thread.Sleep(300);
    }

    public void SwipeToClose(string symbol)
    {
        var position = FindByText(symbol);
        position?.Click();
    }

    public void ClickRefresh()
    {
        RefreshButton?.Click();
        Thread.Sleep(500);
    }

    public void ClickFilter()
    {
        FilterButton?.Click();
        Thread.Sleep(300);
    }

    public void ClickSort()
    {
        SortButton?.Click();
        Thread.Sleep(300);
    }

    public void ClickAddPosition()
    {
        AddPositionButton?.Click();
        Thread.Sleep(500);
    }

    // Get position data
    public AutomationElement? GetPositionByIndex(int index)
    {
        var symbols = GetPositionSymbols().ToList();
        if (index < symbols.Count)
        {
            return FindByText(symbols[index]);
        }
        return null;
    }

    public string? GetPositionQuantity(int index)
    {
        var position = GetPositionByIndex(index);
        if (position == null) return null;

        // Look for quantity near the position
        var elements = Window.FindAllDescendants();
        foreach (var e in elements)
        {
            if (e.Name?.Contains("Qty") == true || e.Name?.Contains("shares") == true)
            {
                return e.Name;
            }
        }
        return "Unknown";
    }

    public string? GetPositionMarketValue(int index)
    {
        var position = GetPositionByIndex(index);
        if (position == null) return null;

        // Look for market value (dollar amounts)
        var elements = Window.FindAllDescendants();
        foreach (var e in elements)
        {
            if (e.Name?.StartsWith("$") == true)
            {
                return e.Name;
            }
        }
        return "Unknown";
    }

    public string? GetPositionPnL(int index)
    {
        return FindByText("+")?.Name ?? FindByText("-")?.Name ?? "Unknown";
    }

    public string? GetPositionCostBasis(int index)
    {
        return FindByText("Cost")?.Name ?? FindByText("Basis")?.Name ?? "Unknown";
    }

    public string? GetPositionAvgPrice(int index)
    {
        return FindByText("Avg")?.Name ?? FindByText("Average")?.Name ?? "Unknown";
    }

    public bool HasCloseButton(int index)
    {
        return GetCloseButton(index) != null;
    }

    private Button? GetCloseButton(int index)
    {
        return FindButton("Close") ?? FindButton("Sell") ?? FindButton("X");
    }

    public bool HasConfirmationDialog()
    {
        return FindByText("Confirm") != null || FindByText("Are you sure") != null;
    }

    public bool HasPosition(string symbol) => FindByText(symbol, TimeSpan.FromSeconds(2)) != null;

    public int GetPositionCount()
    {
        var elements = Window.FindAllDescendants();
        var symbols = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA", "NVDA", "META", "JPM", "V", "MA" };
        return elements.Count(e => symbols.Any(s => e.Name?.Contains(s) == true));
    }

    public IEnumerable<string> GetPositionSymbols()
    {
        var elements = Window.FindAllDescendants();
        var symbols = new List<string>();

        foreach (var element in elements)
        {
            var name = element.Name;
            if (name != null && name.Length >= 2 && name.Length <= 5 &&
                name.All(char.IsUpper) && !name.Contains(" "))
            {
                symbols.Add(name);
            }
        }

        return symbols.Distinct();
    }
}
