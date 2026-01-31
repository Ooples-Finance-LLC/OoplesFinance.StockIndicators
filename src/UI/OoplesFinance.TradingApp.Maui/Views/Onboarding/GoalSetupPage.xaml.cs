namespace OoplesFinance.TradingApp.Maui.Views.Onboarding;

public partial class GoalSetupPage : ContentPage
{
    private string? _selectedGoal;
    private Frame? _selectedFrame;

    public GoalSetupPage()
    {
        InitializeComponent();
        TargetDatePicker.Date = DateTime.Today.AddYears(5);
    }

    private void OnGoalSelected(object sender, EventArgs e)
    {
        if (sender is not Frame frame) return;

        // Deselect previous
        if (_selectedFrame is not null)
        {
            _selectedFrame.BorderColor = Color.FromArgb("#3D3D3D");
            _selectedFrame.BackgroundColor = Color.FromArgb("#2D2D2D");
        }

        // Select new
        frame.BorderColor = Color.FromArgb("#4CAF50");
        frame.BackgroundColor = Color.FromArgb("#1E3D1E");
        _selectedFrame = frame;

        // Get goal type from CommandParameter
        if (frame.GestureRecognizers.FirstOrDefault() is TapGestureRecognizer tap)
        {
            _selectedGoal = tap.CommandParameter?.ToString();
        }

        // Show details form
        GoalDetailsFrame.IsVisible = true;
        NextButton.IsEnabled = true;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnNextClicked(object sender, EventArgs e)
    {
        // Save goal settings
        if (decimal.TryParse(TargetAmountEntry.Text?.Replace("$", "").Replace(",", ""), out var targetAmount))
        {
            Preferences.Set("goal_target_amount", (double)targetAmount);
        }

        Preferences.Set("goal_type", _selectedGoal ?? "Growth");
        Preferences.Set("goal_target_date", TargetDatePicker.Date.ToString("o"));

        if (decimal.TryParse(InitialInvestmentEntry.Text?.Replace("$", "").Replace(",", ""), out var initial))
        {
            Preferences.Set("goal_initial_investment", (double)initial);
        }

        if (decimal.TryParse(MonthlyContributionEntry.Text?.Replace("$", "").Replace(",", ""), out var monthly))
        {
            Preferences.Set("goal_monthly_contribution", (double)monthly);
        }

        await Shell.Current.GoToAsync(nameof(RiskSliderPage));
    }
}
