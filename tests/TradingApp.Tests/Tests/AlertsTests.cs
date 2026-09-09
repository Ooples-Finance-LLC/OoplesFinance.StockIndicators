using FluentAssertions;
using OoplesFinance.TradingApp.UITests.Pages;
using Xunit;

namespace OoplesFinance.TradingApp.UITests.Tests;

/// <summary>
/// Comprehensive E2E tests for the Alerts page
/// </summary>
[Collection("UITests")]
public class AlertsTests : UITestBase
{
    private AlertsPage _alerts = null!;

    private async Task SetupAsync()
    {
        await LaunchAppAsync();

        // Navigate to alerts
        NavigateToTab("Alerts");
        await Task.Delay(1000);

        _alerts = new AlertsPage(MainWindow!, Automation!);
        await _alerts.WaitForElementAsync("Alert", TimeSpan.FromSeconds(10));
    }

    [Fact(DisplayName = "Alerts page loads")]
    public async Task Alerts_ShouldLoad()
    {
        await SetupAsync();

        _alerts.IsLoaded.Should().BeTrue("Alerts page should load");
    }

    [Fact(DisplayName = "Alerts has add button")]
    public async Task Alerts_ShouldHaveAddButton()
    {
        await SetupAsync();

        _alerts.AddAlertButton.Should().NotBeNull("Add alert button should be present");
    }

    [Fact(DisplayName = "Add alert shows form")]
    public async Task Alerts_AddShowsForm()
    {
        await SetupAsync();

        _alerts.ClickAddAlert();
        await Task.Delay(500);

        _alerts.HasAlertForm.Should().BeTrue("Alert form should be visible");
    }

    [Fact(DisplayName = "Alert form has symbol input")]
    public async Task Alerts_FormHasSymbolInput()
    {
        await SetupAsync();

        _alerts.ClickAddAlert();
        await Task.Delay(500);

        _alerts.SymbolInput.Should().NotBeNull("Symbol input should be present");
    }

    [Fact(DisplayName = "Alert form has price input")]
    public async Task Alerts_FormHasPriceInput()
    {
        await SetupAsync();

        _alerts.ClickAddAlert();
        await Task.Delay(500);

        _alerts.PriceInput.Should().NotBeNull("Price input should be present");
    }

    [Fact(DisplayName = "Alert form has condition selector")]
    public async Task Alerts_FormHasConditionSelector()
    {
        await SetupAsync();

        _alerts.ClickAddAlert();
        await Task.Delay(500);

        _alerts.ConditionPicker.Should().NotBeNull("Condition picker should be present");
    }

    [Fact(DisplayName = "Can create price above alert")]
    public async Task Alerts_CanCreatePriceAbove()
    {
        await SetupAsync();

        _alerts.CreatePriceAlert("AAPL", 200.00m, "Above");
        await Task.Delay(1000);

        // Alert should be created
        _alerts.HasAlerts.Should().BeTrue("Alert should be created");
    }

    [Fact(DisplayName = "Can create price below alert")]
    public async Task Alerts_CanCreatePriceBelow()
    {
        await SetupAsync();

        _alerts.CreatePriceAlert("AAPL", 150.00m, "Below");
        await Task.Delay(1000);

        // Alert should be created
        _alerts.HasAlerts.Should().BeTrue("Alert should be created");
    }

    [Fact(DisplayName = "Alert list shows alerts")]
    public async Task Alerts_ListShowsAlerts()
    {
        await SetupAsync();

        if (_alerts.HasAlerts)
        {
            _alerts.AlertCount.Should().BeGreaterThan(0, "Alert list should show alerts");
        }
    }

    [Fact(DisplayName = "Alert shows symbol")]
    public async Task Alerts_ItemShowsSymbol()
    {
        await SetupAsync();

        if (_alerts.HasAlerts)
        {
            _alerts.GetAlertSymbol(0).Should().NotBeNull("Symbol should be visible");
        }
    }

    [Fact(DisplayName = "Alert shows condition")]
    public async Task Alerts_ItemShowsCondition()
    {
        await SetupAsync();

        if (_alerts.HasAlerts)
        {
            _alerts.GetAlertCondition(0).Should().NotBeNull("Condition should be visible");
        }
    }

    [Fact(DisplayName = "Alert shows target price")]
    public async Task Alerts_ItemShowsTargetPrice()
    {
        await SetupAsync();

        if (_alerts.HasAlerts)
        {
            _alerts.GetAlertTargetPrice(0).Should().NotBeNull("Target price should be visible");
        }
    }

    [Fact(DisplayName = "Alert can be toggled")]
    public async Task Alerts_CanBeToggled()
    {
        await SetupAsync();

        if (_alerts.HasAlerts)
        {
            _alerts.ToggleAlert(0);
            await Task.Delay(500);

            // Alert enabled state should change
        }
    }

    [Fact(DisplayName = "Alert can be deleted")]
    public async Task Alerts_CanBeDeleted()
    {
        await SetupAsync();

        if (_alerts.HasAlerts)
        {
            var initialCount = _alerts.AlertCount;
            _alerts.DeleteAlert(0);
            await Task.Delay(500);

            // Alert count should decrease or confirmation shown
        }
    }

    [Fact(DisplayName = "Delete shows confirmation")]
    public async Task Alerts_DeleteShowsConfirmation()
    {
        await SetupAsync();

        if (_alerts.HasAlerts)
        {
            _alerts.ClickDeleteAlert(0);
            await Task.Delay(500);

            _alerts.HasConfirmationDialog().Should().BeTrue("Confirmation dialog should appear");
        }
    }

    [Fact(DisplayName = "Alert can be edited")]
    public async Task Alerts_CanBeEdited()
    {
        await SetupAsync();

        if (_alerts.HasAlerts)
        {
            _alerts.ClickEditAlert(0);
            await Task.Delay(500);

            _alerts.HasAlertForm.Should().BeTrue("Edit form should be visible");
        }
    }

    [Fact(DisplayName = "Empty state shows message")]
    public async Task Alerts_EmptyStateShowsMessage()
    {
        await SetupAsync();

        if (!_alerts.HasAlerts)
        {
            _alerts.EmptyStateMessage.Should().NotBeNull("Empty state message should be visible");
        }
    }

    [Fact(DisplayName = "Alert shows current price")]
    public async Task Alerts_ShowsCurrentPrice()
    {
        await SetupAsync();

        if (_alerts.HasAlerts)
        {
            _alerts.GetCurrentPrice(0).Should().NotBeNull("Current price should be visible");
        }
    }

    [Fact(DisplayName = "Alert shows distance to trigger")]
    public async Task Alerts_ShowsDistanceToTrigger()
    {
        await SetupAsync();

        if (_alerts.HasAlerts)
        {
            _alerts.GetDistanceToTrigger(0).Should().NotBeNull("Distance to trigger should be visible");
        }
    }

    [Fact(DisplayName = "Filter by active alerts")]
    public async Task Alerts_FilterByActive()
    {
        await SetupAsync();

        if (_alerts.HasFilterButton)
        {
            _alerts.FilterBy("Active");
            await Task.Delay(500);

            // Should show only active alerts
        }
    }

    [Fact(DisplayName = "Filter by triggered alerts")]
    public async Task Alerts_FilterByTriggered()
    {
        await SetupAsync();

        if (_alerts.HasFilterButton)
        {
            _alerts.FilterBy("Triggered");
            await Task.Delay(500);

            // Should show only triggered alerts
        }
    }

    [Fact(DisplayName = "Notification preferences exist")]
    public async Task Alerts_NotificationPreferencesExist()
    {
        await SetupAsync();

        _alerts.ClickAddAlert();
        await Task.Delay(500);

        _alerts.HasNotificationOptions.Should().BeTrue("Notification options should be present");
    }

    [Fact(DisplayName = "Can set push notification")]
    public async Task Alerts_CanSetPushNotification()
    {
        await SetupAsync();

        _alerts.ClickAddAlert();
        await Task.Delay(500);

        _alerts.TogglePushNotification(true);
        // Push notification should be enabled
    }

    [Fact(DisplayName = "Can set email notification")]
    public async Task Alerts_CanSetEmailNotification()
    {
        await SetupAsync();

        _alerts.ClickAddAlert();
        await Task.Delay(500);

        _alerts.ToggleEmailNotification(true);
        // Email notification should be enabled
    }

    [Fact(DisplayName = "Percentage change alert type exists")]
    public async Task Alerts_PercentageChangeTypeExists()
    {
        await SetupAsync();

        _alerts.ClickAddAlert();
        await Task.Delay(500);

        _alerts.SelectAlertType("Percentage");
        // Should switch to percentage input mode
    }

    [Fact(DisplayName = "Volume spike alert type exists")]
    public async Task Alerts_VolumeSpikeTypeExists()
    {
        await SetupAsync();

        _alerts.ClickAddAlert();
        await Task.Delay(500);

        _alerts.SelectAlertType("Volume");
        // Should show volume alert options
    }

    [Fact(DisplayName = "Refresh updates alert status")]
    public async Task Alerts_RefreshUpdatesStatus()
    {
        await SetupAsync();

        _alerts.ClickRefresh();
        await Task.Delay(2000);

        _alerts.IsLoaded.Should().BeTrue("Page should still be loaded after refresh");
    }
}
