using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace OoplesFinance.TradingApp.UITests.Pages;

/// <summary>
/// Page object for the Order Entry page
/// </summary>
public class OrderEntryPage : BasePage
{
    public OrderEntryPage(Window window, UIA3Automation automation) : base(window, automation) { }

    // Elements
    public AutomationElement? PageTitle => FindByText("Trade") ?? FindByText("Order");
    public TextBox? SymbolInput => FindByName("Symbol")?.AsTextBox() ?? FindByAutomationId("SymbolEntry")?.AsTextBox();
    public TextBox? QuantityInput => FindByName("Quantity")?.AsTextBox() ?? FindByAutomationId("QuantityEntry")?.AsTextBox();
    public TextBox? PriceInput => FindByName("Price")?.AsTextBox() ?? FindByAutomationId("PriceEntry")?.AsTextBox();
    public AutomationElement? OrderTypeSelector => FindByText("Market") ?? FindByText("Limit");
    public AutomationElement? SideSelector => FindByText("Buy") ?? FindByText("Sell");
    public AutomationElement? QuoteDisplay => FindByText("Last Price") ?? FindByText("Quote");
    public AutomationElement? OrderPreview => FindByText("Preview") ?? FindByText("Estimated");
    public Button? SubmitButton => FindButton("Submit") ?? FindButton("Place Order") ?? FindButton("Buy") ?? FindButton("Sell");

    // Actions
    public void EnterSymbol(string symbol)
    {
        var input = SymbolInput;
        if (input != null)
        {
            input.Focus();
            input.Text = symbol;
            Thread.Sleep(300);
        }
    }

    public void EnterQuantity(int quantity)
    {
        var input = QuantityInput;
        if (input != null)
        {
            input.Focus();
            input.Text = quantity.ToString();
            Thread.Sleep(200);
        }
    }

    public void EnterPrice(decimal price)
    {
        var input = PriceInput;
        if (input != null)
        {
            input.Focus();
            input.Text = price.ToString("F2");
            Thread.Sleep(200);
        }
    }

    public void SelectOrderType(string orderType)
    {
        // Click on the order type (Market, Limit, Stop, etc.)
        ClickButton(orderType);
        Thread.Sleep(200);
    }

    public void SelectBuy() => ClickButton("Buy");
    public void SelectSell() => ClickButton("Sell");

    public void SubmitOrder()
    {
        SubmitButton?.Click();
        Thread.Sleep(500);
    }

    public void PlaceMarketOrder(string symbol, int quantity, bool isBuy)
    {
        EnterSymbol(symbol);
        EnterQuantity(quantity);
        SelectOrderType("Market");
        if (isBuy) SelectBuy();
        else SelectSell();
        SubmitOrder();
    }

    public void PlaceLimitOrder(string symbol, int quantity, decimal price, bool isBuy)
    {
        EnterSymbol(symbol);
        EnterQuantity(quantity);
        EnterPrice(price);
        SelectOrderType("Limit");
        if (isBuy) SelectBuy();
        else SelectSell();
        SubmitOrder();
    }

    // Verifications
    public bool IsLoaded => PageTitle != null;
    public bool HasSymbolInput => SymbolInput != null;
    public bool HasQuantityInput => QuantityInput != null;
    public bool HasSubmitButton => SubmitButton != null;
    public bool HasQuoteDisplay => QuoteDisplay != null;

    public bool HasValidationError() => FindByText("required", TimeSpan.FromSeconds(2)) != null ||
                                         FindByText("invalid", TimeSpan.FromSeconds(2)) != null ||
                                         FindByText("error", TimeSpan.FromSeconds(2)) != null;

    public bool HasConfirmationDialog() => FindByText("Confirm", TimeSpan.FromSeconds(3)) != null ||
                                            FindByText("Review", TimeSpan.FromSeconds(3)) != null;
}
