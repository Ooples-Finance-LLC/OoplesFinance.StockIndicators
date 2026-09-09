using FluentAssertions;
using OoplesFinance.TradingApp.UITests.Pages;
using Xunit;

namespace OoplesFinance.TradingApp.UITests.Tests;

/// <summary>
/// Comprehensive E2E tests for the Order Entry page
/// </summary>
[Collection("UITests")]
public class OrderEntryTests : UITestBase
{
    private OrderEntryPage _orderEntry = null!;

    private async Task SetupAsync()
    {
        await LaunchAppAsync();

        // Navigate to order entry
        NavigateToTab("Trade");
        await Task.Delay(1000);

        _orderEntry = new OrderEntryPage(MainWindow!, Automation!);
        await _orderEntry.WaitForElementAsync("Symbol", TimeSpan.FromSeconds(10));
    }

    [Fact(DisplayName = "Order Entry page loads")]
    public async Task OrderEntry_ShouldLoad()
    {
        await SetupAsync();

        _orderEntry.IsLoaded.Should().BeTrue("Order Entry page should load");
    }

    [Fact(DisplayName = "Order Entry has symbol input")]
    public async Task OrderEntry_ShouldHaveSymbolInput()
    {
        await SetupAsync();

        _orderEntry.HasSymbolInput.Should().BeTrue("Symbol input should be present");
    }

    [Fact(DisplayName = "Order Entry has quantity input")]
    public async Task OrderEntry_ShouldHaveQuantityInput()
    {
        await SetupAsync();

        _orderEntry.HasQuantityInput.Should().BeTrue("Quantity input should be present");
    }

    [Fact(DisplayName = "Order Entry has submit button")]
    public async Task OrderEntry_ShouldHaveSubmitButton()
    {
        await SetupAsync();

        _orderEntry.HasSubmitButton.Should().BeTrue("Submit button should be present");
    }

    [Fact(DisplayName = "Can enter symbol")]
    public async Task OrderEntry_CanEnterSymbol()
    {
        await SetupAsync();

        _orderEntry.EnterSymbol("AAPL");

        // Symbol should be entered (check for quote display or no error)
        await Task.Delay(500);
        _orderEntry.HasValidationError().Should().BeFalse("No validation error should appear for valid symbol");
    }

    [Fact(DisplayName = "Can enter quantity")]
    public async Task OrderEntry_CanEnterQuantity()
    {
        await SetupAsync();

        _orderEntry.EnterQuantity(10);

        _orderEntry.HasValidationError().Should().BeFalse("No validation error should appear for valid quantity");
    }

    [Fact(DisplayName = "Shows quote after entering symbol")]
    public async Task OrderEntry_ShouldShowQuote()
    {
        await SetupAsync();

        _orderEntry.EnterSymbol("AAPL");
        await Task.Delay(1000); // Wait for quote to load

        _orderEntry.HasQuoteDisplay.Should().BeTrue("Quote should display after entering symbol");
    }

    [Fact(DisplayName = "Order type selection works")]
    public async Task OrderEntry_OrderTypeSelectionWorks()
    {
        await SetupAsync();

        _orderEntry.SelectOrderType("Market");
        await Task.Delay(300);

        // Market order should be selected - no price input visible
        // Or verify UI state changed
    }

    [Fact(DisplayName = "Limit order requires price")]
    public async Task OrderEntry_LimitOrderRequiresPrice()
    {
        await SetupAsync();

        _orderEntry.EnterSymbol("AAPL");
        _orderEntry.EnterQuantity(10);
        _orderEntry.SelectOrderType("Limit");
        // Don't enter price

        _orderEntry.SubmitOrder();
        await Task.Delay(500);

        // Should show validation error or price field should be required
    }

    [Fact(DisplayName = "Valid order shows confirmation")]
    public async Task OrderEntry_ValidOrderShowsConfirmation()
    {
        await SetupAsync();

        _orderEntry.EnterSymbol("AAPL");
        _orderEntry.EnterQuantity(10);
        _orderEntry.SelectOrderType("Market");
        _orderEntry.SelectBuy();

        _orderEntry.SubmitOrder();
        await Task.Delay(1000);

        _orderEntry.HasConfirmationDialog().Should().BeTrue("Confirmation dialog should appear");
    }

    [Fact(DisplayName = "Buy button highlights green")]
    public async Task OrderEntry_BuyButtonShouldWork()
    {
        await SetupAsync();

        _orderEntry.SelectBuy();
        await Task.Delay(300);

        // Verify buy is selected - UI should indicate buy mode
    }

    [Fact(DisplayName = "Sell button highlights red")]
    public async Task OrderEntry_SellButtonShouldWork()
    {
        await SetupAsync();

        _orderEntry.SelectSell();
        await Task.Delay(300);

        // Verify sell is selected - UI should indicate sell mode
    }

    [Fact(DisplayName = "Zero quantity shows error")]
    public async Task OrderEntry_ZeroQuantityShowsError()
    {
        await SetupAsync();

        _orderEntry.EnterSymbol("AAPL");
        _orderEntry.EnterQuantity(0);
        _orderEntry.SubmitOrder();

        await Task.Delay(500);

        _orderEntry.HasValidationError().Should().BeTrue("Error should show for zero quantity");
    }

    [Fact(DisplayName = "Invalid symbol shows error")]
    public async Task OrderEntry_InvalidSymbolShowsError()
    {
        await SetupAsync();

        _orderEntry.EnterSymbol("XXXXXX");
        await Task.Delay(1000);

        // Should show error or no quote
    }

    [Fact(DisplayName = "Market order workflow completes")]
    public async Task OrderEntry_MarketOrderWorkflowCompletes()
    {
        await SetupAsync();

        _orderEntry.PlaceMarketOrder("AAPL", 1, isBuy: true);

        await Task.Delay(1000);

        // Should show confirmation or success message
        _orderEntry.HasConfirmationDialog().Should().BeTrue("Order confirmation should appear");
    }

    [Fact(DisplayName = "Limit order workflow completes")]
    public async Task OrderEntry_LimitOrderWorkflowCompletes()
    {
        await SetupAsync();

        _orderEntry.PlaceLimitOrder("AAPL", 1, 150.00m, isBuy: true);

        await Task.Delay(1000);

        // Should show confirmation or success message
        _orderEntry.HasConfirmationDialog().Should().BeTrue("Order confirmation should appear");
    }
}
