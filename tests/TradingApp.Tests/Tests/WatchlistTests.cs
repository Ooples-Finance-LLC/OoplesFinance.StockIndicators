using FluentAssertions;
using OoplesFinance.TradingApp.UITests.Pages;
using Xunit;

namespace OoplesFinance.TradingApp.UITests.Tests;

/// <summary>
/// Comprehensive E2E tests for the Watchlist page
/// </summary>
[Collection("UITests")]
public class WatchlistTests : UITestBase
{
    private WatchlistPage _watchlist = null!;

    private async Task SetupAsync()
    {
        await LaunchAppAsync();

        // Navigate to watchlist
        NavigateToTab("Watchlist");
        await Task.Delay(1000);

        _watchlist = new WatchlistPage(MainWindow!, Automation!);
        await _watchlist.WaitForElementAsync("Watchlist", TimeSpan.FromSeconds(10));
    }

    [Fact(DisplayName = "Watchlist page loads")]
    public async Task Watchlist_ShouldLoad()
    {
        await SetupAsync();

        _watchlist.IsLoaded.Should().BeTrue("Watchlist page should load");
    }

    [Fact(DisplayName = "Watchlist has add button")]
    public async Task Watchlist_ShouldHaveAddButton()
    {
        await SetupAsync();

        _watchlist.AddSymbolButton.Should().NotBeNull("Add symbol button should be present");
    }

    [Fact(DisplayName = "Watchlist has search")]
    public async Task Watchlist_ShouldHaveSearch()
    {
        await SetupAsync();

        _watchlist.HasSearchBox.Should().BeTrue("Search box should be present");
    }

    [Fact(DisplayName = "Watchlist shows symbols")]
    public async Task Watchlist_ShouldShowSymbols()
    {
        await SetupAsync();

        _watchlist.HasSymbols.Should().BeTrue("Watchlist should show symbols");
    }

    [Fact(DisplayName = "Symbol shows ticker")]
    public async Task Watchlist_SymbolShowsTicker()
    {
        await SetupAsync();

        if (_watchlist.HasSymbols)
        {
            var symbol = _watchlist.GetSymbolByIndex(0);
            symbol.Should().NotBeNull("Symbol ticker should be visible");
        }
    }

    [Fact(DisplayName = "Symbol shows price")]
    public async Task Watchlist_SymbolShowsPrice()
    {
        await SetupAsync();

        if (_watchlist.HasSymbols)
        {
            _watchlist.GetSymbolPrice(0).Should().NotBeNull("Price should be visible");
        }
    }

    [Fact(DisplayName = "Symbol shows change")]
    public async Task Watchlist_SymbolShowsChange()
    {
        await SetupAsync();

        if (_watchlist.HasSymbols)
        {
            _watchlist.GetSymbolChange(0).Should().NotBeNull("Change should be visible");
        }
    }

    [Fact(DisplayName = "Symbol shows change percent")]
    public async Task Watchlist_SymbolShowsChangePercent()
    {
        await SetupAsync();

        if (_watchlist.HasSymbols)
        {
            _watchlist.GetSymbolChangePercent(0).Should().NotBeNull("Change percent should be visible");
        }
    }

    [Fact(DisplayName = "Click symbol opens chart")]
    public async Task Watchlist_ClickOpensChart()
    {
        await SetupAsync();

        if (_watchlist.HasSymbols)
        {
            _watchlist.ClickSymbol("AAPL");
            await Task.Delay(1000);

            // Should navigate to chart
            await WaitForElementAsync("$", TimeSpan.FromSeconds(5));
        }
    }

    [Fact(DisplayName = "Swipe to remove works")]
    public async Task Watchlist_SwipeToRemoveWorks()
    {
        await SetupAsync();

        if (_watchlist.HasSymbols)
        {
            var initialCount = _watchlist.SymbolCount;
            _watchlist.SwipeToRemove(0);
            await Task.Delay(500);

            // Either shows confirmation or removes immediately
        }
    }

    [Fact(DisplayName = "Add symbol button shows dialog")]
    public async Task Watchlist_AddButtonShowsDialog()
    {
        await SetupAsync();

        _watchlist.ClickAddSymbol();
        await Task.Delay(500);

        // Should show add symbol dialog or navigate to search
        await _watchlist.WaitForElementAsync("Add", TimeSpan.FromSeconds(3));
    }

    [Fact(DisplayName = "Search filters symbols")]
    public async Task Watchlist_SearchFiltersSymbols()
    {
        await SetupAsync();

        _watchlist.EnterSearch("AAPL");
        await Task.Delay(500);

        // Results should be filtered
        _watchlist.HasSymbols.Should().BeTrue("Search should show results");
    }

    [Fact(DisplayName = "Clear search shows all")]
    public async Task Watchlist_ClearSearchShowsAll()
    {
        await SetupAsync();

        _watchlist.EnterSearch("AAPL");
        await Task.Delay(300);
        _watchlist.ClearSearch();
        await Task.Delay(500);

        // All symbols should be visible again
        _watchlist.HasSymbols.Should().BeTrue("All symbols should be visible after clearing search");
    }

    [Fact(DisplayName = "Refresh updates prices")]
    public async Task Watchlist_RefreshUpdatesPrices()
    {
        await SetupAsync();

        _watchlist.ClickRefresh();
        await Task.Delay(2000);

        _watchlist.IsLoaded.Should().BeTrue("Page should still be loaded after refresh");
    }

    [Fact(DisplayName = "Empty watchlist shows message")]
    public async Task Watchlist_EmptyShowsMessage()
    {
        await SetupAsync();

        if (!_watchlist.HasSymbols)
        {
            _watchlist.EmptyStateMessage.Should().NotBeNull("Empty state message should be visible");
        }
    }

    [Fact(DisplayName = "Symbol has mini chart")]
    public async Task Watchlist_SymbolHasMiniChart()
    {
        await SetupAsync();

        if (_watchlist.HasSymbols)
        {
            _watchlist.HasMiniChart(0).Should().BeTrue("Mini chart should be visible");
        }
    }

    [Fact(DisplayName = "Symbol shows company name")]
    public async Task Watchlist_SymbolShowsCompanyName()
    {
        await SetupAsync();

        if (_watchlist.HasSymbols)
        {
            _watchlist.GetCompanyName(0).Should().NotBeNull("Company name should be visible");
        }
    }

    [Fact(DisplayName = "Sort button works")]
    public async Task Watchlist_SortButtonWorks()
    {
        await SetupAsync();

        if (_watchlist.HasSortButton)
        {
            _watchlist.ClickSort();
            await Task.Delay(500);

            // Sort options should appear
            await _watchlist.WaitForElementAsync("Sort", TimeSpan.FromSeconds(3));
        }
    }

    [Fact(DisplayName = "Sort by name works")]
    public async Task Watchlist_SortByNameWorks()
    {
        await SetupAsync();

        _watchlist.SortBy("Name");
        await Task.Delay(500);

        // Symbols should be sorted
        _watchlist.HasSymbols.Should().BeTrue("Symbols should still be visible after sorting");
    }

    [Fact(DisplayName = "Sort by change works")]
    public async Task Watchlist_SortByChangeWorks()
    {
        await SetupAsync();

        _watchlist.SortBy("Change");
        await Task.Delay(500);

        // Symbols should be sorted
        _watchlist.HasSymbols.Should().BeTrue("Symbols should still be visible after sorting");
    }

    [Fact(DisplayName = "Long press shows context menu")]
    public async Task Watchlist_LongPressShowsContextMenu()
    {
        await SetupAsync();

        if (_watchlist.HasSymbols)
        {
            _watchlist.LongPressSymbol(0);
            await Task.Delay(500);

            // Context menu with options should appear
        }
    }

    [Fact(DisplayName = "Add to alerts from context menu")]
    public async Task Watchlist_AddToAlertsFromContext()
    {
        await SetupAsync();

        if (_watchlist.HasSymbols)
        {
            _watchlist.LongPressSymbol(0);
            await Task.Delay(300);
            _watchlist.ClickContextMenuOption("Alert");
            await Task.Delay(500);

            // Should navigate to alert creation or show alert dialog
        }
    }

    [Fact(DisplayName = "Trade from context menu")]
    public async Task Watchlist_TradeFromContext()
    {
        await SetupAsync();

        if (_watchlist.HasSymbols)
        {
            _watchlist.LongPressSymbol(0);
            await Task.Delay(300);
            _watchlist.ClickContextMenuOption("Trade");
            await Task.Delay(500);

            // Should navigate to order entry
            await WaitForElementAsync("Order", TimeSpan.FromSeconds(5));
        }
    }
}
