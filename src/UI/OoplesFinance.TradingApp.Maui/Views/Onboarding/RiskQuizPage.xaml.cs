using OoplesFinance.TradingApp.Maui.Services;

namespace OoplesFinance.TradingApp.Maui.Views.Onboarding;

public partial class RiskQuizPage : ContentPage
{
    private readonly IAdaptiveUIService _adaptiveUI;
    private readonly List<QuizQuestion> _questions;
    private int _currentIndex = 0;
    private int _totalScore = 0;

    public RiskQuizPage(IAdaptiveUIService adaptiveUI)
    {
        InitializeComponent();
        _adaptiveUI = adaptiveUI;
        _questions = CreateQuestions();
        QuestionsCarousel.ItemsSource = _questions;
    }

    private List<QuizQuestion> CreateQuestions()
    {
        return new List<QuizQuestion>
        {
            new QuizQuestion
            {
                Question = "How would you describe your investment experience?",
                Options = new List<QuizOption>
                {
                    new("I'm new to investing", 1),
                    new("I've invested in stocks/mutual funds", 2),
                    new("I actively trade stocks and options", 3),
                    new("I'm a professional trader/investor", 4)
                }
            },
            new QuizQuestion
            {
                Question = "How would you react if your portfolio dropped 20% in a month?",
                Options = new List<QuizOption>
                {
                    new("Sell everything immediately", 1),
                    new("Sell some positions to reduce risk", 2),
                    new("Hold and wait for recovery", 3),
                    new("Buy more at lower prices", 4)
                }
            },
            new QuizQuestion
            {
                Question = "What is your primary investment goal?",
                Options = new List<QuizOption>
                {
                    new("Preserve my capital", 1),
                    new("Generate steady income", 2),
                    new("Grow my wealth over time", 3),
                    new("Maximize returns aggressively", 4)
                }
            },
            new QuizQuestion
            {
                Question = "How long do you plan to invest?",
                Options = new List<QuizOption>
                {
                    new("Less than 1 year", 1),
                    new("1-3 years", 2),
                    new("3-10 years", 3),
                    new("More than 10 years", 4)
                }
            },
            new QuizQuestion
            {
                Question = "Which best describes your understanding of options trading?",
                Options = new List<QuizOption>
                {
                    new("I don't know what options are", 1),
                    new("I know the basics (calls/puts)", 2),
                    new("I understand Greeks and strategies", 3),
                    new("I trade complex multi-leg strategies", 4)
                }
            }
        };
    }

    private void OnCurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
    {
        _currentIndex = _questions.IndexOf((QuizQuestion)e.CurrentItem);
        UpdateUI();
    }

    private void UpdateUI()
    {
        QuestionCounter.Text = $"Question {_currentIndex + 1} of {_questions.Count}";
        ProgressBar.Progress = 0.33 + (0.33 * _currentIndex / _questions.Count);
        BackButton.IsVisible = _currentIndex > 0;
        NextButton.Text = _currentIndex == _questions.Count - 1 ? "Continue" : "Next";
    }

    private void OnBackClicked(object sender, EventArgs e)
    {
        if (_currentIndex > 0)
        {
            QuestionsCarousel.CurrentItem = _questions[_currentIndex - 1];
        }
    }

    private async void OnNextClicked(object sender, EventArgs e)
    {
        // Record answer score
        var question = _questions[_currentIndex];
        if (question.SelectedOption is not null)
        {
            _totalScore += question.SelectedOption.Score;
        }

        if (_currentIndex < _questions.Count - 1)
        {
            QuestionsCarousel.CurrentItem = _questions[_currentIndex + 1];
        }
        else
        {
            // Calculate skill level from score
            var skillLevel = CalculateSkillLevel();
            await _adaptiveUI.SetSkillLevelAsync(skillLevel);
            await Shell.Current.GoToAsync(nameof(GoalSetupPage));
        }
    }

    private UserSkillLevel CalculateSkillLevel()
    {
        // Score ranges: 5-8 = Beginner, 9-14 = Intermediate, 15-20 = Expert
        if (_totalScore <= 8)
            return UserSkillLevel.Beginner;
        if (_totalScore <= 14)
            return UserSkillLevel.Intermediate;
        return UserSkillLevel.Expert;
    }
}

public class QuizQuestion
{
    public string Question { get; init; } = string.Empty;
    public List<QuizOption> Options { get; init; } = new();
    public QuizOption? SelectedOption { get; set; }
}

public class QuizOption
{
    public string Text { get; }
    public int Score { get; }

    public QuizOption(string text, int score)
    {
        Text = text;
        Score = score;
    }
}
