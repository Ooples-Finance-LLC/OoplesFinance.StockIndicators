using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace OoplesFinance.TradingApp.UITests.Pages;

/// <summary>
/// Page object for the Welcome page (first onboarding screen)
/// </summary>
public class WelcomePage : BasePage
{
    public WelcomePage(Window window, UIA3Automation automation) : base(window, automation) { }

    public AutomationElement? WelcomeText => FindByText("Welcome");
    public AutomationElement? AppLogo => FindByAutomationId("AppLogo");
    public Button? GetStartedButton => FindButton("Get Started") ?? FindButton("Continue") ?? FindButton("Next");
    public Button? SkipButton => FindButton("Skip");

    public void ClickGetStarted() => GetStartedButton?.Click();
    public void ClickSkip() => SkipButton?.Click();

    public bool IsLoaded => WelcomeText != null || GetStartedButton != null;
}

/// <summary>
/// Page object for the Risk Quiz page
/// </summary>
public class RiskQuizPage : BasePage
{
    public RiskQuizPage(Window window, UIA3Automation automation) : base(window, automation) { }

    public AutomationElement? QuestionText => FindByText("?");
    public AutomationElement? ProgressIndicator => FindByText("/");
    public Button? NextButton => FindButton("Next") ?? FindButton("Continue");
    public Button? BackButton => FindButton("Back") ?? FindButton("Previous");

    public void SelectAnswer(int answerIndex)
    {
        // Click on answer option by index
        var answers = Window.FindAllDescendants()
            .Where(e => e.ControlType == FlaUI.Core.Definitions.ControlType.RadioButton ||
                       e.ControlType == FlaUI.Core.Definitions.ControlType.Button)
            .ToList();

        if (answerIndex < answers.Count)
        {
            answers[answerIndex].Click();
            Thread.Sleep(200);
        }
    }

    public void ClickNext() => NextButton?.Click();
    public void ClickBack() => BackButton?.Click();

    public bool IsLoaded => QuestionText != null || NextButton != null;
}

/// <summary>
/// Page object for the Goal Setup page
/// </summary>
public class GoalSetupPage : BasePage
{
    public GoalSetupPage(Window window, UIA3Automation automation) : base(window, automation) { }

    public AutomationElement? PageTitle => FindByText("Goal") ?? FindByText("target");
    public AutomationElement? RetirementGoal => FindByText("Retirement");
    public AutomationElement? HomePurchaseGoal => FindByText("Home") ?? FindByText("House");
    public AutomationElement? EducationGoal => FindByText("Education");
    public AutomationElement? VacationGoal => FindByText("Vacation");
    public AutomationElement? EmergencyFundGoal => FindByText("Emergency");
    public AutomationElement? CustomGoal => FindByText("Custom");
    public Button? ContinueButton => FindButton("Continue") ?? FindButton("Next");

    public void SelectGoal(string goalType)
    {
        ClickButton(goalType);
        Thread.Sleep(300);
    }

    public void ClickContinue() => ContinueButton?.Click();

    public bool IsLoaded => PageTitle != null || RetirementGoal != null;
}

/// <summary>
/// Page object for the Risk Slider page
/// </summary>
public class RiskSliderPage : BasePage
{
    public RiskSliderPage(Window window, UIA3Automation automation) : base(window, automation) { }

    public AutomationElement? PageTitle => FindByText("Risk");
    public AutomationElement? RiskSlider => FindByAutomationId("RiskSlider") ?? FindByName("Risk Slider");
    public AutomationElement? ConservativeLabel => FindByText("Conservative");
    public AutomationElement? ModerateLabel => FindByText("Moderate");
    public AutomationElement? AggressiveLabel => FindByText("Aggressive");
    public Button? FinishButton => FindButton("Finish") ?? FindButton("Complete") ?? FindButton("Done");

    public void SetRiskLevel(string level)
    {
        // Click on the risk level label
        ClickButton(level);
        Thread.Sleep(300);
    }

    public void ClickFinish() => FinishButton?.Click();

    public bool IsLoaded => PageTitle != null || RiskSlider != null;
}
