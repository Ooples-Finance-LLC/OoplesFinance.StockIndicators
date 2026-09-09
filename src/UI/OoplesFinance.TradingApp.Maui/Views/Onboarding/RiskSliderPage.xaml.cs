namespace OoplesFinance.TradingApp.Maui.Views.Onboarding;

public partial class RiskSliderPage : ContentPage
{
    private readonly (string Label, int Stocks, int Bonds, int Cash, double AvgReturn, double MaxDrawdown)[] _riskProfiles =
    {
        ("Very Conservative", 20, 50, 30, 4.2, -8),
        ("Conservative", 40, 45, 15, 5.5, -12),
        ("Moderate", 60, 30, 10, 7.2, -18),
        ("Aggressive", 80, 15, 5, 9.1, -28),
        ("Very Aggressive", 95, 5, 0, 11.5, -40)
    };

    public RiskSliderPage()
    {
        App.LogError("RiskSliderPage", "Constructor starting");
        try
        {
            InitializeComponent();
            UpdateRiskDisplay((int)RiskSlider.Value);
            App.LogError("RiskSliderPage", "Constructor completed");
        }
        catch (Exception ex)
        {
            App.LogException("RiskSliderPage.Constructor", ex);
            throw;
        }
    }

    private void OnRiskValueChanged(object sender, ValueChangedEventArgs e)
    {
        // Snap to integer values
        int value = (int)Math.Round(e.NewValue);
        if (Math.Abs(e.NewValue - value) > 0.01)
        {
            RiskSlider.Value = value;
            return;
        }

        UpdateRiskDisplay(value);
    }

    private void UpdateRiskDisplay(int level)
    {
        int index = Math.Clamp(level - 1, 0, 4);
        var profile = _riskProfiles[index];

        RiskLevelLabel.Text = profile.Label;

        // Update allocation bars
        StocksPercentLabel.Text = $"{profile.Stocks}%";
        StocksBar.Progress = profile.Stocks / 100.0;

        BondsPercentLabel.Text = $"{profile.Bonds}%";
        BondsBar.Progress = profile.Bonds / 100.0;

        CashPercentLabel.Text = $"{profile.Cash}%";
        CashBar.Progress = profile.Cash / 100.0;

        // Update performance
        AvgReturnLabel.Text = $"+{profile.AvgReturn:F1}%";
        MaxDrawdownLabel.Text = $"{profile.MaxDrawdown}%";

        // Color coding
        RiskLevelLabel.TextColor = level switch
        {
            1 => Color.FromArgb("#2196F3"), // Blue
            2 => Color.FromArgb("#4CAF50"), // Green
            3 => Color.FromArgb("#FFC107"), // Yellow
            4 => Color.FromArgb("#FF9800"), // Orange
            5 => Color.FromArgb("#F44336"), // Red
            _ => Color.FromArgb("#4CAF50")
        };
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnStartTradingClicked(object sender, EventArgs e)
    {
        // Save risk level
        int riskLevel = (int)RiskSlider.Value;
        Preferences.Set("risk_level", riskLevel);
        Preferences.Set("onboarding_complete", true);

        // Navigate to main app
        await Shell.Current.GoToAsync("//DashboardPage");
    }
}
