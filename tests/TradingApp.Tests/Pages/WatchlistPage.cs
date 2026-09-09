using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace OoplesFinance.TradingApp.UITests.Pages;

/// <summary>
/// Page object for the Watchlist page
/// </summary>
public class WatchlistPage : BasePage
{
    public WatchlistPage(Window window, UIA3Automation automation) : base(window, automation) { }

    // Elements
    public AutomationElement? PageTitle => FindByText("Watchlist");
    public Button? AddSymbolButton => FindButton("Add") ?? FindButton("+");
    public Button? AddButton => FindButton("Add") ?? FindButton("+");
    public TextBox? SymbolInput => FindByName("Symbol")?.AsTextBox();
    public TextBox? SearchBox => FindByAutomationId("SearchBox")?.AsTextBox() ?? FindByName("Search")?.AsTextBox();
    public AutomationElement? EmptyStateMessage => FindByText("No symbols") ?? FindByText("empty") ?? FindByText("Add symbols");
    public Button? RefreshButton => FindButton("Refresh") ?? FindByAutomationId("RefreshButton") as Button;
    public Button? SortButton => FindButton("Sort") ?? FindByAutomationId("SortButton") as Button;

    // Properties
    public bool IsLoaded => PageTitle != null || FindByText("Watch") != null;
    public bool HasSearchBox => SearchBox != null || FindByAutomationId("SearchBox") != null;
    public bool HasSymbols => GetSymbolCount() > 0;
    public int SymbolCount => GetSymbolCount();
    public bool HasSortButton => SortButton != null;

    // Actions
    public void ClickAddSymbol()
    {
        AddButton?.Click();
        Thread.Sleep(300);
    }

    public void AddSymbolToWatchlist(string symbol)
    {
        ClickAddSymbol();
        Thread.Sleep(500);

        var input = SymbolInput ?? FindByName("Symbol")?.AsTextBox() ?? FindByAutomationId("SymbolEntry")?.AsTextBox();
        if (input != null)
        {
            input.Focus();
            input.Text = symbol;
            Thread.Sleep(200);
        }

        ClickButton("Add");
        Thread.Sleep(500);
    }

    public void ClickSymbol(string symbol)
    {
        var symbolElement = FindByText(symbol);
        symbolElement?.Click();
        Thread.Sleep(300);
    }

    public void RemoveSymbol(string symbol)
    {
        var symbolElement = FindByText(symbol);
        if (symbolElement != null)
        {
            ClickButton("Remove");
        }
    }

    public void SwipeToRemove(int index)
    {
        var symbol = GetSymbolByIndex(index);
        if (symbol != null)
        {
            // Swipe gesture - click the remove button that appears
            ClickButton("Remove") ;
            Thread.Sleep(300);
        }
    }

    public void EnterSearch(string text)
    {
        var searchBox = SearchBox ?? FindByAutomationId("SearchBox")?.AsTextBox();
        if (searchBox != null)
        {
            searchBox.Focus();
            searchBox.Text = text;
            Thread.Sleep(300);
        }
    }

    public void ClearSearch()
    {
        var searchBox = SearchBox ?? FindByAutomationId("SearchBox")?.AsTextBox();
        if (searchBox != null)
        {
            searchBox.Text = string.Empty;
            Thread.Sleep(300);
        }
    }

    public void ClickRefresh()
    {
        RefreshButton?.Click();
        Thread.Sleep(500);
    }

    public void ClickSort()
    {
        SortButton?.Click();
        Thread.Sleep(300);
    }

    public void SortBy(string sortOption)
    {
        ClickSort();
        Thread.Sleep(300);
        ClickButton(sortOption);
        Thread.Sleep(300);
    }

    public void LongPressSymbol(int index)
    {
        var symbol = GetSymbolByIndex(index);
        symbol?.Click(); // FlaUI doesn't have long press, simulate with click
        Thread.Sleep(500);
    }

    public void ClickContextMenuOption(string option)
    {
        ClickButton(option);
        Thread.Sleep(300);
    }

    // Get symbol data
    public AutomationElement? GetSymbolByIndex(int index)
    {
        var symbols = GetWatchlistSymbols().ToList();
        if (index < symbols.Count)
        {
            return FindByText(symbols[index]);
        }
        return null;
    }

    public string? GetSymbolPrice(int index)
    {
        // Look for price elements (dollar amounts)
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

    public string? GetSymbolChange(int index)
    {
        // Look for change values (+/- amounts)
        return FindByText("+")?.Name ?? FindByText("-")?.Name ?? "Unknown";
    }

    public string? GetSymbolChangePercent(int index)
    {
        // Look for percentage values
        var elements = Window.FindAllDescendants();
        foreach (var e in elements)
        {
            if (e.Name?.Contains("%") == true)
            {
                return e.Name;
            }
        }
        return "Unknown";
    }

    public string? GetCompanyName(int index)
    {
        // Look for company names (longer text)
        var elements = Window.FindAllDescendants();
        foreach (var e in elements)
        {
            if (e.Name?.Length > 10 && !e.Name.Contains("$") && !e.Name.Contains("%"))
            {
                return e.Name;
            }
        }
        return "Unknown";
    }

    public bool HasMiniChart(int index)
    {
        // Look for chart elements
        return FindByAutomationId("MiniChart") != null || FindByName("Chart") != null;
    }

    public bool HasSymbol(string symbol) => FindByText(symbol, TimeSpan.FromSeconds(2)) != null;

    public int GetSymbolCount()
    {
        var elements = Window.FindAllDescendants();
        var commonSymbols = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA", "NVDA", "META", "SPY", "QQQ" };
        return elements.Count(e => commonSymbols.Any(s => e.Name == s));
    }

    public IEnumerable<string> GetWatchlistSymbols()
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
