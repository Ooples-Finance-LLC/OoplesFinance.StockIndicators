namespace OoplesFinance.TradingApp.Maui.Views.Onboarding;

public partial class WelcomePage : ContentPage
{
    public WelcomePage()
    {
        App.LogError("WelcomePage", "Constructor starting");
        try
        {
            InitializeComponent();
            App.LogError("WelcomePage", "Constructor completed");
        }
        catch (Exception ex)
        {
            App.LogException("WelcomePage.Constructor", ex);
            throw;
        }
    }

    private async void OnGetStartedClicked(object sender, EventArgs e)
    {
        App.LogError("WelcomePage", "Get Started clicked");
        try
        {
            await Shell.Current.GoToAsync(nameof(RiskQuizPage));
        }
        catch (Exception ex)
        {
            App.LogException("WelcomePage.OnGetStartedClicked", ex);
        }
    }

    private async void OnSignInTapped(object sender, EventArgs e)
    {
        App.LogError("WelcomePage", "Sign In tapped");
        try
        {
            await Shell.Current.GoToAsync("//LoginPage");
        }
        catch (Exception ex)
        {
            App.LogException("WelcomePage.OnSignInTapped", ex);
        }
    }
}
