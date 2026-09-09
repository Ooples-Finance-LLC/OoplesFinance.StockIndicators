using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace OoplesFinance.TradingApp.UITests.Pages;

/// <summary>
/// Page object for the Alerts page
/// </summary>
public class AlertsPage : BasePage
{
    public AlertsPage(Window window, UIA3Automation automation) : base(window, automation) { }

    // Elements
    public AutomationElement? PageTitle => FindByText("Alerts");
    public Button? AddAlertButton => FindButton("Add Alert") ?? FindButton("Create") ?? FindButton("+");
    public AutomationElement? ActiveAlertsSection => FindByText("Active");
    public AutomationElement? TriggeredAlertsSection => FindByText("Triggered");
    public AutomationElement? EmptyStateMessage => FindByText("No alerts") ?? FindByText("empty") ?? FindByText("Create");
    public Button? RefreshButton => FindButton("Refresh") ?? FindByAutomationId("RefreshButton") as Button;
    public Button? FilterButton => FindButton("Filter") ?? FindByAutomationId("FilterButton") as Button;

    // Alert form elements
    public TextBox? SymbolInput => FindByName("Symbol")?.AsTextBox() ?? FindByAutomationId("SymbolInput")?.AsTextBox();
    public TextBox? PriceInput => FindByName("Price")?.AsTextBox() ?? FindByAutomationId("PriceInput")?.AsTextBox();
    public AutomationElement? ConditionPicker => FindByText("Above") ?? FindByText("Below") ?? FindByAutomationId("ConditionPicker");
    public CheckBox? PushNotificationToggle => FindByName("Push")?.AsCheckBox() ?? FindByAutomationId("PushToggle")?.AsCheckBox();
    public CheckBox? EmailNotificationToggle => FindByName("Email")?.AsCheckBox() ?? FindByAutomationId("EmailToggle")?.AsCheckBox();

    // Properties
    public bool IsLoaded => PageTitle != null || FindByText("Alert") != null;
    public bool HasAddButton => AddAlertButton != null;
    public bool HasActiveAlerts => ActiveAlertsSection != null;
    public bool HasAlerts => GetAlertCount() > 0;
    public int AlertCount => GetAlertCount();
    public bool HasFilterButton => FilterButton != null;
    public bool HasAlertForm => SymbolInput != null || FindByText("Create Alert") != null;
    public bool HasNotificationOptions => PushNotificationToggle != null || FindByText("Notification") != null;

    // Actions
    public void ClickAddAlert()
    {
        AddAlertButton?.Click();
        Thread.Sleep(300);
    }

    public void CreatePriceAlert(string symbol, decimal price, string condition)
    {
        ClickAddAlert();
        Thread.Sleep(500);

        var symbolInput = SymbolInput ?? FindByName("Symbol")?.AsTextBox();
        if (symbolInput != null)
        {
            symbolInput.Focus();
            symbolInput.Text = symbol;
            Thread.Sleep(200);
        }

        var priceInput = PriceInput ?? FindByName("Price")?.AsTextBox();
        if (priceInput != null)
        {
            priceInput.Focus();
            priceInput.Text = price.ToString("F2");
            Thread.Sleep(200);
        }

        // Select condition (Above/Below)
        ClickButton(condition);
        Thread.Sleep(200);

        // Save the alert
        ClickButton("Save") ;
        Thread.Sleep(500);
    }

    public void CreatePriceAlert(string symbol, decimal price, bool isAbove)
    {
        CreatePriceAlert(symbol, price, isAbove ? "Above" : "Below");
    }

    public void DeleteAlert(int index)
    {
        ClickDeleteAlert(index);
        Thread.Sleep(300);
        // Confirm deletion
        ClickButton("Confirm") ;
        Thread.Sleep(300);
    }

    public void ClickDeleteAlert(int index)
    {
        // Find delete button for the alert at index
        var deleteButton = FindButton("Delete") ?? FindButton("Remove");
        deleteButton?.Click();
    }

    public void ClickEditAlert(int index)
    {
        var editButton = FindButton("Edit") ?? FindButton("Modify");
        editButton?.Click();
        Thread.Sleep(300);
    }

    public void ToggleAlert(int index)
    {
        // Find toggle for the alert at index
        var toggle = FindByAutomationId($"AlertToggle_{index}");
        toggle?.Click();
    }

    public void ToggleAlert(string symbol)
    {
        var alertElement = FindByText(symbol);
        alertElement?.Click();
    }

    public void ClickRefresh()
    {
        RefreshButton?.Click();
        Thread.Sleep(500);
    }

    public void FilterBy(string filter)
    {
        FilterButton?.Click();
        Thread.Sleep(300);
        ClickButton(filter);
        Thread.Sleep(300);
    }

    public void TogglePushNotification(bool enabled)
    {
        var toggle = PushNotificationToggle;
        if (toggle != null && toggle.IsChecked != enabled)
        {
            toggle.Click();
        }
    }

    public void ToggleEmailNotification(bool enabled)
    {
        var toggle = EmailNotificationToggle;
        if (toggle != null && toggle.IsChecked != enabled)
        {
            toggle.Click();
        }
    }

    public void SelectAlertType(string alertType)
    {
        ClickButton(alertType);
        Thread.Sleep(300);
    }

    // Get alert data
    public string? GetAlertSymbol(int index)
    {
        var symbols = GetAlertSymbols().ToList();
        return index < symbols.Count ? symbols[index] : null;
    }

    public string? GetAlertCondition(int index)
    {
        return FindByText("Above")?.Name ?? FindByText("Below")?.Name ?? "Unknown";
    }

    public string? GetAlertTargetPrice(int index)
    {
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

    public string? GetCurrentPrice(int index)
    {
        return FindByText("Current")?.Name ?? "Unknown";
    }

    public string? GetDistanceToTrigger(int index)
    {
        return FindByText("Distance")?.Name ?? FindByText("away")?.Name ?? "Unknown";
    }

    public bool HasConfirmationDialog()
    {
        return FindByText("Confirm") != null || FindByText("Are you sure") != null;
    }

    public bool HasAlert(string symbol) => FindByText(symbol, TimeSpan.FromSeconds(2)) != null;

    public int GetAlertCount()
    {
        var elements = Window.FindAllDescendants();
        return elements.Count(e => e.Name?.Contains("$") == true && e.Name?.Contains(".") == true);
    }

    private IEnumerable<string> GetAlertSymbols()
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
