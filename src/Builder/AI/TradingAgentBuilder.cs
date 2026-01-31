using AiDotNet;
using AiDotNet.Configuration;
using AiDotNet.Finance.Trading.Environments;
using AiDotNet.Helpers;
using AiDotNet.Models.Options;
using AiDotNet.ReinforcementLearning.Agents.PPO;
using AiDotNet.ReinforcementLearning.Agents.SAC;
using AiDotNet.ReinforcementLearning.Agents.DQN;
using AiDotNet.ReinforcementLearning.Agents.A2C;
using AiDotNet.ReinforcementLearning.Agents.DDPG;
using AiDotNet.Tensors;
using AiDotNet.Tensors.LinearAlgebra;

namespace OoplesFinance.StockIndicators.Builder.AI;

/// <summary>
/// Simplified builder for creating RL trading agents using AiDotNet's infrastructure.
/// Uses the facade pattern via AiModelBuilder.
/// </summary>
/// <remarks>
/// <para>
/// <b>For Beginners:</b> This class makes it easy to create AI trading agents.
/// Just provide your price data and call Build() - the complexity is handled for you.
/// </para>
/// <para>
/// <b>Example Usage:</b>
/// <code>
/// var prices = stockData.Select(d => (double)d.Close).ToArray();
/// var result = await TradingAgentBuilder.Create()
///     .WithPriceData(prices)
///     .WithInitialCapital(100000)
///     .WithAgentType(TradingAgentType.PPO)
///     .WithEpisodes(1000)
///     .BuildAsync();
///
/// // Use the trained agent for predictions
/// var action = result.Model.Predict(currentState);
/// </code>
/// </para>
/// </remarks>
public sealed class TradingAgentBuilder
{
    private double[] _priceData = Array.Empty<double>();
    private double _initialCapital = 100000;
    private int _windowSize = 20;
    private double _transactionCost = 0.001;
    private bool _allowShortSelling = false;
    private int _episodes = 1000;
    private int _maxStepsPerEpisode = 0;
    private TradingAgentType _agentType = TradingAgentType.PPO;
    private int? _seed;
    private double _tradeSize = 1.0;
    private bool _randomStart = true;
    private int _logFrequency = 100;

    // Agent-specific options
    private List<int> _hiddenLayers = new() { 64, 64 };
    private double _learningRate = 0.0003;
    private double _discountFactor = 0.99;

    // Callbacks
    private Action<RLStepMetrics<double>>? _onStepComplete;
    private Action<RLEpisodeMetrics<double>>? _onEpisodeComplete;
    private Action<RLTrainingSummary<double>>? _onTrainingComplete;

    private TradingAgentBuilder() { }

    /// <summary>
    /// Creates a new trading agent builder.
    /// </summary>
    public static TradingAgentBuilder Create() => new();

    /// <summary>
    /// Sets the price data for training.
    /// </summary>
    public TradingAgentBuilder WithPriceData(double[] prices)
    {
        _priceData = prices ?? throw new ArgumentNullException(nameof(prices));
        return this;
    }

    /// <summary>
    /// Sets the price data from decimal values.
    /// </summary>
    public TradingAgentBuilder WithPriceData(IEnumerable<decimal> prices)
    {
        _priceData = prices?.Select(p => (double)p).ToArray() ?? throw new ArgumentNullException(nameof(prices));
        return this;
    }

    /// <summary>
    /// Sets the initial capital for the trading simulation.
    /// </summary>
    public TradingAgentBuilder WithInitialCapital(double capital)
    {
        _initialCapital = capital;
        return this;
    }

    /// <summary>
    /// Sets the observation window size (how many past bars the agent sees).
    /// </summary>
    public TradingAgentBuilder WithWindowSize(int size)
    {
        _windowSize = size;
        return this;
    }

    /// <summary>
    /// Sets the transaction cost rate (e.g., 0.001 = 0.1%).
    /// </summary>
    public TradingAgentBuilder WithTransactionCost(double cost)
    {
        _transactionCost = cost;
        return this;
    }

    /// <summary>
    /// Enables or disables short selling.
    /// </summary>
    public TradingAgentBuilder WithShortSelling(bool allow)
    {
        _allowShortSelling = allow;
        return this;
    }

    /// <summary>
    /// Sets the number of training episodes.
    /// </summary>
    public TradingAgentBuilder WithEpisodes(int episodes)
    {
        _episodes = episodes;
        return this;
    }

    /// <summary>
    /// Sets the maximum steps per episode (0 = use full data).
    /// </summary>
    public TradingAgentBuilder WithMaxStepsPerEpisode(int steps)
    {
        _maxStepsPerEpisode = steps;
        return this;
    }

    /// <summary>
    /// Sets the RL agent type (PPO, SAC, DQN, etc.).
    /// </summary>
    public TradingAgentBuilder WithAgentType(TradingAgentType type)
    {
        _agentType = type;
        return this;
    }

    /// <summary>
    /// Sets the random seed for reproducibility.
    /// </summary>
    public TradingAgentBuilder WithSeed(int seed)
    {
        _seed = seed;
        return this;
    }

    /// <summary>
    /// Sets the trade size (number of shares per trade).
    /// </summary>
    public TradingAgentBuilder WithTradeSize(double size)
    {
        _tradeSize = size;
        return this;
    }

    /// <summary>
    /// Enables or disables random starting points for episodes.
    /// </summary>
    public TradingAgentBuilder WithRandomStart(bool enable)
    {
        _randomStart = enable;
        return this;
    }

    /// <summary>
    /// Sets how often to log progress (0 = no logging).
    /// </summary>
    public TradingAgentBuilder WithLogFrequency(int frequency)
    {
        _logFrequency = frequency;
        return this;
    }

    /// <summary>
    /// Sets the hidden layer sizes for the neural network.
    /// </summary>
    public TradingAgentBuilder WithHiddenLayers(params int[] layers)
    {
        _hiddenLayers = layers.ToList();
        return this;
    }

    /// <summary>
    /// Sets the learning rate.
    /// </summary>
    public TradingAgentBuilder WithLearningRate(double rate)
    {
        _learningRate = rate;
        return this;
    }

    /// <summary>
    /// Sets the discount factor (gamma).
    /// </summary>
    public TradingAgentBuilder WithDiscountFactor(double gamma)
    {
        _discountFactor = gamma;
        return this;
    }

    /// <summary>
    /// Sets the callback for when a step completes.
    /// </summary>
    public TradingAgentBuilder OnStepComplete(Action<RLStepMetrics<double>> callback)
    {
        _onStepComplete = callback;
        return this;
    }

    /// <summary>
    /// Sets the callback for when an episode completes.
    /// </summary>
    public TradingAgentBuilder OnEpisodeComplete(Action<RLEpisodeMetrics<double>> callback)
    {
        _onEpisodeComplete = callback;
        return this;
    }

    /// <summary>
    /// Sets the callback for when training completes.
    /// </summary>
    public TradingAgentBuilder OnTrainingComplete(Action<RLTrainingSummary<double>> callback)
    {
        _onTrainingComplete = callback;
        return this;
    }

    /// <summary>
    /// Builds and trains the trading agent.
    /// </summary>
    public async Task<TradingAgentResult> BuildAsync(CancellationToken cancellationToken = default)
    {
        if (_priceData.Length < _windowSize + 10)
        {
            throw new InvalidOperationException($"Price data must have at least {_windowSize + 10} bars.");
        }

        // Create market data tensor [time, 1 asset]
        var marketData = new Tensor<double>(_priceData, [_priceData.Length, 1]);

        // Create trading environment using AiDotNet's infrastructure
        var environment = new StockTradingEnvironment<double>(
            marketData: marketData,
            windowSize: _windowSize,
            initialCapital: _initialCapital,
            tradeSize: _tradeSize,
            transactionCost: _transactionCost,
            allowShortSelling: _allowShortSelling,
            randomStart: _randomStart,
            maxEpisodeLength: _maxStepsPerEpisode,
            seed: _seed);

        // Create the appropriate agent
        var agent = CreateAgent(environment);

        // Configure RL training options
        var rlOptions = new RLTrainingOptions<double>
        {
            Environment = environment,
            Episodes = _episodes,
            MaxStepsPerEpisode = _maxStepsPerEpisode > 0 ? _maxStepsPerEpisode : _priceData.Length - _windowSize,
            LogFrequency = _logFrequency,
            OnStepComplete = _onStepComplete,
            OnEpisodeComplete = _onEpisodeComplete,
            OnTrainingComplete = _onTrainingComplete
        };

        // Use AiModelBuilder facade
        var builder = new AiModelBuilder<double, Vector<double>, Vector<double>>()
            .ConfigureReinforcementLearning(rlOptions)
            .ConfigureModel(agent);

        var result = await builder.BuildAsync().ConfigureAwait(false);

        return new TradingAgentResult
        {
            Model = result,
            Environment = environment,
            AgentType = _agentType,
            IsSuccess = result is not null
        };
    }

    private AiDotNet.Interfaces.IRLAgent<double> CreateAgent(StockTradingEnvironment<double> environment)
    {
        int stateSize = environment.ObservationSpaceDimension;
        int actionSize = environment.ActionSpaceSize;

        return _agentType switch
        {
            TradingAgentType.PPO => new PPOAgent<double>(new PPOOptions<double>
            {
                StateSize = stateSize,
                ActionSize = actionSize,
                IsContinuous = environment.IsContinuousActionSpace,
                PolicyLearningRate = _learningRate,
                ValueLearningRate = _learningRate,
                DiscountFactor = _discountFactor,
                PolicyHiddenLayers = _hiddenLayers,
                ValueHiddenLayers = _hiddenLayers,
                Seed = _seed
            }),

            TradingAgentType.SAC => new SACAgent<double>(new SACOptions<double>
            {
                StateSize = stateSize,
                ActionSize = actionSize,
                PolicyLearningRate = _learningRate,
                QLearningRate = _learningRate,
                DiscountFactor = _discountFactor,
                PolicyHiddenLayers = _hiddenLayers,
                QHiddenLayers = _hiddenLayers,
                Seed = _seed
            }),

            TradingAgentType.DQN => new DQNAgent<double>(new DQNOptions<double>
            {
                StateSize = stateSize,
                ActionSize = actionSize,
                LearningRate = _learningRate,
                DiscountFactor = _discountFactor,
                HiddenLayers = _hiddenLayers,
                Seed = _seed
            }),

            TradingAgentType.A2C => new A2CAgent<double>(new A2COptions<double>
            {
                StateSize = stateSize,
                ActionSize = actionSize,
                IsContinuous = environment.IsContinuousActionSpace,
                PolicyLearningRate = _learningRate,
                ValueLearningRate = _learningRate,
                DiscountFactor = _discountFactor,
                PolicyHiddenLayers = _hiddenLayers,
                ValueHiddenLayers = _hiddenLayers,
                Seed = _seed
            }),

            TradingAgentType.DDPG => new DDPGAgent<double>(new DDPGOptions<double>
            {
                StateSize = stateSize,
                ActionSize = actionSize,
                ActorLearningRate = _learningRate,
                CriticLearningRate = _learningRate,
                DiscountFactor = _discountFactor,
                ActorHiddenLayers = _hiddenLayers,
                CriticHiddenLayers = _hiddenLayers,
                Seed = _seed
            }),

            _ => throw new NotSupportedException($"Agent type {_agentType} is not supported.")
        };
    }
}

/// <summary>
/// Supported RL agent types for trading.
/// </summary>
public enum TradingAgentType
{
    /// <summary>Proximal Policy Optimization - good balance of stability and performance.</summary>
    PPO,

    /// <summary>Soft Actor-Critic - best for continuous action spaces.</summary>
    SAC,

    /// <summary>Deep Q-Network - classic approach for discrete actions.</summary>
    DQN,

    /// <summary>Advantage Actor-Critic - simpler than PPO.</summary>
    A2C,

    /// <summary>Deep Deterministic Policy Gradient - for continuous control.</summary>
    DDPG
}

/// <summary>
/// Result from training a trading agent.
/// </summary>
public sealed class TradingAgentResult
{
    /// <summary>The trained model from AiModelBuilder.</summary>
    public AiDotNet.Models.Results.AiModelResult<double, Vector<double>, Vector<double>>? Model { get; init; }

    /// <summary>The trading environment used for training.</summary>
    public StockTradingEnvironment<double>? Environment { get; init; }

    /// <summary>The type of agent that was trained.</summary>
    public TradingAgentType AgentType { get; init; }

    /// <summary>Whether training completed successfully.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets a trading action for the given market state.
    /// </summary>
    public Vector<double> GetAction(Vector<double> state)
    {
        if (Model is null)
        {
            throw new InvalidOperationException("Model is not trained.");
        }

        return Model.Predict(state);
    }
}
