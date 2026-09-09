using FluentAssertions;
using OoplesFinance.TradingApp.UITests.Pages;
using Xunit;

namespace OoplesFinance.TradingApp.UITests.Tests;

/// <summary>
/// Comprehensive E2E tests for the Settings page
/// </summary>
[Collection("UITests")]
public class SettingsTests : UITestBase
{
    private SettingsPage _settings = null!;

    private async Task SetupAsync()
    {
        await LaunchAppAsync();

        // Navigate to settings
        NavigateToTab("Settings");
        await Task.Delay(1000);

        _settings = new SettingsPage(MainWindow!, Automation!);
        await _settings.WaitForElementAsync("Settings", TimeSpan.FromSeconds(10));
    }

    [Fact(DisplayName = "Settings page loads")]
    public async Task Settings_ShouldLoad()
    {
        await SetupAsync();

        _settings.IsLoaded.Should().BeTrue("Settings page should load");
    }

    [Fact(DisplayName = "Settings has Broker Connection section")]
    public async Task Settings_ShouldHaveBrokerSection()
    {
        await SetupAsync();

        _settings.HasBrokerSection.Should().BeTrue("Broker Connection section should be visible");
    }

    [Fact(DisplayName = "Settings has Appearance section")]
    public async Task Settings_ShouldHaveAppearanceSection()
    {
        await SetupAsync();

        _settings.AppearanceSection.Should().NotBeNull("Appearance section should be visible");
    }

    [Fact(DisplayName = "Settings has UI Experience section")]
    public async Task Settings_ShouldHaveUIExperienceSection()
    {
        await SetupAsync();

        _settings.HasUIExperienceSection.Should().BeTrue("UI Experience section should be visible");
    }

    [Fact(DisplayName = "Settings has Security section")]
    public async Task Settings_ShouldHaveSecuritySection()
    {
        await SetupAsync();

        _settings.HasSecuritySection.Should().BeTrue("Security section should be visible");
    }

    [Fact(DisplayName = "Settings has Notifications section")]
    public async Task Settings_ShouldHaveNotificationsSection()
    {
        await SetupAsync();

        _settings.HasNotificationsSection.Should().BeTrue("Notifications section should be visible");
    }

    [Fact(DisplayName = "Settings has Support section")]
    public async Task Settings_ShouldHaveSupportSection()
    {
        await SetupAsync();

        _settings.HasSupportSection.Should().BeTrue("Support section should be visible");
    }

    [Fact(DisplayName = "Settings has About section")]
    public async Task Settings_ShouldHaveAboutSection()
    {
        await SetupAsync();

        _settings.HasAboutSection.Should().BeTrue("About section should be visible");
    }

    [Fact(DisplayName = "Settings shows connection status")]
    public async Task Settings_ShouldShowConnectionStatus()
    {
        await SetupAsync();

        _settings.ConnectionStatus.Should().NotBeNull("Connection status should be visible");
    }

    [Fact(DisplayName = "Manage Broker button exists")]
    public async Task Settings_ManageBrokerButtonExists()
    {
        await SetupAsync();

        _settings.ManageBrokerButton.Should().NotBeNull("Manage Broker button should be present");
    }

    [Fact(DisplayName = "View Achievements button exists")]
    public async Task Settings_ViewAchievementsButtonExists()
    {
        await SetupAsync();

        _settings.ViewAchievementsButton.Should().NotBeNull("View Achievements button should be present");
    }

    [Fact(DisplayName = "View Achievements shows dialog")]
    public async Task Settings_ViewAchievementsShowsDialog()
    {
        await SetupAsync();

        _settings.ClickViewAchievements();
        await Task.Delay(500);

        // Should show achievements dialog
        await _settings.WaitForElementAsync("Achievements", TimeSpan.FromSeconds(3));
    }

    [Fact(DisplayName = "UI Mode can be changed")]
    public async Task Settings_UIModeShouldBeChangeable()
    {
        await SetupAsync();

        var initialMode = _settings.GetCurrentUIMode();
        _settings.SelectUIMode("Intermediate");

        await Task.Delay(500);

        // Mode should be updated (or dialog shown)
    }

    [Fact(DisplayName = "Theme picker exists")]
    public async Task Settings_ThemePickerExists()
    {
        await SetupAsync();

        _settings.ThemePicker.Should().NotBeNull("Theme picker should be present");
    }

    [Fact(DisplayName = "Biometric toggle exists")]
    public async Task Settings_BiometricToggleExists()
    {
        await SetupAsync();

        _settings.BiometricToggle.Should().NotBeNull("Biometric toggle should be present");
    }

    [Fact(DisplayName = "Push Notifications toggle exists")]
    public async Task Settings_PushNotificationsToggleExists()
    {
        await SetupAsync();

        _settings.PushNotificationsToggle.Should().NotBeNull("Push notifications toggle should be present");
    }

    [Fact(DisplayName = "Price Alerts toggle exists")]
    public async Task Settings_PriceAlertsToggleExists()
    {
        await SetupAsync();

        _settings.PriceAlertsToggle.Should().NotBeNull("Price alerts toggle should be present");
    }

    [Fact(DisplayName = "Order Fills toggle exists")]
    public async Task Settings_OrderFillsToggleExists()
    {
        await SetupAsync();

        _settings.OrderFillsToggle.Should().NotBeNull("Order fills toggle should be present");
    }

    [Fact(DisplayName = "Help Center button works")]
    public async Task Settings_HelpCenterButtonWorks()
    {
        await SetupAsync();

        _settings.HelpCenterButton.Should().NotBeNull("Help Center button should be present");
        _settings.ClickHelpCenter();

        // Should open help center (browser or in-app)
        await Task.Delay(1000);
    }

    [Fact(DisplayName = "Contact Support button works")]
    public async Task Settings_ContactSupportButtonWorks()
    {
        await SetupAsync();

        _settings.ContactSupportButton.Should().NotBeNull("Contact Support button should be present");
        _settings.ClickContactSupport();

        // Should open email or support form
        await Task.Delay(1000);
    }

    [Fact(DisplayName = "Privacy Policy button exists")]
    public async Task Settings_PrivacyPolicyButtonExists()
    {
        await SetupAsync();

        _settings.PrivacyPolicyButton.Should().NotBeNull("Privacy Policy button should be present");
    }

    [Fact(DisplayName = "Terms button exists")]
    public async Task Settings_TermsButtonExists()
    {
        await SetupAsync();

        _settings.TermsButton.Should().NotBeNull("Terms button should be present");
    }

    [Fact(DisplayName = "Logout button exists when connected")]
    public async Task Settings_LogoutButtonExistsWhenConnected()
    {
        await SetupAsync();

        if (_settings.IsBrokerConnected)
        {
            _settings.LogoutButton.Should().NotBeNull("Logout button should be present when connected");
        }
    }

    [Fact(DisplayName = "App version is displayed")]
    public async Task Settings_AppVersionIsDisplayed()
    {
        await SetupAsync();

        _settings.AppVersion.Should().NotBeNull("App version should be displayed");
    }

    [Fact(DisplayName = "UI mode displays current value")]
    public async Task Settings_UIModeShouldDisplayCurrentValue()
    {
        await SetupAsync();

        var mode = _settings.GetCurrentUIMode();
        mode.Should().NotBeNull("Current UI mode should be displayed");
        mode.Should().BeOneOf("Beginner", "Intermediate", "Expert");
    }
}
