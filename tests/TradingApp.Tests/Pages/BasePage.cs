using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.UIA3;

namespace OoplesFinance.TradingApp.UITests.Pages;

/// <summary>
/// Base class for all page objects
/// </summary>
public abstract class BasePage
{
    protected Window Window { get; }
    protected UIA3Automation Automation { get; }
    protected ConditionFactory CF => Automation.ConditionFactory;

    protected BasePage(Window window, UIA3Automation automation)
    {
        Window = window;
        Automation = automation;
    }

    /// <summary>
    /// Finds an element by automation ID
    /// </summary>
    protected AutomationElement? FindByAutomationId(string automationId, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(10);
        var endTime = DateTime.Now.Add(timeout.Value);

        while (DateTime.Now < endTime)
        {
            var element = Window.FindFirstDescendant(CF.ByAutomationId(automationId));
            if (element != null) return element;
            Thread.Sleep(100);
        }
        return null;
    }

    /// <summary>
    /// Finds an element by name
    /// </summary>
    protected AutomationElement? FindByName(string name, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(10);
        var endTime = DateTime.Now.Add(timeout.Value);

        while (DateTime.Now < endTime)
        {
            var element = Window.FindFirstDescendant(CF.ByName(name));
            if (element != null) return element;
            Thread.Sleep(100);
        }
        return null;
    }

    /// <summary>
    /// Finds an element containing text
    /// </summary>
    protected AutomationElement? FindByText(string text, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(10);
        var endTime = DateTime.Now.Add(timeout.Value);

        while (DateTime.Now < endTime)
        {
            var elements = Window.FindAllDescendants();
            var element = elements.FirstOrDefault(e =>
                e.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) == true);
            if (element != null) return element;
            Thread.Sleep(100);
        }
        return null;
    }

    /// <summary>
    /// Finds a button by text
    /// </summary>
    protected Button? FindButton(string text)
    {
        var element = FindByName(text) ?? FindByText(text);
        return element?.AsButton();
    }

    /// <summary>
    /// Clicks a button with the given text
    /// </summary>
    protected void ClickButton(string text)
    {
        var button = FindButton(text);
        button?.Click();
        Thread.Sleep(300);
    }

    /// <summary>
    /// Checks if an element with the given text is visible
    /// </summary>
    public bool IsElementVisible(string text)
    {
        var element = FindByText(text, TimeSpan.FromSeconds(2));
        return element != null && element.IsOffscreen == false;
    }

    /// <summary>
    /// Waits for an element to appear
    /// </summary>
    public async Task<bool> WaitForElementAsync(string text, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(10);
        var endTime = DateTime.Now.Add(timeout.Value);

        while (DateTime.Now < endTime)
        {
            if (IsElementVisible(text)) return true;
            await Task.Delay(100);
        }
        return false;
    }
}
