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
/// Hierarchical agent system where a meta-agent allocates capital across specialist agents.
/// Each specialist agent handles a specific asset class (equity, options, forex, crypto, futures).
/// </summary>
/// <remarks>
/// <para>
/// <b>Architecture:</b>
/// <code>
/// ┌─────────────────────────────────────────────────────┐
/// │              MetaAgent (Capital Allocator)          │
/// │         Decides: 40% equity, 30% crypto, 30% forex  │
/// ├──────────┬──────────┬──────────┬──────────┬─────────┤
/// │ EquityAgt│ OptionsAgt│ ForexAgt │ CryptoAgt│FuturesAgt│
/// │  (PPO)   │  (SAC)   │  (PPO)   │  (SAC)   │  (PPO)   │
/// └──────────┴──────────┴──────────┴──────────┴─────────┘
/// </code>
/// </para>
/// <para>
/// <b>Example Usage:</b>
/// <code>
/// var orchestrator = await HierarchicalAgentOrchestrator.CreateBuilder()
///     .WithInitialCapital(100000)
///     .AddEquitySpecialist(equityPrices)
///     .AddCryptoSpecialist(cryptoPrices)
///     .WithMetaAgentEpisodes(500)
///     .WithSpecialistEpisodes(1000)
///     .BuildAsync();
///
/// // Get allocations from meta-agent
/// var allocations = orchestrator.GetCapitalAllocations(currentState);
/// // Execute with specialists
/// var actions = orchestrator.GetSpecialistActions(allocations);
/// </code>
/// </para>
/// </remarks>
public sealed class HierarchicalAgentOrchestrator
{
    private readonly HierarchicalAgentResult _metaAgentResult;
    private readonly Dictionary<AssetClass, SpecialistAgentResult> _specialists;
    private readonly HierarchicalAgentOptions _options;

    internal HierarchicalAgentOrchestrator(
        HierarchicalAgentResult metaAgentResult,
        Dictionary<AssetClass, SpecialistAgentResult> specialists,
        HierarchicalAgentOptions options)
    {
        _metaAgentResult = metaAgentResult;
        _specialists = specialists;
        _options = options;
    }

    /// <summary>
    /// Creates a new hierarchical agent orchestrator builder.
    /// </summary>
    public static HierarchicalAgentBuilder CreateBuilder() => new();

    /// <summary>
    /// Gets capital allocation weights from the meta-agent.
    /// </summary>
    /// <param name="marketState">Current market state observation.</param>
    /// <returns>Dictionary of asset class to allocation weight (0-1).</returns>
    public Dictionary<AssetClass, double> GetCapitalAllocations(Vector<double> marketState)
    {
        if (_metaAgentResult.Model is null)
        {
            throw new InvalidOperationException("Meta-agent model is not trained.");
        }

        var rawWeights = _metaAgentResult.Model.Predict(marketState);
        return NormalizeAllocations(rawWeights);
    }

    /// <summary>
    /// Gets trading actions from specialist agents given capital allocations.
    /// </summary>
    /// <param name="allocations">Capital allocations from meta-agent.</param>
    /// <param name="specialistStates">Current state for each specialist.</param>
    /// <returns>Trading actions from each active specialist.</returns>
    public Dictionary<AssetClass, SpecialistAction> GetSpecialistActions(
        Dictionary<AssetClass, double> allocations,
        Dictionary<AssetClass, Vector<double>> specialistStates)
    {
        var actions = new Dictionary<AssetClass, SpecialistAction>();

        foreach (var (assetClass, allocation) in allocations)
        {
            if (allocation < _options.MinimumAllocationThreshold)
            {
                // Skip specialists with minimal allocation
                continue;
            }

            if (!_specialists.TryGetValue(assetClass, out var specialist) || specialist.Model is null)
            {
                continue;
            }

            if (!specialistStates.TryGetValue(assetClass, out var state))
            {
                continue;
            }

            var rawAction = specialist.Model.Predict(state);
            actions[assetClass] = new SpecialistAction
            {
                AssetClass = assetClass,
                RawAction = rawAction,
                AllocationWeight = allocation,
                EffectiveCapital = _options.TotalCapital * allocation
            };
        }

        return actions;
    }

    /// <summary>
    /// Gets combined portfolio action with risk-adjusted sizing.
    /// </summary>
    public CombinedPortfolioAction GetCombinedAction(
        Vector<double> marketState,
        Dictionary<AssetClass, Vector<double>> specialistStates)
    {
        var allocations = GetCapitalAllocations(marketState);
        var specialistActions = GetSpecialistActions(allocations, specialistStates);

        return new CombinedPortfolioAction
        {
            Allocations = allocations,
            SpecialistActions = specialistActions,
            TotalCapital = _options.TotalCapital,
            GeneratedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gets the list of trained specialist asset classes.
    /// </summary>
    public IReadOnlyList<AssetClass> TrainedSpecialists => _specialists.Keys.ToList();

    /// <summary>
    /// Checks if a specific specialist is available.
    /// </summary>
    public bool HasSpecialist(AssetClass assetClass) => _specialists.ContainsKey(assetClass);

    private Dictionary<AssetClass, double> NormalizeAllocations(Vector<double> rawWeights)
    {
        var allocations = new Dictionary<AssetClass, double>();
        var assetClasses = _specialists.Keys.ToList();

        // Apply softmax to get valid probability distribution
        var maxVal = rawWeights.Max();
        var expValues = new double[rawWeights.Length];
        var expSum = 0.0;

        for (int i = 0; i < rawWeights.Length && i < assetClasses.Count; i++)
        {
            expValues[i] = Math.Exp(rawWeights[i] - maxVal);
            expSum += expValues[i];
        }

        for (int i = 0; i < assetClasses.Count && i < rawWeights.Length; i++)
        {
            allocations[assetClasses[i]] = expSum > 0 ? expValues[i] / expSum : 1.0 / assetClasses.Count;
        }

        return allocations;
    }
}

/// <summary>
/// Builder for creating hierarchical agent orchestrators.
/// </summary>
public sealed class HierarchicalAgentBuilder
{
    private double _totalCapital = 100000;
    private int _metaAgentEpisodes = 500;
    private int _specialistEpisodes = 1000;
    private int _windowSize = 20;
    private double _learningRate = 0.0003;
    private double _discountFactor = 0.99;
    private List<int> _hiddenLayers = new() { 128, 128 };
    private int? _seed;
    private double _minimumAllocationThreshold = 0.05;

    private readonly Dictionary<AssetClass, SpecialistConfig> _specialistConfigs = new();

    // Callbacks
    private Action<AssetClass, RLEpisodeMetrics<double>>? _onSpecialistEpisodeComplete;
    private Action<RLEpisodeMetrics<double>>? _onMetaAgentEpisodeComplete;

    internal HierarchicalAgentBuilder() { }

    /// <summary>
    /// Sets the total capital to allocate.
    /// </summary>
    public HierarchicalAgentBuilder WithTotalCapital(double capital)
    {
        _totalCapital = capital;
        return this;
    }

    /// <summary>
    /// Sets training episodes for the meta-agent.
    /// </summary>
    public HierarchicalAgentBuilder WithMetaAgentEpisodes(int episodes)
    {
        _metaAgentEpisodes = episodes;
        return this;
    }

    /// <summary>
    /// Sets training episodes for specialist agents.
    /// </summary>
    public HierarchicalAgentBuilder WithSpecialistEpisodes(int episodes)
    {
        _specialistEpisodes = episodes;
        return this;
    }

    /// <summary>
    /// Sets the observation window size.
    /// </summary>
    public HierarchicalAgentBuilder WithWindowSize(int size)
    {
        _windowSize = size;
        return this;
    }

    /// <summary>
    /// Sets the learning rate for all agents.
    /// </summary>
    public HierarchicalAgentBuilder WithLearningRate(double rate)
    {
        _learningRate = rate;
        return this;
    }

    /// <summary>
    /// Sets the random seed for reproducibility.
    /// </summary>
    public HierarchicalAgentBuilder WithSeed(int seed)
    {
        _seed = seed;
        return this;
    }

    /// <summary>
    /// Sets minimum allocation threshold below which specialists are skipped.
    /// </summary>
    public HierarchicalAgentBuilder WithMinimumAllocationThreshold(double threshold)
    {
        _minimumAllocationThreshold = threshold;
        return this;
    }

    /// <summary>
    /// Adds an equity specialist agent.
    /// </summary>
    public HierarchicalAgentBuilder AddEquitySpecialist(double[] prices, TradingAgentType agentType = TradingAgentType.PPO)
    {
        _specialistConfigs[AssetClass.Equity] = new SpecialistConfig(prices, agentType);
        return this;
    }

    /// <summary>
    /// Adds an options specialist agent.
    /// </summary>
    public HierarchicalAgentBuilder AddOptionsSpecialist(double[] prices, TradingAgentType agentType = TradingAgentType.SAC)
    {
        _specialistConfigs[AssetClass.Options] = new SpecialistConfig(prices, agentType);
        return this;
    }

    /// <summary>
    /// Adds a forex specialist agent.
    /// </summary>
    public HierarchicalAgentBuilder AddForexSpecialist(double[] prices, TradingAgentType agentType = TradingAgentType.PPO)
    {
        _specialistConfigs[AssetClass.Forex] = new SpecialistConfig(prices, agentType);
        return this;
    }

    /// <summary>
    /// Adds a crypto specialist agent.
    /// </summary>
    public HierarchicalAgentBuilder AddCryptoSpecialist(double[] prices, TradingAgentType agentType = TradingAgentType.SAC)
    {
        _specialistConfigs[AssetClass.Crypto] = new SpecialistConfig(prices, agentType);
        return this;
    }

    /// <summary>
    /// Adds a futures specialist agent.
    /// </summary>
    public HierarchicalAgentBuilder AddFuturesSpecialist(double[] prices, TradingAgentType agentType = TradingAgentType.PPO)
    {
        _specialistConfigs[AssetClass.Futures] = new SpecialistConfig(prices, agentType);
        return this;
    }

    /// <summary>
    /// Sets callback for specialist episode completion.
    /// </summary>
    public HierarchicalAgentBuilder OnSpecialistEpisodeComplete(Action<AssetClass, RLEpisodeMetrics<double>> callback)
    {
        _onSpecialistEpisodeComplete = callback;
        return this;
    }

    /// <summary>
    /// Sets callback for meta-agent episode completion.
    /// </summary>
    public HierarchicalAgentBuilder OnMetaAgentEpisodeComplete(Action<RLEpisodeMetrics<double>> callback)
    {
        _onMetaAgentEpisodeComplete = callback;
        return this;
    }

    /// <summary>
    /// Builds and trains the hierarchical agent system.
    /// </summary>
    public async Task<HierarchicalAgentOrchestrator> BuildAsync(CancellationToken cancellationToken = default)
    {
        if (_specialistConfigs.Count < 2)
        {
            throw new InvalidOperationException("Hierarchical agent requires at least 2 specialists.");
        }

        // Step 1: Train specialist agents in parallel
        var specialistTasks = _specialistConfigs.Select(kvp =>
            TrainSpecialistAsync(kvp.Key, kvp.Value, cancellationToken));

        var specialistResults = await Task.WhenAll(specialistTasks).ConfigureAwait(false);
        var specialists = specialistResults.ToDictionary(r => r.AssetClass, r => r);

        // Step 2: Train meta-agent to allocate between specialists
        var metaAgentResult = await TrainMetaAgentAsync(specialists, cancellationToken).ConfigureAwait(false);

        var options = new HierarchicalAgentOptions
        {
            TotalCapital = _totalCapital,
            MinimumAllocationThreshold = _minimumAllocationThreshold
        };

        return new HierarchicalAgentOrchestrator(metaAgentResult, specialists, options);
    }

    private async Task<SpecialistAgentResult> TrainSpecialistAsync(
        AssetClass assetClass,
        SpecialistConfig config,
        CancellationToken cancellationToken)
    {
        var builder = TradingAgentBuilder.Create()
            .WithPriceData(config.Prices)
            .WithInitialCapital(_totalCapital / _specialistConfigs.Count)
            .WithWindowSize(_windowSize)
            .WithAgentType(config.AgentType)
            .WithEpisodes(_specialistEpisodes)
            .WithLearningRate(_learningRate)
            .WithHiddenLayers(_hiddenLayers.ToArray());

        if (_seed.HasValue)
        {
            builder.WithSeed(_seed.Value + (int)assetClass);
        }

        if (_onSpecialistEpisodeComplete is not null)
        {
            builder.OnEpisodeComplete(info => _onSpecialistEpisodeComplete(assetClass, info));
        }

        var result = await builder.BuildAsync(cancellationToken).ConfigureAwait(false);

        return new SpecialistAgentResult
        {
            AssetClass = assetClass,
            Model = result.Model,
            AgentType = config.AgentType,
            IsSuccess = result.IsSuccess,
            OriginalPrices = config.Prices
        };
    }

    private async Task<HierarchicalAgentResult> TrainMetaAgentAsync(
        Dictionary<AssetClass, SpecialistAgentResult> specialists,
        CancellationToken cancellationToken)
    {
        // Create combined market data for meta-agent training
        // Meta-agent observes aggregate market state and outputs allocation weights
        var minLength = specialists.Values
            .Where(s => s.OriginalPrices.Length > 0)
            .Min(s => s.OriginalPrices.Length);

        var numAssets = specialists.Count;
        var combinedData = new double[minLength * numAssets];

        var assetOrder = specialists.Keys.OrderBy(k => (int)k).ToList();
        for (int t = 0; t < minLength; t++)
        {
            for (int a = 0; a < assetOrder.Count; a++)
            {
                var specialist = specialists[assetOrder[a]];
                if (specialist.OriginalPrices.Length > t)
                {
                    combinedData[t * numAssets + a] = specialist.OriginalPrices[t];
                }
            }
        }

        var tensor = new Tensor<double>(combinedData, [minLength, numAssets]);

        // Use PortfolioTradingEnvironment for meta-agent (outputs allocation weights)
        var environment = new PortfolioTradingEnvironment<double>(
            marketData: tensor,
            windowSize: _windowSize,
            initialCapital: _totalCapital,
            transactionCost: 0.001,
            allowShortSelling: false,
            randomStart: true,
            seed: _seed);

        // Meta-agent uses PPO for stable learning
        var agent = new PPOAgent<double>(new PPOOptions<double>
        {
            StateSize = environment.ObservationSpaceDimension,
            ActionSize = environment.ActionSpaceSize,
            IsContinuous = true,
            PolicyLearningRate = _learningRate,
            ValueLearningRate = _learningRate,
            DiscountFactor = _discountFactor,
            PolicyHiddenLayers = _hiddenLayers,
            ValueHiddenLayers = _hiddenLayers,
            Seed = _seed
        });

        var rlOptions = new RLTrainingOptions<double>
        {
            Environment = environment,
            Episodes = _metaAgentEpisodes,
            MaxStepsPerEpisode = minLength - _windowSize,
            LogFrequency = 50,
            OnEpisodeComplete = _onMetaAgentEpisodeComplete
        };

        var builder = new AiModelBuilder<double, Vector<double>, Vector<double>>()
            .ConfigureReinforcementLearning(rlOptions)
            .ConfigureModel(agent);

        var result = await builder.BuildAsync().ConfigureAwait(false);

        return new HierarchicalAgentResult
        {
            Model = result,
            Environment = environment,
            AssetClasses = assetOrder,
            IsSuccess = result is not null
        };
    }

    private sealed record SpecialistConfig(double[] Prices, TradingAgentType AgentType);
}

#region Types

/// <summary>
/// Asset classes supported by the hierarchical agent system.
/// </summary>
public enum AssetClass
{
    Equity = 0,
    Options = 1,
    Forex = 2,
    Crypto = 3,
    Futures = 4
}

/// <summary>
/// Options for the hierarchical agent orchestrator.
/// </summary>
public sealed class HierarchicalAgentOptions
{
    public double TotalCapital { get; init; }
    public double MinimumAllocationThreshold { get; init; }
}

/// <summary>
/// Result from training a specialist agent.
/// </summary>
public sealed class SpecialistAgentResult
{
    public AssetClass AssetClass { get; init; }
    public AiDotNet.Models.Results.AiModelResult<double, Vector<double>, Vector<double>>? Model { get; init; }
    public TradingAgentType AgentType { get; init; }
    public bool IsSuccess { get; init; }
    public double[] OriginalPrices { get; init; } = Array.Empty<double>();
}

/// <summary>
/// Result from training the hierarchical meta-agent.
/// </summary>
public sealed class HierarchicalAgentResult
{
    public AiDotNet.Models.Results.AiModelResult<double, Vector<double>, Vector<double>>? Model { get; init; }
    public PortfolioTradingEnvironment<double>? Environment { get; init; }
    public IReadOnlyList<AssetClass> AssetClasses { get; init; } = Array.Empty<AssetClass>();
    public bool IsSuccess { get; init; }
}

/// <summary>
/// Action from a specialist agent with allocation context.
/// </summary>
public sealed class SpecialistAction
{
    public AssetClass AssetClass { get; init; }
    public Vector<double> RawAction { get; init; } = new Vector<double>(0);
    public double AllocationWeight { get; init; }
    public double EffectiveCapital { get; init; }
}

/// <summary>
/// Combined portfolio action from hierarchical system.
/// </summary>
public sealed class CombinedPortfolioAction
{
    public Dictionary<AssetClass, double> Allocations { get; init; } = new();
    public Dictionary<AssetClass, SpecialistAction> SpecialistActions { get; init; } = new();
    public double TotalCapital { get; init; }
    public DateTime GeneratedAt { get; init; }
}

#endregion
