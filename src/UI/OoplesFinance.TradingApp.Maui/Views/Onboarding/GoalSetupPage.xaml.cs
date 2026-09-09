namespace OoplesFinance.TradingApp.Maui.Views.Onboarding;

public partial class GoalSetupPage : ContentPage
{
    private string? _selectedGoal;
    private Border? _selectedBorder;

    public GoalSetupPage()
    {
        App.LogError("GoalSetupPage", "Constructor starting");
        try
        {
            InitializeComponent();
            TargetDatePicker.Date = DateTime.Today.AddYears(5);
            App.LogError("GoalSetupPage", "Constructor completed");
        }
        catch (Exception ex)
        {
            App.LogException("GoalSetupPage.Constructor", ex);
            throw;
        }
    }

    private void OnGoalSelected(object sender, EventArgs e)
    {
        if (sender is not Border border) return;

        // Deselect previous
        if (_selectedBorder is not null)
        {
            _selectedBorder.Stroke = Color.FromArgb("#334155");
            _selectedBorder.BackgroundColor = Color.FromArgb("#1E293B");
        }

        // Select new
        border.Stroke = Color.FromArgb("#10B981");
        border.BackgroundColor = Color.FromArgb("#1E3D1E");
        _selectedBorder = border;

        // Get goal type from CommandParameter
        if (border.GestureRecognizers.FirstOrDefault() is TapGestureRecognizer tap)
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
        var targetDate = TargetDatePicker.Date ?? DateTime.Today.AddYears(5);
        Preferences.Set("goal_target_date", targetDate.ToString("o"));

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
