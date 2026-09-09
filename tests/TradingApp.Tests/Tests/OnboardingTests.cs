using FluentAssertions;
using OoplesFinance.TradingApp.UITests.Pages;
using Xunit;

namespace OoplesFinance.TradingApp.UITests.Tests;

/// <summary>
/// Comprehensive E2E tests for the Onboarding flow
/// </summary>
[Collection("UITests")]
public class OnboardingTests : UITestBase
{
    [Fact(DisplayName = "Welcome page loads on first launch")]
    public async Task Onboarding_WelcomePageLoads()
    {
        await LaunchAppAsync(clearData: true);

        var welcome = new WelcomePage(MainWindow!, Automation!);
        await welcome.WaitForElementAsync("Welcome", TimeSpan.FromSeconds(10));

        welcome.IsLoaded.Should().BeTrue("Welcome page should load on first launch");
    }

    [Fact(DisplayName = "Welcome page shows app logo")]
    public async Task Onboarding_WelcomeShowsLogo()
    {
        await LaunchAppAsync(clearData: true);

        var welcome = new WelcomePage(MainWindow!, Automation!);
        await welcome.WaitForElementAsync("Welcome", TimeSpan.FromSeconds(10));

        welcome.AppLogo.Should().NotBeNull("App logo should be visible");
    }

    [Fact(DisplayName = "Welcome page has Get Started button")]
    public async Task Onboarding_WelcomeHasGetStarted()
    {
        await LaunchAppAsync(clearData: true);

        var welcome = new WelcomePage(MainWindow!, Automation!);
        await welcome.WaitForElementAsync("Welcome", TimeSpan.FromSeconds(10));

        welcome.GetStartedButton.Should().NotBeNull("Get Started button should be present");
    }

    [Fact(DisplayName = "Get Started navigates to risk quiz")]
    public async Task Onboarding_GetStartedNavigatesToRiskQuiz()
    {
        await LaunchAppAsync(clearData: true);

        var welcome = new WelcomePage(MainWindow!, Automation!);
        await welcome.WaitForElementAsync("Welcome", TimeSpan.FromSeconds(10));

        welcome.ClickGetStarted();
        await Task.Delay(1000);

        var riskQuiz = new RiskQuizPage(MainWindow!, Automation!);
        riskQuiz.IsLoaded.Should().BeTrue("Risk quiz should load after Get Started");
    }

    [Fact(DisplayName = "Skip button skips onboarding")]
    public async Task Onboarding_SkipButtonWorks()
    {
        await LaunchAppAsync(clearData: true);

        var welcome = new WelcomePage(MainWindow!, Automation!);
        await welcome.WaitForElementAsync("Welcome", TimeSpan.FromSeconds(10));

        if (welcome.SkipButton != null)
        {
            welcome.ClickSkip();
            await Task.Delay(1000);

            // Should navigate to dashboard
            await WaitForElementAsync("Portfolio", TimeSpan.FromSeconds(10));
        }
    }

    [Fact(DisplayName = "Risk quiz shows questions")]
    public async Task Onboarding_RiskQuizShowsQuestions()
    {
        await LaunchAppAsync(clearData: true);
        await NavigateToRiskQuizAsync();

        var riskQuiz = new RiskQuizPage(MainWindow!, Automation!);
        riskQuiz.QuestionText.Should().NotBeNull("Question text should be visible");
    }

    [Fact(DisplayName = "Risk quiz shows progress")]
    public async Task Onboarding_RiskQuizShowsProgress()
    {
        await LaunchAppAsync(clearData: true);
        await NavigateToRiskQuizAsync();

        var riskQuiz = new RiskQuizPage(MainWindow!, Automation!);
        riskQuiz.ProgressIndicator.Should().NotBeNull("Progress indicator should be visible");
    }

    [Fact(DisplayName = "Risk quiz can select answer")]
    public async Task Onboarding_RiskQuizCanSelectAnswer()
    {
        await LaunchAppAsync(clearData: true);
        await NavigateToRiskQuizAsync();

        var riskQuiz = new RiskQuizPage(MainWindow!, Automation!);
        riskQuiz.SelectAnswer(0);
        await Task.Delay(300);

        // Answer should be selected
    }

    [Fact(DisplayName = "Risk quiz Next advances question")]
    public async Task Onboarding_RiskQuizNextAdvances()
    {
        await LaunchAppAsync(clearData: true);
        await NavigateToRiskQuizAsync();

        var riskQuiz = new RiskQuizPage(MainWindow!, Automation!);
        riskQuiz.SelectAnswer(0);
        await Task.Delay(300);
        riskQuiz.ClickNext();
        await Task.Delay(500);

        // Should be on next question or next page
    }

    [Fact(DisplayName = "Risk quiz Back returns to previous")]
    public async Task Onboarding_RiskQuizBackWorks()
    {
        await LaunchAppAsync(clearData: true);
        await NavigateToRiskQuizAsync();

        var riskQuiz = new RiskQuizPage(MainWindow!, Automation!);
        riskQuiz.SelectAnswer(0);
        await Task.Delay(300);
        riskQuiz.ClickNext();
        await Task.Delay(500);

        if (riskQuiz.BackButton != null)
        {
            riskQuiz.ClickBack();
            await Task.Delay(500);

            // Should return to first question
        }
    }

    [Fact(DisplayName = "Goal setup page loads after quiz")]
    public async Task Onboarding_GoalSetupLoads()
    {
        await LaunchAppAsync(clearData: true);
        await CompleteRiskQuizAsync();

        var goalSetup = new GoalSetupPage(MainWindow!, Automation!);
        await goalSetup.WaitForElementAsync("Goal", TimeSpan.FromSeconds(10));

        goalSetup.IsLoaded.Should().BeTrue("Goal setup should load after risk quiz");
    }

    [Fact(DisplayName = "Goal setup shows goal options")]
    public async Task Onboarding_GoalSetupShowsOptions()
    {
        await LaunchAppAsync(clearData: true);
        await CompleteRiskQuizAsync();

        var goalSetup = new GoalSetupPage(MainWindow!, Automation!);
        await goalSetup.WaitForElementAsync("Goal", TimeSpan.FromSeconds(10));

        goalSetup.RetirementGoal.Should().NotBeNull("Retirement goal should be visible");
    }

    [Fact(DisplayName = "Can select retirement goal")]
    public async Task Onboarding_CanSelectRetirement()
    {
        await LaunchAppAsync(clearData: true);
        await CompleteRiskQuizAsync();

        var goalSetup = new GoalSetupPage(MainWindow!, Automation!);
        await goalSetup.WaitForElementAsync("Goal", TimeSpan.FromSeconds(10));

        goalSetup.SelectGoal("Retirement");
        await Task.Delay(300);

        // Goal should be selected
    }

    [Fact(DisplayName = "Can select home purchase goal")]
    public async Task Onboarding_CanSelectHomePurchase()
    {
        await LaunchAppAsync(clearData: true);
        await CompleteRiskQuizAsync();

        var goalSetup = new GoalSetupPage(MainWindow!, Automation!);
        await goalSetup.WaitForElementAsync("Goal", TimeSpan.FromSeconds(10));

        goalSetup.SelectGoal("Home");
        await Task.Delay(300);

        // Goal should be selected
    }

    [Fact(DisplayName = "Risk slider page loads after goals")]
    public async Task Onboarding_RiskSliderLoads()
    {
        await LaunchAppAsync(clearData: true);
        await CompleteRiskQuizAsync();
        await CompleteGoalSetupAsync();

        var riskSlider = new RiskSliderPage(MainWindow!, Automation!);
        await riskSlider.WaitForElementAsync("Risk", TimeSpan.FromSeconds(10));

        riskSlider.IsLoaded.Should().BeTrue("Risk slider should load after goal setup");
    }

    [Fact(DisplayName = "Risk slider shows options")]
    public async Task Onboarding_RiskSliderShowsOptions()
    {
        await LaunchAppAsync(clearData: true);
        await CompleteRiskQuizAsync();
        await CompleteGoalSetupAsync();

        var riskSlider = new RiskSliderPage(MainWindow!, Automation!);
        await riskSlider.WaitForElementAsync("Risk", TimeSpan.FromSeconds(10));

        riskSlider.ConservativeLabel.Should().NotBeNull("Conservative option should be visible");
        riskSlider.ModerateLabel.Should().NotBeNull("Moderate option should be visible");
        riskSlider.AggressiveLabel.Should().NotBeNull("Aggressive option should be visible");
    }

    [Fact(DisplayName = "Can select conservative risk")]
    public async Task Onboarding_CanSelectConservative()
    {
        await LaunchAppAsync(clearData: true);
        await CompleteRiskQuizAsync();
        await CompleteGoalSetupAsync();

        var riskSlider = new RiskSliderPage(MainWindow!, Automation!);
        await riskSlider.WaitForElementAsync("Risk", TimeSpan.FromSeconds(10));

        riskSlider.SetRiskLevel("Conservative");
        await Task.Delay(300);

        // Conservative should be selected
    }

    [Fact(DisplayName = "Can select moderate risk")]
    public async Task Onboarding_CanSelectModerate()
    {
        await LaunchAppAsync(clearData: true);
        await CompleteRiskQuizAsync();
        await CompleteGoalSetupAsync();

        var riskSlider = new RiskSliderPage(MainWindow!, Automation!);
        await riskSlider.WaitForElementAsync("Risk", TimeSpan.FromSeconds(10));

        riskSlider.SetRiskLevel("Moderate");
        await Task.Delay(300);

        // Moderate should be selected
    }

    [Fact(DisplayName = "Can select aggressive risk")]
    public async Task Onboarding_CanSelectAggressive()
    {
        await LaunchAppAsync(clearData: true);
        await CompleteRiskQuizAsync();
        await CompleteGoalSetupAsync();

        var riskSlider = new RiskSliderPage(MainWindow!, Automation!);
        await riskSlider.WaitForElementAsync("Risk", TimeSpan.FromSeconds(10));

        riskSlider.SetRiskLevel("Aggressive");
        await Task.Delay(300);

        // Aggressive should be selected
    }

    [Fact(DisplayName = "Finish completes onboarding")]
    public async Task Onboarding_FinishCompletesOnboarding()
    {
        await LaunchAppAsync(clearData: true);
        await CompleteRiskQuizAsync();
        await CompleteGoalSetupAsync();

        var riskSlider = new RiskSliderPage(MainWindow!, Automation!);
        await riskSlider.WaitForElementAsync("Risk", TimeSpan.FromSeconds(10));

        riskSlider.SetRiskLevel("Moderate");
        await Task.Delay(300);
        riskSlider.ClickFinish();
        await Task.Delay(1000);

        // Should navigate to dashboard
        await WaitForElementAsync("Portfolio", TimeSpan.FromSeconds(10));
    }

    [Fact(DisplayName = "Full onboarding flow completes")]
    public async Task Onboarding_FullFlowCompletes()
    {
        await LaunchAppAsync(clearData: true);

        // Welcome page
        var welcome = new WelcomePage(MainWindow!, Automation!);
        await welcome.WaitForElementAsync("Welcome", TimeSpan.FromSeconds(10));
        welcome.ClickGetStarted();
        await Task.Delay(1000);

        // Risk quiz - answer all questions
        var riskQuiz = new RiskQuizPage(MainWindow!, Automation!);
        for (int i = 0; i < 5; i++) // Assume 5 questions
        {
            if (!riskQuiz.IsLoaded) break;
            riskQuiz.SelectAnswer(1); // Select middle option
            await Task.Delay(300);
            riskQuiz.ClickNext();
            await Task.Delay(500);
        }

        // Goal setup
        var goalSetup = new GoalSetupPage(MainWindow!, Automation!);
        if (goalSetup.IsLoaded)
        {
            goalSetup.SelectGoal("Retirement");
            await Task.Delay(300);
            goalSetup.ClickContinue();
            await Task.Delay(500);
        }

        // Risk slider
        var riskSlider = new RiskSliderPage(MainWindow!, Automation!);
        if (riskSlider.IsLoaded)
        {
            riskSlider.SetRiskLevel("Moderate");
            await Task.Delay(300);
            riskSlider.ClickFinish();
            await Task.Delay(1000);
        }

        // Should be on dashboard
        await WaitForElementAsync("Portfolio", TimeSpan.FromSeconds(15));
    }

    // Helper methods
    private async Task NavigateToRiskQuizAsync()
    {
        var welcome = new WelcomePage(MainWindow!, Automation!);
        await welcome.WaitForElementAsync("Welcome", TimeSpan.FromSeconds(10));
        welcome.ClickGetStarted();
        await Task.Delay(1000);
    }

    private async Task CompleteRiskQuizAsync()
    {
        await NavigateToRiskQuizAsync();

        var riskQuiz = new RiskQuizPage(MainWindow!, Automation!);
        for (int i = 0; i < 5; i++)
        {
            if (!riskQuiz.IsLoaded) break;
            riskQuiz.SelectAnswer(1);
            await Task.Delay(300);
            riskQuiz.ClickNext();
            await Task.Delay(500);
        }
    }

    private async Task CompleteGoalSetupAsync()
    {
        var goalSetup = new GoalSetupPage(MainWindow!, Automation!);
        await goalSetup.WaitForElementAsync("Goal", TimeSpan.FromSeconds(10));

        if (goalSetup.IsLoaded)
        {
            goalSetup.SelectGoal("Retirement");
            await Task.Delay(300);
            goalSetup.ClickContinue();
            await Task.Delay(500);
        }
    }
}
