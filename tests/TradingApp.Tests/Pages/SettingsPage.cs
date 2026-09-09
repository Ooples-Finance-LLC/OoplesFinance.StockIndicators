using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace OoplesFinance.TradingApp.UITests.Pages;

/// <summary>
/// Page object for the Settings page
/// </summary>
public class SettingsPage : BasePage
{
    public SettingsPage(Window window, UIA3Automation automation) : base(window, automation) { }

    // Section headers
    public AutomationElement? BrokerSection => FindByText("Broker Connection");
    public AutomationElement? AppearanceSection => FindByText("Appearance");
    public AutomationElement? UIExperienceSection => FindByText("UI Experience");
    public AutomationElement? SecuritySection => FindByText("Security");
    public AutomationElement? NotificationsSection => FindByText("Notifications");
    public AutomationElement? SupportSection => FindByText("Support");
    public AutomationElement? AboutSection => FindByText("About");

    // Broker connection
    public AutomationElement? ConnectionStatus => FindByText("Connected") ?? FindByText("Not Connected");
    public Button? ManageBrokerButton => FindButton("Connect") ?? FindButton("Disconnect");

    // UI Experience
    public AutomationElement? SkillLevelPicker => FindByText("Skill Level");
    public AutomationElement? UnlockedFeaturesCount => FindByText("Features Unlocked");
    public AutomationElement? AchievementsCount => FindByText("Achievements");
    public Button? ViewAchievementsButton => FindButton("View All Achievements");

    // Theme
    public AutomationElement? ThemePicker => FindByText("Theme");

    // Security
    public AutomationElement? BiometricToggle => FindByText("Biometric");

    // Notifications
    public AutomationElement? PushNotificationsToggle => FindByText("Push Notifications");
    public AutomationElement? PriceAlertsToggle => FindByText("Price Alerts");
    public AutomationElement? OrderFillsToggle => FindByText("Order Fills");

    // Support
    public Button? HelpCenterButton => FindButton("Help Center");
    public Button? ContactSupportButton => FindButton("Contact Support");
    public Button? PrivacyPolicyButton => FindButton("Privacy Policy");
    public Button? TermsButton => FindButton("Terms of Service");

    // Account
    public Button? LogoutButton => FindButton("Log Out");
    public AutomationElement? AppVersion => FindByText("Version");

    // Actions
    public void ClickManageBroker() => ManageBrokerButton?.Click();
    public void ClickViewAchievements() => ViewAchievementsButton?.Click();
    public void ClickHelpCenter() => HelpCenterButton?.Click();
    public void ClickContactSupport() => ContactSupportButton?.Click();
    public void ClickLogout() => LogoutButton?.Click();

    public void SelectUIMode(string mode)
    {
        // Click on the UI mode picker and select the mode
        var picker = SkillLevelPicker;
        picker?.Click();
        Thread.Sleep(300);
        ClickButton(mode);
        Thread.Sleep(300);
    }

    public void SelectTheme(string theme)
    {
        var picker = ThemePicker;
        picker?.Click();
        Thread.Sleep(300);
        ClickButton(theme);
        Thread.Sleep(300);
    }

    public void ToggleBiometric()
    {
        var toggle = BiometricToggle;
        toggle?.Click();
    }

    public void TogglePushNotifications()
    {
        var toggle = PushNotificationsToggle;
        toggle?.Click();
    }

    // Verifications
    public bool IsLoaded => BrokerSection != null || AppearanceSection != null;
    public bool HasBrokerSection => BrokerSection != null;
    public bool HasUIExperienceSection => UIExperienceSection != null;
    public bool HasSecuritySection => SecuritySection != null;
    public bool HasNotificationsSection => NotificationsSection != null;
    public bool HasSupportSection => SupportSection != null;
    public bool HasAboutSection => AboutSection != null;

    public bool IsBrokerConnected => FindByText("Connected", TimeSpan.FromSeconds(2)) != null;

    public string? GetCurrentUIMode()
    {
        if (FindByText("Beginner", TimeSpan.FromSeconds(1)) != null) return "Beginner";
        if (FindByText("Intermediate", TimeSpan.FromSeconds(1)) != null) return "Intermediate";
        if (FindByText("Expert", TimeSpan.FromSeconds(1)) != null) return "Expert";
        return null;
    }
}
