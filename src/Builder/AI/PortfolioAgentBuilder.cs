using AiDotNet;
using AiDotNet.Configuration;
using AiDotNet.Finance.Trading.Environments;
using AiDotNet.Models.Options;
using AiDotNet.ReinforcementLearning.Agents.PPO;
using AiDotNet.ReinforcementLearning.Agents.SAC;
using AiDotNet.Tensors;
using AiDotNet.Tensors.LinearAlgebra;

namespace OoplesFinance.StockIndicators.Builder.AI;

/// <summary>
/// Builder for creating RL portfolio trading agents using AiDotNet's infrastructure.
/// Manages multiple assets with portfolio weight allocation.
/// </summary>
/// <remarks>
/// <para>
/// <b>For Beginners:</b> This creates an AI that learns to allocate capital across
/// multiple stocks/assets. It outputs portfolio weights (e.g., 40% AAPL, 30% MSFT, 30% cash).
/// </para>
/// <para>
/// <b>Example Usage:</b>
/// <code>
/// var result = await PortfolioAgentBuilder.Create()
///     .AddAsset("AAPL", aaplPrices)
///     .AddAsset("MSFT", msftPrices)
///     .AddAsset("GOOGL", googlPrices)
///     .WithInitialCapital(100000)
///     .WithEpisodes(1000)
///     .BuildAsync();
///
/// // Get optimal portfolio weights
/// var weights = result.GetAction(currentState);
/// </code>
/// </para>
/// </remarks>
public sealed class PortfolioAgentBuilder
{
    private readonly List<(string Symbol, double[] Prices)> _assets = new();
    private double _initialCapital = 100000;
    private int _windowSize = 20;
    private double _transactionCost = 0.001;
    private bool _allowShortSelling = false;
    private int _episodes = 1000;
    private int _maxStepsPerEpisode = 0;
    private TradingAgentType _agentType = TradingAgentType.PPO;
    private int? _seed;
    private bool _randomStart = true;
    private int _logFrequency = 100;

    // Agent options
    private List<int> _hiddenLayers = new() { 128, 128 };
    private double _learningRate = 0.0003;
    private double _discountFactor = 0.99;

    // Callbacks
    private Action<RLEpisodeMetrics<double>>? _onEpisodeComplete;
    private Action<RLTrainingSummary<double>>? _onTrainingComplete;

    private PortfolioAgentBuilder() { }

    /// <summary>
    /// Creates a new portfolio agent builder.
    /// </summary>
    public static PortfolioAgentBuilder Create() => new();

    /// <summary>
    /// Adds an asset to the portfolio.
    /// </summary>
    public PortfolioAgentBuilder AddAsset(string symbol, double[] prices)
    {
        _assets.Add((symbol, prices ?? throw new ArgumentNullException(nameof(prices))));
        return this;
    }

    /// <summary>
    /// Adds an asset with decimal prices.
    /// </summary>
    public PortfolioAgentBuilder AddAsset(string symbol, IEnumerable<decimal> prices)
    {
        _assets.Add((symbol, prices?.Select(p => (double)p).ToArray() ?? throw new ArgumentNullException(nameof(prices))));
        return this;
    }

    /// <summary>
    /// Sets the initial capital.
    /// </summary>
    public PortfolioAgentBuilder WithInitialCapital(double capital)
    {
        _initialCapital = capital;
        return this;
    }

    /// <summary>
    /// Sets the observation window size.
    /// </summary>
    public PortfolioAgentBuilder WithWindowSize(int size)
    {
        _windowSize = size;
        return this;
    }

    /// <summary>
    /// Sets the transaction cost rate.
    /// </summary>
    public PortfolioAgentBuilder WithTransactionCost(double cost)
    {
        _transactionCost = cost;
        return this;
    }

    /// <summary>
    /// Enables or disables short selling.
    /// </summary>
    public PortfolioAgentBuilder WithShortSelling(bool allow)
    {
        _allowShortSelling = allow;
        return this;
    }

    /// <summary>
    /// Sets the number of training episodes.
    /// </summary>
    public PortfolioAgentBuilder WithEpisodes(int episodes)
    {
        _episodes = episodes;
        return this;
    }

    /// <summary>
    /// Sets maximum steps per episode.
    /// </summary>
    public PortfolioAgentBuilder WithMaxStepsPerEpisode(int steps)
    {
        _maxStepsPerEpisode = steps;
        return this;
    }

    /// <summary>
    /// Sets the RL agent type.
    /// </summary>
    public PortfolioAgentBuilder WithAgentType(TradingAgentType type)
    {
        _agentType = type;
        return this;
    }

    /// <summary>
    /// Sets the random seed.
    /// </summary>
    public PortfolioAgentBuilder WithSeed(int seed)
    {
        _seed = seed;
        return this;
    }

    /// <summary>
    /// Sets hidden layer sizes.
    /// </summary>
    public PortfolioAgentBuilder WithHiddenLayers(params int[] layers)
    {
        _hiddenLayers = layers.ToList();
        return this;
    }

    /// <summary>
    /// Sets the learning rate.
    /// </summary>
    public PortfolioAgentBuilder WithLearningRate(double rate)
    {
        _learningRate = rate;
        return this;
    }

    /// <summary>
    /// Sets callback for episode completion.
    /// </summary>
    public PortfolioAgentBuilder OnEpisodeComplete(Action<RLEpisodeMetrics<double>> callback)
    {
        _onEpisodeComplete = callback;
        return this;
    }

    /// <summary>
    /// Sets callback for training completion.
    /// </summary>
    public PortfolioAgentBuilder OnTrainingComplete(Action<RLTrainingSummary<double>> callback)
    {
        _onTrainingComplete = callback;
        return this;
    }

    /// <summary>
    /// Builds and trains the portfolio agent.
    /// </summary>
    public async Task<PortfolioAgentResult> BuildAsync(CancellationToken cancellationToken = default)
    {
        if (_assets.Count < 2)
        {
            throw new InvalidOperationException("Portfolio must have at least 2 assets.");
        }

        // Verify all assets have same length
        var minLength = _assets.Min(a => a.Prices.Length);
        if (minLength < _windowSize + 10)
        {
            throw new InvalidOperationException($"All assets must have at least {_windowSize + 10} price bars.");
        }

        // Create market data tensor [time, assets]
        var numAssets = _assets.Count;
        var marketData = new double[minLength * numAssets];
        for (int t = 0; t < minLength; t++)
        {
            for (int a = 0; a < numAssets; a++)
            {
                marketData[t * numAssets + a] = _assets[a].Prices[t];
            }
        }

        var tensor = new Tensor<double>(marketData, [minLength, numAssets]);

        // Create portfolio environment
        var environment = new PortfolioTradingEnvironment<double>(
            marketData: tensor,
            windowSize: _windowSize,
            initialCapital: _initialCapital,
            transactionCost: _transactionCost,
            allowShortSelling: _allowShortSelling,
            randomStart: _randomStart,
            maxEpisodeLength: _maxStepsPerEpisode,
            seed: _seed);

        // Create agent (PPO or SAC work best for continuous portfolio weights)
        var agent = CreateAgent(environment);

        // Configure training
        var rlOptions = new RLTrainingOptions<double>
        {
            Environment = environment,
            Episodes = _episodes,
            MaxStepsPerEpisode = _maxStepsPerEpisode > 0 ? _maxStepsPerEpisode : minLength - _windowSize,
            LogFrequency = _logFrequency,
            OnEpisodeComplete = _onEpisodeComplete,
            OnTrainingComplete = _onTrainingComplete
        };

        // Use AiModelBuilder facade
        var builder = new AiModelBuilder<double, Vector<double>, Vector<double>>()
            .ConfigureReinforcementLearning(rlOptions)
            .ConfigureModel(agent);

        var result = await builder.BuildAsync().ConfigureAwait(false);

        return new PortfolioAgentResult
        {
            Model = result,
            Environment = environment,
            AssetSymbols = _assets.Select(a => a.Symbol).ToList(),
            AgentType = _agentType,
            IsSuccess = result is not null
        };
    }

    private AiDotNet.Interfaces.IRLAgent<double> CreateAgent(PortfolioTradingEnvironment<double> environment)
    {
        int stateSize = environment.ObservationSpaceDimension;
        int actionSize = environment.ActionSpaceSize;

        return _agentType switch
        {
            TradingAgentType.PPO => new PPOAgent<double>(new PPOOptions<double>
            {
                StateSize = stateSize,
                ActionSize = actionSize,
                IsContinuous = true, // Portfolio weights are continuous
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

            _ => new PPOAgent<double>(new PPOOptions<double>
            {
                StateSize = stateSize,
                ActionSize = actionSize,
                IsContinuous = true,
                PolicyLearningRate = _learningRate,
                ValueLearningRate = _learningRate,
                DiscountFactor = _discountFactor,
                PolicyHiddenLayers = _hiddenLayers,
                ValueHiddenLayers = _hiddenLayers,
                Seed = _seed
            })
        };
    }
}

/// <summary>
/// Result from training a portfolio agent.
/// </summary>
public sealed class PortfolioAgentResult
{
    /// <summary>The trained model.</summary>
    public AiDotNet.Models.Results.AiModelResult<double, Vector<double>, Vector<double>>? Model { get; init; }

    /// <summary>The portfolio environment.</summary>
    public PortfolioTradingEnvironment<double>? Environment { get; init; }

    /// <summary>The asset symbols in order.</summary>
    public IReadOnlyList<string> AssetSymbols { get; init; } = Array.Empty<string>();

    /// <summary>The agent type used.</summary>
    public TradingAgentType AgentType { get; init; }

    /// <summary>Whether training succeeded.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets optimal portfolio weights for the current state.
    /// </summary>
    public Vector<double> GetWeights(Vector<double> state)
    {
        if (Model is null)
        {
            throw new InvalidOperationException("Model is not trained.");
        }

        return Model.Predict(state);
    }

    /// <summary>
    /// Gets portfolio allocation as a dictionary.
    /// </summary>
    public Dictionary<string, double> GetAllocation(Vector<double> state)
    {
        var weights = GetWeights(state);
        var allocation = new Dictionary<string, double>();

        for (int i = 0; i < AssetSymbols.Count && i < weights.Length; i++)
        {
            allocation[AssetSymbols[i]] = weights[i];
        }

        return allocation;
    }
}
