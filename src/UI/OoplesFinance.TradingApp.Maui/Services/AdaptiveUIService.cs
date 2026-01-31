namespace OoplesFinance.TradingApp.Maui.Services;

/// <summary>
/// Service that manages adaptive UI complexity based on user skill level.
/// Handles mode switching, feature unlocking, and UI customization.
/// </summary>
public interface IAdaptiveUIService
{
    /// <summary>Gets the current skill level.</summary>
    UserSkillLevel CurrentSkillLevel { get; }

    /// <summary>Gets the current UI mode.</summary>
    UIMode CurrentMode { get; }

    /// <summary>Gets whether a specific feature is unlocked.</summary>
    bool IsFeatureUnlocked(Feature feature);

    /// <summary>Gets the list of all unlocked features.</summary>
    IReadOnlyList<Feature> UnlockedFeatures { get; }

    /// <summary>Gets the list of unlocked achievements.</summary>
    IReadOnlyList<Achievement> UnlockedAchievements { get; }

    /// <summary>Sets the UI mode.</summary>
    Task SetModeAsync(UIMode mode);

    /// <summary>Sets skill level from onboarding questionnaire.</summary>
    Task SetSkillLevelAsync(UserSkillLevel level);

    /// <summary>Records a user action for achievement tracking.</summary>
    Task RecordActionAsync(UserAction action);

    /// <summary>Unlocks a feature manually (with warning for beginners).</summary>
    Task<bool> RequestFeatureUnlockAsync(Feature feature);

    /// <summary>Event raised when mode changes.</summary>
    event Action<UIMode>? OnModeChanged;

    /// <summary>Event raised when a feature is unlocked.</summary>
    event Action<Feature>? OnFeatureUnlocked;

    /// <summary>Event raised when an achievement is earned.</summary>
    event Action<Achievement>? OnAchievementEarned;
}

/// <summary>
/// Implementation of the adaptive UI service.
/// </summary>
public class AdaptiveUIService : IAdaptiveUIService
{
    private readonly ISettingsService _settings;
    private UserSkillLevel _skillLevel = UserSkillLevel.Beginner;
    private UIMode _currentMode = UIMode.Beginner;
    private readonly HashSet<Feature> _unlockedFeatures = new();
    private readonly HashSet<Achievement> _achievements = new();
    private readonly Dictionary<UserAction, int> _actionCounts = new();

    public event Action<UIMode>? OnModeChanged;
    public event Action<Feature>? OnFeatureUnlocked;
    public event Action<Achievement>? OnAchievementEarned;

    public AdaptiveUIService(ISettingsService settings)
    {
        _settings = settings;
        InitializeAsync().ConfigureAwait(false);
    }

    public UserSkillLevel CurrentSkillLevel => _skillLevel;
    public UIMode CurrentMode => _currentMode;
    public IReadOnlyList<Feature> UnlockedFeatures => _unlockedFeatures.ToList();
    public IReadOnlyList<Achievement> UnlockedAchievements => _achievements.ToList();

    public bool IsFeatureUnlocked(Feature feature)
    {
        // Beginners have limited features
        if (_currentMode == UIMode.Beginner)
        {
            return BeginnerFeatures.Contains(feature) || _unlockedFeatures.Contains(feature);
        }

        // Intermediate has most features
        if (_currentMode == UIMode.Intermediate)
        {
            return !ExpertOnlyFeatures.Contains(feature) || _unlockedFeatures.Contains(feature);
        }

        // Expert has all features
        return true;
    }

    public async Task SetModeAsync(UIMode mode)
    {
        if (_currentMode == mode) return;

        var previousMode = _currentMode;
        _currentMode = mode;

        await SaveSettingsAsync();
        OnModeChanged?.Invoke(mode);
    }

    public async Task SetSkillLevelAsync(UserSkillLevel level)
    {
        _skillLevel = level;

        // Auto-set UI mode based on skill level
        _currentMode = level switch
        {
            UserSkillLevel.Beginner => UIMode.Beginner,
            UserSkillLevel.Intermediate => UIMode.Intermediate,
            UserSkillLevel.Expert => UIMode.Expert,
            _ => UIMode.Beginner
        };

        // Unlock features appropriate for skill level
        if (level >= UserSkillLevel.Intermediate)
        {
            foreach (var feature in IntermediateFeatures)
            {
                _unlockedFeatures.Add(feature);
            }
        }

        if (level >= UserSkillLevel.Expert)
        {
            foreach (var feature in ExpertOnlyFeatures)
            {
                _unlockedFeatures.Add(feature);
            }
        }

        await SaveSettingsAsync();
        OnModeChanged?.Invoke(_currentMode);
    }

    public async Task RecordActionAsync(UserAction action)
    {
        if (!_actionCounts.ContainsKey(action))
        {
            _actionCounts[action] = 0;
        }
        _actionCounts[action]++;

        // Check for achievement unlocks
        await CheckAchievementsAsync(action);

        await SaveSettingsAsync();
    }

    public async Task<bool> RequestFeatureUnlockAsync(Feature feature)
    {
        if (_unlockedFeatures.Contains(feature))
        {
            return true; // Already unlocked
        }

        // Check if user has met requirements
        var requirements = GetFeatureRequirements(feature);
        if (requirements is not null && !MeetsRequirements(requirements))
        {
            return false;
        }

        _unlockedFeatures.Add(feature);
        await SaveSettingsAsync();
        OnFeatureUnlocked?.Invoke(feature);

        return true;
    }

    private async Task CheckAchievementsAsync(UserAction action)
    {
        var newAchievements = new List<Achievement>();

        // First Trade achievement
        if (action == UserAction.PlacedTrade && !_achievements.Contains(Achievement.FirstTrade))
        {
            newAchievements.Add(Achievement.FirstTrade);
            _unlockedFeatures.Add(Feature.WatchlistCustomization);
        }

        // 10 Trades achievement
        if (_actionCounts.GetValueOrDefault(UserAction.PlacedTrade, 0) >= 10 &&
            !_achievements.Contains(Achievement.TenTrades))
        {
            newAchievements.Add(Achievement.TenTrades);
            _unlockedFeatures.Add(Feature.LimitOrders);
        }

        // First Profit achievement
        if (action == UserAction.ProfitableTrade && !_achievements.Contains(Achievement.FirstProfit))
        {
            newAchievements.Add(Achievement.FirstProfit);
            _unlockedFeatures.Add(Feature.AdvancedOrders);
        }

        // Completed Options Quiz
        if (action == UserAction.CompletedOptionsQuiz && !_achievements.Contains(Achievement.OptionsQuizComplete))
        {
            newAchievements.Add(Achievement.OptionsQuizComplete);
            _unlockedFeatures.Add(Feature.OptionsTrading);
        }

        // 30 Day Streak
        if (action == UserAction.DailyLogin &&
            _actionCounts.GetValueOrDefault(UserAction.DailyLogin, 0) >= 30 &&
            !_achievements.Contains(Achievement.ThirtyDayStreak))
        {
            newAchievements.Add(Achievement.ThirtyDayStreak);
            _unlockedFeatures.Add(Feature.MarginTrading);
        }

        // Add achievements and trigger events
        foreach (var achievement in newAchievements)
        {
            _achievements.Add(achievement);
            OnAchievementEarned?.Invoke(achievement);
        }
    }

    private FeatureRequirements? GetFeatureRequirements(Feature feature) => feature switch
    {
        Feature.LimitOrders => new FeatureRequirements { MinTrades = 10 },
        Feature.AdvancedOrders => new FeatureRequirements { MinTrades = 25, RequiredAchievements = new[] { Achievement.FirstProfit } },
        Feature.OptionsTrading => new FeatureRequirements { RequiredAchievements = new[] { Achievement.OptionsQuizComplete } },
        Feature.MarginTrading => new FeatureRequirements { MinTrades = 50, RequiredAchievements = new[] { Achievement.ThirtyDayStreak } },
        Feature.AdvancedCharting => new FeatureRequirements { MinTrades = 20 },
        Feature.AITrading => new FeatureRequirements { MinTrades = 100, RequiredAchievements = new[] { Achievement.TenTrades } },
        _ => null
    };

    private bool MeetsRequirements(FeatureRequirements requirements)
    {
        var totalTrades = _actionCounts.GetValueOrDefault(UserAction.PlacedTrade, 0);

        if (requirements.MinTrades.HasValue && totalTrades < requirements.MinTrades.Value)
            return false;

        if (requirements.RequiredAchievements is not null)
        {
            foreach (var req in requirements.RequiredAchievements)
            {
                if (!_achievements.Contains(req))
                    return false;
            }
        }

        return true;
    }

    private async Task InitializeAsync()
    {
        // Load saved settings
        var savedMode = Preferences.Get("ui_mode", "Beginner");
        var savedLevel = Preferences.Get("skill_level", "Beginner");

        _currentMode = Enum.TryParse<UIMode>(savedMode, out var mode) ? mode : UIMode.Beginner;
        _skillLevel = Enum.TryParse<UserSkillLevel>(savedLevel, out var level) ? level : UserSkillLevel.Beginner;

        // Load unlocked features
        var featuresJson = Preferences.Get("unlocked_features", "[]");
        try
        {
            var features = System.Text.Json.JsonSerializer.Deserialize<List<string>>(featuresJson) ?? new();
            foreach (var f in features)
            {
                if (Enum.TryParse<Feature>(f, out var feature))
                {
                    _unlockedFeatures.Add(feature);
                }
            }
        }
        catch { }

        // Load achievements
        var achievementsJson = Preferences.Get("achievements", "[]");
        try
        {
            var achievements = System.Text.Json.JsonSerializer.Deserialize<List<string>>(achievementsJson) ?? new();
            foreach (var a in achievements)
            {
                if (Enum.TryParse<Achievement>(a, out var achievement))
                {
                    _achievements.Add(achievement);
                }
            }
        }
        catch { }

        // Load action counts
        var countsJson = Preferences.Get("action_counts", "{}");
        try
        {
            var counts = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(countsJson) ?? new();
            foreach (var kvp in counts)
            {
                if (Enum.TryParse<UserAction>(kvp.Key, out var action))
                {
                    _actionCounts[action] = kvp.Value;
                }
            }
        }
        catch { }
    }

    private async Task SaveSettingsAsync()
    {
        Preferences.Set("ui_mode", _currentMode.ToString());
        Preferences.Set("skill_level", _skillLevel.ToString());

        var featuresJson = System.Text.Json.JsonSerializer.Serialize(
            _unlockedFeatures.Select(f => f.ToString()).ToList());
        Preferences.Set("unlocked_features", featuresJson);

        var achievementsJson = System.Text.Json.JsonSerializer.Serialize(
            _achievements.Select(a => a.ToString()).ToList());
        Preferences.Set("achievements", achievementsJson);

        var countsJson = System.Text.Json.JsonSerializer.Serialize(
            _actionCounts.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value));
        Preferences.Set("action_counts", countsJson);
    }

    // Feature sets for each mode
    private static readonly HashSet<Feature> BeginnerFeatures = new()
    {
        Feature.BasicDashboard,
        Feature.SimpleOrderEntry,
        Feature.Watchlist,
        Feature.BasicPositions,
        Feature.PriceAlerts
    };

    private static readonly HashSet<Feature> IntermediateFeatures = new()
    {
        Feature.WatchlistCustomization,
        Feature.LimitOrders,
        Feature.AdvancedOrders,
        Feature.BasicCharting,
        Feature.PositionDetails,
        Feature.OrderHistory
    };

    private static readonly HashSet<Feature> ExpertOnlyFeatures = new()
    {
        Feature.AdvancedCharting,
        Feature.OptionsTrading,
        Feature.MarginTrading,
        Feature.AITrading,
        Feature.StrategyBuilder,
        Feature.MultipleLayouts,
        Feature.Hotkeys
    };
}

#region Types

/// <summary>
/// User skill levels from onboarding questionnaire.
/// </summary>
public enum UserSkillLevel
{
    Beginner,
    Intermediate,
    Expert
}

/// <summary>
/// UI complexity modes.
/// </summary>
public enum UIMode
{
    /// <summary>Large buttons, minimal data, guided actions.</summary>
    Beginner,

    /// <summary>Standard trading UI, most features available.</summary>
    Intermediate,

    /// <summary>Full trading terminal, all features, hotkeys.</summary>
    Expert
}

/// <summary>
/// Features that can be unlocked progressively.
/// </summary>
public enum Feature
{
    // Beginner features (always available)
    BasicDashboard,
    SimpleOrderEntry,
    Watchlist,
    BasicPositions,
    PriceAlerts,

    // Intermediate features (unlocked with experience)
    WatchlistCustomization,
    LimitOrders,
    AdvancedOrders,
    BasicCharting,
    PositionDetails,
    OrderHistory,

    // Expert features (require achievements or manual unlock)
    AdvancedCharting,
    OptionsTrading,
    MarginTrading,
    AITrading,
    StrategyBuilder,
    MultipleLayouts,
    Hotkeys
}

/// <summary>
/// Achievements that unlock features.
/// </summary>
public enum Achievement
{
    FirstTrade,
    TenTrades,
    FirstProfit,
    HundredTrades,
    ThousandProfit,
    OptionsQuizComplete,
    ThirtyDayStreak,
    BacktestComplete,
    AIAgentTrained
}

/// <summary>
/// User actions to track for achievements.
/// </summary>
public enum UserAction
{
    PlacedTrade,
    ProfitableTrade,
    LosingTrade,
    ViewedChart,
    SetAlert,
    CompletedOptionsQuiz,
    DailyLogin,
    RanBacktest,
    TrainedAgent
}

/// <summary>
/// Requirements for unlocking a feature.
/// </summary>
public sealed class FeatureRequirements
{
    public int? MinTrades { get; init; }
    public Achievement[]? RequiredAchievements { get; init; }
}

#endregion
