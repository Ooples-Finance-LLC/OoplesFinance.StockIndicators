using FluentAssertions;
using OoplesFinance.TradingApp.UITests.Pages;
using Xunit;

namespace OoplesFinance.TradingApp.UITests.Tests;

/// <summary>
/// Comprehensive E2E tests for the Positions page
/// </summary>
[Collection("UITests")]
public class PositionsTests : UITestBase
{
    private PositionsPage _positions = null!;

    private async Task SetupAsync()
    {
        await LaunchAppAsync();

        // Navigate to positions
        NavigateToTab("Positions");
        await Task.Delay(1000);

        _positions = new PositionsPage(MainWindow!, Automation!);
        await _positions.WaitForElementAsync("Position", TimeSpan.FromSeconds(10));
    }

    [Fact(DisplayName = "Positions page loads")]
    public async Task Positions_ShouldLoad()
    {
        await SetupAsync();

        _positions.IsLoaded.Should().BeTrue("Positions page should load");
    }

    [Fact(DisplayName = "Positions has header section")]
    public async Task Positions_ShouldHaveHeader()
    {
        await SetupAsync();

        _positions.PageHeader.Should().NotBeNull("Page header should be visible");
    }

    [Fact(DisplayName = "Positions shows total value")]
    public async Task Positions_ShouldShowTotalValue()
    {
        await SetupAsync();

        _positions.TotalValueLabel.Should().NotBeNull("Total value should be displayed");
    }

    [Fact(DisplayName = "Positions shows P/L summary")]
    public async Task Positions_ShouldShowPnLSummary()
    {
        await SetupAsync();

        _positions.TotalPnLLabel.Should().NotBeNull("Total P/L should be displayed");
    }

    [Fact(DisplayName = "Positions list is visible")]
    public async Task Positions_ListShouldBeVisible()
    {
        await SetupAsync();

        _positions.HasPositionsList.Should().BeTrue("Positions list should be visible");
    }

    [Fact(DisplayName = "Position item shows symbol")]
    public async Task Positions_ItemShouldShowSymbol()
    {
        await SetupAsync();

        if (_positions.HasPositions)
        {
            var firstPosition = _positions.GetPositionByIndex(0);
            firstPosition.Should().NotBeNull("First position should exist");
        }
    }

    [Fact(DisplayName = "Position item shows quantity")]
    public async Task Positions_ItemShouldShowQuantity()
    {
        await SetupAsync();

        if (_positions.HasPositions)
        {
            _positions.GetPositionQuantity(0).Should().NotBeNull("Quantity should be visible");
        }
    }

    [Fact(DisplayName = "Position item shows market value")]
    public async Task Positions_ItemShouldShowMarketValue()
    {
        await SetupAsync();

        if (_positions.HasPositions)
        {
            _positions.GetPositionMarketValue(0).Should().NotBeNull("Market value should be visible");
        }
    }

    [Fact(DisplayName = "Position item shows P/L")]
    public async Task Positions_ItemShouldShowPnL()
    {
        await SetupAsync();

        if (_positions.HasPositions)
        {
            _positions.GetPositionPnL(0).Should().NotBeNull("P/L should be visible");
        }
    }

    [Fact(DisplayName = "Position can be tapped for details")]
    public async Task Positions_CanTapForDetails()
    {
        await SetupAsync();

        if (_positions.HasPositions)
        {
            _positions.ClickPosition(0);
            await Task.Delay(1000);

            // Should navigate to position detail or show popup
            await WaitForElementAsync("Detail", TimeSpan.FromSeconds(5));
        }
    }

    [Fact(DisplayName = "Position has close button")]
    public async Task Positions_HasCloseButton()
    {
        await SetupAsync();

        if (_positions.HasPositions)
        {
            _positions.HasCloseButton(0).Should().BeTrue("Close button should be present");
        }
    }

    [Fact(DisplayName = "Close button shows confirmation")]
    public async Task Positions_CloseShowsConfirmation()
    {
        await SetupAsync();

        if (_positions.HasPositions)
        {
            _positions.ClickClosePosition(0);
            await Task.Delay(500);

            _positions.HasConfirmationDialog().Should().BeTrue("Confirmation dialog should appear");
        }
    }

    [Fact(DisplayName = "Positions can be refreshed")]
    public async Task Positions_CanBeRefreshed()
    {
        await SetupAsync();

        _positions.ClickRefresh();
        await Task.Delay(2000);

        _positions.IsLoaded.Should().BeTrue("Page should still be loaded after refresh");
    }

    [Fact(DisplayName = "Empty state shown when no positions")]
    public async Task Positions_ShowsEmptyState()
    {
        await SetupAsync();

        if (!_positions.HasPositions)
        {
            _positions.EmptyStateMessage.Should().NotBeNull("Empty state message should be visible");
        }
    }

    [Fact(DisplayName = "Positions shows cost basis")]
    public async Task Positions_ShowsCostBasis()
    {
        await SetupAsync();

        if (_positions.HasPositions)
        {
            _positions.GetPositionCostBasis(0).Should().NotBeNull("Cost basis should be visible");
        }
    }

    [Fact(DisplayName = "Positions shows average price")]
    public async Task Positions_ShowsAveragePrice()
    {
        await SetupAsync();

        if (_positions.HasPositions)
        {
            _positions.GetPositionAvgPrice(0).Should().NotBeNull("Average price should be visible");
        }
    }

    [Fact(DisplayName = "Positions filter works")]
    public async Task Positions_FilterWorks()
    {
        await SetupAsync();

        if (_positions.HasFilterButton)
        {
            _positions.ClickFilter();
            await Task.Delay(500);

            // Filter options should appear
            await _positions.WaitForElementAsync("Filter", TimeSpan.FromSeconds(3));
        }
    }

    [Fact(DisplayName = "Positions sort works")]
    public async Task Positions_SortWorks()
    {
        await SetupAsync();

        if (_positions.HasSortButton)
        {
            _positions.ClickSort();
            await Task.Delay(500);

            // Sort options should appear
            await _positions.WaitForElementAsync("Sort", TimeSpan.FromSeconds(3));
        }
    }

    [Fact(DisplayName = "Add position button exists")]
    public async Task Positions_AddButtonExists()
    {
        await SetupAsync();

        _positions.AddPositionButton.Should().NotBeNull("Add position button should exist");
    }

    [Fact(DisplayName = "Add position navigates to trade")]
    public async Task Positions_AddNavigatesToTrade()
    {
        await SetupAsync();

        _positions.ClickAddPosition();
        await Task.Delay(1000);

        // Should navigate to order entry
        await WaitForElementAsync("Order", TimeSpan.FromSeconds(5));
    }
}
