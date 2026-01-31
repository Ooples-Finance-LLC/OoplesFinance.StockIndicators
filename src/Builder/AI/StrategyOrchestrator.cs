using System.Collections.Concurrent;
using AiDotNet.Tensors.LinearAlgebra;

namespace OoplesFinance.StockIndicators.Builder.AI;

/// <summary>
/// Orchestrates multiple trading strategies running in parallel.
/// Handles capital allocation, performance tracking, and risk management across strategies.
/// </summary>
/// <remarks>
/// <para>
/// <b>For Beginners:</b> This is like a fund manager that runs multiple investment strategies
/// at once, dividing your money between them and tracking which ones are doing well.
/// </para>
/// <para>
/// <b>Example Usage:</b>
/// <code>
/// var orchestrator = new StrategyOrchestrator(new StrategyOrchestratorOptions
/// {
///     TotalCapital = 100000,
///     MaxStrategies = 5,
///     RebalanceFrequency = TimeSpan.FromDays(1)
/// });
///
/// // Add strategies
/// orchestrator.AddStrategy("Momentum", momentumAgent, 0.3m);
/// orchestrator.AddStrategy("MeanReversion", meanReversionAgent, 0.3m);
/// orchestrator.AddStrategy("MLPredictor", mlAgent, 0.4m);
///
/// // Start orchestration
/// await orchestrator.StartAsync(cancellationToken);
///
/// // Get combined signals
/// var signals = orchestrator.GetAggregatedSignals("AAPL");
/// </code>
/// </para>
/// </remarks>
public sealed class StrategyOrchestrator : IDisposable, IAsyncDisposable
{
    private readonly StrategyOrchestratorOptions _options;
    private readonly ConcurrentDictionary<string, ManagedStrategy> _strategies = new();
    private readonly ConcurrentDictionary<string, StrategyPerformance> _performance = new();
    private readonly SemaphoreSlim _rebalanceLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private Task? _monitoringTask;
    private bool _isRunning;
    private bool _disposed;

    /// <summary>
    /// Creates a new strategy orchestrator.
    /// </summary>
    public StrategyOrchestrator(StrategyOrchestratorOptions? options = null)
    {
        _options = options ?? new StrategyOrchestratorOptions();
    }

    /// <summary>
    /// Gets whether the orchestrator is currently running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Gets the number of active strategies.
    /// </summary>
    public int StrategyCount => _strategies.Count;

    /// <summary>
    /// Gets all strategy names.
    /// </summary>
    public IReadOnlyList<string> StrategyNames => _strategies.Keys.ToList();

    /// <summary>
    /// Adds a strategy with a trained trading agent.
    /// </summary>
    /// <param name="name">Unique strategy name.</param>
    /// <param name="agentResult">Trained trading agent result.</param>
    /// <param name="allocationWeight">Initial capital allocation weight (0-1).</param>
    /// <param name="metadata">Optional strategy metadata.</param>
    public void AddStrategy(
        string name,
        TradingAgentResult agentResult,
        decimal allocationWeight = 0.1m,
        StrategyMetadata? metadata = null)
    {
        if (_strategies.Count >= _options.MaxStrategies)
        {
            throw new InvalidOperationException($"Maximum of {_options.MaxStrategies} strategies allowed.");
        }

        if (allocationWeight < 0 || allocationWeight > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(allocationWeight), "Must be between 0 and 1.");
        }

        var strategy = new ManagedStrategy
        {
            Name = name,
            AgentResult = agentResult,
            AllocationWeight = allocationWeight,
            AllocatedCapital = _options.TotalCapital * (double)allocationWeight,
            Metadata = metadata ?? new StrategyMetadata { Name = name },
            Status = StrategyStatus.Active,
            AddedAt = DateTime.UtcNow
        };

        if (!_strategies.TryAdd(name, strategy))
        {
            throw new InvalidOperationException($"Strategy '{name}' already exists.");
        }

        _performance[name] = new StrategyPerformance { StrategyName = name };
    }

    /// <summary>
    /// Adds a portfolio strategy with a trained portfolio agent.
    /// </summary>
    public void AddPortfolioStrategy(
        string name,
        PortfolioAgentResult agentResult,
        decimal allocationWeight = 0.1m,
        StrategyMetadata? metadata = null)
    {
        if (_strategies.Count >= _options.MaxStrategies)
        {
            throw new InvalidOperationException($"Maximum of {_options.MaxStrategies} strategies allowed.");
        }

        var strategy = new ManagedStrategy
        {
            Name = name,
            PortfolioAgentResult = agentResult,
            AllocationWeight = allocationWeight,
            AllocatedCapital = _options.TotalCapital * (double)allocationWeight,
            Metadata = metadata ?? new StrategyMetadata { Name = name },
            Status = StrategyStatus.Active,
            AddedAt = DateTime.UtcNow,
            IsPortfolioStrategy = true
        };

        if (!_strategies.TryAdd(name, strategy))
        {
            throw new InvalidOperationException($"Strategy '{name}' already exists.");
        }

        _performance[name] = new StrategyPerformance { StrategyName = name };
    }

    /// <summary>
    /// Removes a strategy from the orchestrator.
    /// </summary>
    public bool RemoveStrategy(string name)
    {
        if (_strategies.TryRemove(name, out _))
        {
            _performance.TryRemove(name, out _);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Pauses a specific strategy.
    /// </summary>
    public bool PauseStrategy(string name)
    {
        if (_strategies.TryGetValue(name, out var strategy))
        {
            strategy.Status = StrategyStatus.Paused;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Resumes a paused strategy.
    /// </summary>
    public bool ResumeStrategy(string name)
    {
        if (_strategies.TryGetValue(name, out var strategy))
        {
            strategy.Status = StrategyStatus.Active;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets trading signals from all active strategies for a symbol.
    /// </summary>
    /// <param name="state">Current market state observation.</param>
    /// <returns>Aggregated signal with contributions from each strategy.</returns>
    public AggregatedSignal GetAggregatedSignals(Vector<double> state)
    {
        var contributions = new List<StrategyContribution>();
        var totalWeight = 0.0;
        var weightedSignal = 0.0;

        foreach (var (name, strategy) in _strategies)
        {
            if (strategy.Status != StrategyStatus.Active)
                continue;

            try
            {
                Vector<double> rawAction;

                if (strategy.IsPortfolioStrategy && strategy.PortfolioAgentResult?.Model is not null)
                {
                    rawAction = strategy.PortfolioAgentResult.Model.Predict(state);
                }
                else if (strategy.AgentResult?.Model is not null)
                {
                    rawAction = strategy.AgentResult.Model.Predict(state);
                }
                else
                {
                    continue;
                }

                // Interpret action as signal (-1 to +1)
                var signal = rawAction.Length > 0 ? Math.Tanh(rawAction[0]) : 0;
                var weight = (double)strategy.AllocationWeight;

                contributions.Add(new StrategyContribution
                {
                    StrategyName = name,
                    RawAction = rawAction,
                    Signal = signal,
                    Weight = weight,
                    AllocatedCapital = strategy.AllocatedCapital
                });

                weightedSignal += signal * weight;
                totalWeight += weight;
            }
            catch (Exception ex)
            {
                // Log error but continue with other strategies
                strategy.LastError = ex.Message;
                strategy.ErrorCount++;
            }
        }

        var aggregatedSignal = totalWeight > 0 ? weightedSignal / totalWeight : 0;

        return new AggregatedSignal
        {
            Signal = aggregatedSignal,
            SignalType = ClassifySignal(aggregatedSignal),
            Confidence = CalculateConfidence(contributions),
            Contributions = contributions,
            ActiveStrategyCount = contributions.Count,
            GeneratedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Updates performance metrics for a strategy after a trade.
    /// </summary>
    public void RecordTradeResult(string strategyName, TradeResult result)
    {
        if (!_performance.TryGetValue(strategyName, out var perf))
            return;

        perf.TotalTrades++;
        perf.TotalPnL += result.PnL;

        if (result.PnL > 0)
        {
            perf.WinningTrades++;
            perf.GrossProfit += result.PnL;
        }
        else
        {
            perf.LosingTrades++;
            perf.GrossLoss += Math.Abs(result.PnL);
        }

        perf.LastTradeAt = DateTime.UtcNow;
        UpdatePerformanceMetrics(perf);
    }

    /// <summary>
    /// Gets performance metrics for a specific strategy.
    /// </summary>
    public StrategyPerformance? GetPerformance(string strategyName)
    {
        return _performance.TryGetValue(strategyName, out var perf) ? perf : null;
    }

    /// <summary>
    /// Gets performance metrics for all strategies.
    /// </summary>
    public IReadOnlyList<StrategyPerformance> GetAllPerformance()
    {
        return _performance.Values.ToList();
    }

    /// <summary>
    /// Rebalances capital allocation based on recent performance.
    /// </summary>
    public async Task RebalanceAsync(CancellationToken cancellationToken = default)
    {
        await _rebalanceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_options.AutoRebalance)
                return;

            var strategies = _strategies.Values
                .Where(s => s.Status == StrategyStatus.Active)
                .ToList();

            if (strategies.Count == 0)
                return;

            switch (_options.RebalanceMethod)
            {
                case RebalanceMethod.EqualWeight:
                    RebalanceEqualWeight(strategies);
                    break;

                case RebalanceMethod.PerformanceWeighted:
                    RebalanceByPerformance(strategies);
                    break;

                case RebalanceMethod.RiskParity:
                    RebalanceRiskParity(strategies);
                    break;
            }
        }
        finally
        {
            _rebalanceLock.Release();
        }
    }

    /// <summary>
    /// Starts the orchestrator's background monitoring.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
            return Task.CompletedTask;

        _isRunning = true;
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        _monitoringTask = Task.Run(async () =>
        {
            while (!linkedCts.Token.IsCancellationRequested)
            {
                try
                {
                    // Check for automatic rebalancing
                    if (_options.AutoRebalance)
                    {
                        await RebalanceAsync(linkedCts.Token).ConfigureAwait(false);
                    }

                    // Check strategy health
                    foreach (var (name, strategy) in _strategies)
                    {
                        if (strategy.ErrorCount > _options.MaxErrorsBeforePause)
                        {
                            strategy.Status = StrategyStatus.Paused;
                        }
                    }

                    await Task.Delay(_options.MonitoringInterval, linkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, linkedCts.Token);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the orchestrator.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning)
            return;

        _cts.Cancel();

        if (_monitoringTask is not null)
        {
            try
            {
                await _monitoringTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _isRunning = false;
    }

    private static OrchestrationSignalType ClassifySignal(double signal)
    {
        return signal switch
        {
            > 0.5 => OrchestrationSignalType.StrongBuy,
            > 0.2 => OrchestrationSignalType.Buy,
            < -0.5 => OrchestrationSignalType.StrongSell,
            < -0.2 => OrchestrationSignalType.Sell,
            _ => OrchestrationSignalType.Hold
        };
    }

    private static double CalculateConfidence(List<StrategyContribution> contributions)
    {
        if (contributions.Count == 0)
            return 0;

        // Higher confidence when strategies agree
        var signals = contributions.Select(c => c.Signal).ToList();
        var mean = signals.Average();
        var variance = signals.Sum(s => Math.Pow(s - mean, 2)) / signals.Count;
        var stdDev = Math.Sqrt(variance);

        // Lower std dev = higher agreement = higher confidence
        var agreementScore = Math.Max(0, 1 - stdDev);

        // More strategies = higher confidence
        var coverageScore = Math.Min(1.0, contributions.Count / 3.0);

        return (agreementScore * 0.7 + coverageScore * 0.3);
    }

    private void RebalanceEqualWeight(List<ManagedStrategy> strategies)
    {
        var equalWeight = 1.0m / strategies.Count;
        foreach (var strategy in strategies)
        {
            strategy.AllocationWeight = equalWeight;
            strategy.AllocatedCapital = _options.TotalCapital * (double)equalWeight;
        }
    }

    private void RebalanceByPerformance(List<ManagedStrategy> strategies)
    {
        var performanceScores = new Dictionary<string, double>();
        var totalScore = 0.0;

        foreach (var strategy in strategies)
        {
            if (_performance.TryGetValue(strategy.Name, out var perf))
            {
                // Use Sharpe ratio or win rate as score
                var score = Math.Max(0.1, perf.SharpeRatio > 0 ? perf.SharpeRatio : perf.WinRate);
                performanceScores[strategy.Name] = score;
                totalScore += score;
            }
            else
            {
                performanceScores[strategy.Name] = 1.0;
                totalScore += 1.0;
            }
        }

        foreach (var strategy in strategies)
        {
            var weight = (decimal)(performanceScores[strategy.Name] / totalScore);
            strategy.AllocationWeight = weight;
            strategy.AllocatedCapital = _options.TotalCapital * (double)weight;
        }
    }

    private void RebalanceRiskParity(List<ManagedStrategy> strategies)
    {
        var volatilities = new Dictionary<string, double>();
        var totalInverseVol = 0.0;

        foreach (var strategy in strategies)
        {
            if (_performance.TryGetValue(strategy.Name, out var perf) && perf.Volatility > 0)
            {
                var inverseVol = 1.0 / perf.Volatility;
                volatilities[strategy.Name] = inverseVol;
                totalInverseVol += inverseVol;
            }
            else
            {
                volatilities[strategy.Name] = 1.0;
                totalInverseVol += 1.0;
            }
        }

        foreach (var strategy in strategies)
        {
            var weight = (decimal)(volatilities[strategy.Name] / totalInverseVol);
            strategy.AllocationWeight = weight;
            strategy.AllocatedCapital = _options.TotalCapital * (double)weight;
        }
    }

    private static void UpdatePerformanceMetrics(StrategyPerformance perf)
    {
        perf.WinRate = perf.TotalTrades > 0
            ? (double)perf.WinningTrades / perf.TotalTrades
            : 0;

        perf.ProfitFactor = perf.GrossLoss > 0
            ? perf.GrossProfit / perf.GrossLoss
            : perf.GrossProfit > 0 ? double.MaxValue : 0;

        perf.LastUpdatedAt = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        _cts.Cancel();
        _cts.Dispose();
        _rebalanceLock.Dispose();
        _disposed = true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await StopAsync().ConfigureAwait(false);
        Dispose();
    }
}

#region Types

/// <summary>
/// Options for the strategy orchestrator.
/// </summary>
public sealed class StrategyOrchestratorOptions
{
    /// <summary>Total capital to allocate across strategies.</summary>
    public double TotalCapital { get; set; } = 100000;

    /// <summary>Maximum number of strategies allowed.</summary>
    public int MaxStrategies { get; set; } = 10;

    /// <summary>Whether to automatically rebalance allocations.</summary>
    public bool AutoRebalance { get; set; } = true;

    /// <summary>How often to rebalance.</summary>
    public TimeSpan RebalanceFrequency { get; set; } = TimeSpan.FromDays(1);

    /// <summary>Monitoring interval for health checks.</summary>
    public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Method for rebalancing allocations.</summary>
    public RebalanceMethod RebalanceMethod { get; set; } = RebalanceMethod.PerformanceWeighted;

    /// <summary>Maximum errors before auto-pausing a strategy.</summary>
    public int MaxErrorsBeforePause { get; set; } = 5;
}

/// <summary>
/// Rebalancing method.
/// </summary>
public enum RebalanceMethod
{
    /// <summary>Equal weight to all strategies.</summary>
    EqualWeight,

    /// <summary>Weight by recent performance (Sharpe ratio).</summary>
    PerformanceWeighted,

    /// <summary>Risk parity - inverse volatility weighting.</summary>
    RiskParity
}

/// <summary>
/// Strategy status.
/// </summary>
public enum StrategyStatus
{
    Active,
    Paused,
    Stopped,
    Error
}

/// <summary>
/// Strategy metadata.
/// </summary>
public sealed class StrategyMetadata
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Managed strategy within the orchestrator.
/// </summary>
internal sealed class ManagedStrategy
{
    public string Name { get; set; } = string.Empty;
    public TradingAgentResult? AgentResult { get; set; }
    public PortfolioAgentResult? PortfolioAgentResult { get; set; }
    public bool IsPortfolioStrategy { get; set; }
    public decimal AllocationWeight { get; set; }
    public double AllocatedCapital { get; set; }
    public StrategyMetadata Metadata { get; set; } = new();
    public StrategyStatus Status { get; set; }
    public DateTime AddedAt { get; set; }
    public string? LastError { get; set; }
    public int ErrorCount { get; set; }
}

/// <summary>
/// Performance metrics for a strategy.
/// </summary>
public sealed class StrategyPerformance
{
    public string StrategyName { get; set; } = string.Empty;
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public double TotalPnL { get; set; }
    public double GrossProfit { get; set; }
    public double GrossLoss { get; set; }
    public double WinRate { get; set; }
    public double ProfitFactor { get; set; }
    public double SharpeRatio { get; set; }
    public double Volatility { get; set; }
    public double MaxDrawdown { get; set; }
    public DateTime? LastTradeAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}

/// <summary>
/// Trade result for recording.
/// </summary>
public sealed class TradeResult
{
    public string Symbol { get; set; } = string.Empty;
    public double EntryPrice { get; set; }
    public double ExitPrice { get; set; }
    public double Quantity { get; set; }
    public double PnL { get; set; }
    public DateTime EntryTime { get; set; }
    public DateTime ExitTime { get; set; }
}

/// <summary>
/// Contribution from a single strategy to the aggregated signal.
/// </summary>
public sealed class StrategyContribution
{
    public string StrategyName { get; set; } = string.Empty;
    public Vector<double> RawAction { get; set; } = new Vector<double>(0);
    public double Signal { get; set; }
    public double Weight { get; set; }
    public double AllocatedCapital { get; set; }
}

/// <summary>
/// Aggregated trading signal from all strategies.
/// </summary>
public sealed class AggregatedSignal
{
    public double Signal { get; set; }
    public OrchestrationSignalType SignalType { get; set; }
    public double Confidence { get; set; }
    public IReadOnlyList<StrategyContribution> Contributions { get; set; } = Array.Empty<StrategyContribution>();
    public int ActiveStrategyCount { get; set; }
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Signal type from orchestration.
/// </summary>
public enum OrchestrationSignalType
{
    StrongBuy,
    Buy,
    Hold,
    Sell,
    StrongSell
}

#endregion
