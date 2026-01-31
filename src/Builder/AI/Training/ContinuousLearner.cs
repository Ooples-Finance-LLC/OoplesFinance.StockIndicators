namespace OoplesFinance.StockIndicators.Builder.AI.Training;

/// <summary>
/// Manages continuous/online learning for RL agents.
/// Monitors performance, detects concept drift, and triggers retraining.
/// </summary>
public interface IContinuousLearner
{
    /// <summary>Enables continuous learning for an agent.</summary>
    Task EnableAsync(string agentId, ContinuousLearningConfig config, CancellationToken ct = default);

    /// <summary>Disables continuous learning for an agent.</summary>
    Task DisableAsync(string agentId, CancellationToken ct = default);

    /// <summary>Records a live trading result for learning.</summary>
    Task RecordTradeResultAsync(string agentId, TradeResult result, CancellationToken ct = default);

    /// <summary>Gets the learning status for an agent.</summary>
    Task<LearningStatus?> GetStatusAsync(string agentId, CancellationToken ct = default);

    /// <summary>Forces a retraining cycle.</summary>
    Task ForceRetrainAsync(string agentId, CancellationToken ct = default);

    /// <summary>Rolls back to a previous model version.</summary>
    Task RollbackAsync(string agentId, string versionId, CancellationToken ct = default);

    /// <summary>Gets the version history for an agent.</summary>
    Task<IReadOnlyList<ModelVersion>> GetVersionHistoryAsync(string agentId, CancellationToken ct = default);
}

/// <summary>
/// Implementation of continuous learning system.
/// </summary>
public sealed class ContinuousLearner : IContinuousLearner
{
    private readonly Dictionary<string, LearnerState> _learners = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event Action<string, ConceptDriftAlert>? OnConceptDriftDetected;
    public event Action<string, RetrainingEvent>? OnRetrainingTriggered;
    public event Action<string, PerformanceAlert>? OnPerformanceDegraded;

    public async Task EnableAsync(string agentId, ContinuousLearningConfig config, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var state = new LearnerState
            {
                AgentId = agentId,
                Config = config,
                IsEnabled = true,
                EnabledAt = DateTime.UtcNow,
                CurrentVersion = new ModelVersion
                {
                    VersionId = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    Reason = "Initial version"
                }
            };

            _learners[agentId] = state;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DisableAsync(string agentId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_learners.TryGetValue(agentId, out var state))
            {
                state.IsEnabled = false;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RecordTradeResultAsync(string agentId, TradeResult result, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        LearnerState? state = null;
        try
        {
            if (!_learners.TryGetValue(agentId, out state) || !state.IsEnabled)
                return;

            // Add to experience buffer
            state.RecentTrades.Add(result);

            // Keep buffer at configured size
            while (state.RecentTrades.Count > state.Config.ExperienceBufferSize)
            {
                state.RecentTrades.RemoveAt(0);
            }

            // Update running metrics
            UpdateMetrics(state, result);
        }
        finally
        {
            _lock.Release();
        }

        if (state is null) return;

        // Check for concept drift (outside lock)
        await CheckForConceptDriftAsync(agentId, state, ct);

        // Check if retraining is needed
        await CheckForRetrainingAsync(agentId, state, ct);
    }

    public async Task<LearningStatus?> GetStatusAsync(string agentId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_learners.TryGetValue(agentId, out var state))
                return null;

            return new LearningStatus
            {
                AgentId = agentId,
                IsEnabled = state.IsEnabled,
                EnabledAt = state.EnabledAt,
                CurrentVersion = state.CurrentVersion,
                TotalTradesRecorded = state.TotalTrades,
                RecentWinRate = state.RecentWinRate,
                RecentSharpe = state.RecentSharpe,
                DriftScore = state.DriftScore,
                LastRetrainedAt = state.LastRetrainedAt,
                NextScheduledRetrain = state.NextScheduledRetrain
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ForceRetrainAsync(string agentId, CancellationToken ct = default)
    {
        LearnerState? state;

        await _lock.WaitAsync(ct);
        try
        {
            if (!_learners.TryGetValue(agentId, out state) || !state.IsEnabled)
                return;
        }
        finally
        {
            _lock.Release();
        }

        await ExecuteRetrainingAsync(agentId, state, "Manual trigger", ct);
    }

    public async Task RollbackAsync(string agentId, string versionId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_learners.TryGetValue(agentId, out var state))
                return;

            var targetVersion = state.VersionHistory.FirstOrDefault(v => v.VersionId == versionId);
            if (targetVersion is null)
                throw new KeyNotFoundException($"Version {versionId} not found");

            // Create rollback version
            var rollbackVersion = new ModelVersion
            {
                VersionId = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                Reason = $"Rollback to {versionId}",
                PreviousVersionId = state.CurrentVersion.VersionId
            };

            state.VersionHistory.Add(rollbackVersion);
            state.CurrentVersion = rollbackVersion;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<ModelVersion>> GetVersionHistoryAsync(string agentId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_learners.TryGetValue(agentId, out var state))
                return Array.Empty<ModelVersion>();

            return state.VersionHistory.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    private void UpdateMetrics(LearnerState state, TradeResult result)
    {
        state.TotalTrades++;

        if (result.PnL > 0)
            state.WinningTrades++;

        // Calculate recent win rate (last 100 trades)
        var recentTrades = state.RecentTrades.TakeLast(100).ToList();
        state.RecentWinRate = recentTrades.Count > 0
            ? (double)recentTrades.Count(t => t.PnL > 0) / recentTrades.Count
            : 0;

        // Simple Sharpe approximation from recent trades
        if (recentTrades.Count >= 20)
        {
            var returns = recentTrades.Select(t => (double)t.PnL / (double)t.EntryPrice * 100).ToList();
            var mean = returns.Average();
            var std = Math.Sqrt(returns.Average(r => Math.Pow(r - mean, 2)));
            state.RecentSharpe = std > 0 ? mean / std * Math.Sqrt(252) : 0;
        }
    }

    private async Task CheckForConceptDriftAsync(string agentId, LearnerState state, CancellationToken ct)
    {
        if (state.RecentTrades.Count < state.Config.MinTradesForDriftDetection)
            return;

        // Simple drift detection: compare recent performance to baseline
        var recentTrades = state.RecentTrades.TakeLast(50).ToList();
        var olderTrades = state.RecentTrades.SkipLast(50).TakeLast(50).ToList();

        if (olderTrades.Count < 20) return;

        var recentWinRate = (double)recentTrades.Count(t => t.PnL > 0) / recentTrades.Count;
        var olderWinRate = (double)olderTrades.Count(t => t.PnL > 0) / olderTrades.Count;

        var driftScore = Math.Abs(recentWinRate - olderWinRate);

        await _lock.WaitAsync(ct);
        try
        {
            if (_learners.TryGetValue(agentId, out var currentState))
            {
                currentState.DriftScore = driftScore;
            }
        }
        finally
        {
            _lock.Release();
        }

        if (driftScore > state.Config.DriftThreshold)
        {
            OnConceptDriftDetected?.Invoke(agentId, new ConceptDriftAlert
            {
                AgentId = agentId,
                DriftScore = driftScore,
                Threshold = state.Config.DriftThreshold,
                RecentWinRate = recentWinRate,
                BaselineWinRate = olderWinRate,
                DetectedAt = DateTime.UtcNow
            });
        }
    }

    private async Task CheckForRetrainingAsync(string agentId, LearnerState state, CancellationToken ct)
    {
        var shouldRetrain = false;
        var reason = string.Empty;

        // Check performance degradation
        if (state.RecentSharpe < state.Config.MinAcceptableSharpe)
        {
            shouldRetrain = true;
            reason = $"Sharpe ratio ({state.RecentSharpe:F2}) below threshold ({state.Config.MinAcceptableSharpe:F2})";

            OnPerformanceDegraded?.Invoke(agentId, new PerformanceAlert
            {
                AgentId = agentId,
                CurrentSharpe = state.RecentSharpe,
                Threshold = state.Config.MinAcceptableSharpe,
                DetectedAt = DateTime.UtcNow
            });
        }

        // Check concept drift threshold
        if (state.DriftScore > state.Config.DriftThreshold * 1.5)
        {
            shouldRetrain = true;
            reason = $"Significant concept drift detected (score: {state.DriftScore:F2})";
        }

        // Check scheduled retraining
        if (state.NextScheduledRetrain.HasValue && DateTime.UtcNow >= state.NextScheduledRetrain.Value)
        {
            shouldRetrain = true;
            reason = "Scheduled retraining";
        }

        // Check minimum trades since last retrain
        var tradesSinceRetrain = state.TotalTrades - state.TradesAtLastRetrain;
        if (tradesSinceRetrain >= state.Config.RetrainEveryNTrades)
        {
            shouldRetrain = true;
            reason = $"Reached {tradesSinceRetrain} trades since last retrain";
        }

        if (shouldRetrain && state.Config.AutoRetrain)
        {
            await ExecuteRetrainingAsync(agentId, state, reason, ct);
        }
    }

    private async Task ExecuteRetrainingAsync(string agentId, LearnerState state, string reason, CancellationToken ct)
    {
        OnRetrainingTriggered?.Invoke(agentId, new RetrainingEvent
        {
            AgentId = agentId,
            Reason = reason,
            TradesInBuffer = state.RecentTrades.Count,
            TriggeredAt = DateTime.UtcNow
        });

        // In production, this would actually retrain the model
        // For now, we just update the version

        await _lock.WaitAsync(ct);
        try
        {
            if (!_learners.TryGetValue(agentId, out var currentState))
                return;

            var newVersion = new ModelVersion
            {
                VersionId = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                Reason = reason,
                PreviousVersionId = currentState.CurrentVersion.VersionId,
                TradesUsed = currentState.RecentTrades.Count
            };

            currentState.VersionHistory.Add(newVersion);
            currentState.CurrentVersion = newVersion;
            currentState.LastRetrainedAt = DateTime.UtcNow;
            currentState.TradesAtLastRetrain = currentState.TotalTrades;

            if (currentState.Config.ScheduledRetrainIntervalHours > 0)
            {
                currentState.NextScheduledRetrain = DateTime.UtcNow.AddHours(currentState.Config.ScheduledRetrainIntervalHours);
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}

#region Types

public sealed record ContinuousLearningConfig
{
    /// <summary>Whether to automatically retrain when conditions are met.</summary>
    public bool AutoRetrain { get; init; } = true;

    /// <summary>Size of the experience buffer for online learning.</summary>
    public int ExperienceBufferSize { get; init; } = 1000;

    /// <summary>Minimum trades needed to detect drift.</summary>
    public int MinTradesForDriftDetection { get; init; } = 100;

    /// <summary>Concept drift detection threshold (0.0-1.0).</summary>
    public double DriftThreshold { get; init; } = 0.15;

    /// <summary>Minimum acceptable Sharpe ratio before retraining.</summary>
    public double MinAcceptableSharpe { get; init; } = 0.5;

    /// <summary>Number of trades between retraining cycles.</summary>
    public int RetrainEveryNTrades { get; init; } = 500;

    /// <summary>Scheduled retraining interval in hours (0 = disabled).</summary>
    public int ScheduledRetrainIntervalHours { get; init; } = 0;

    /// <summary>Learning rate for online updates.</summary>
    public double OnlineLearningRate { get; init; } = 0.0001;
}

public sealed record TradeResult
{
    public string TradeId { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public DateTime EntryTime { get; init; }
    public DateTime ExitTime { get; init; }
    public decimal EntryPrice { get; init; }
    public decimal ExitPrice { get; init; }
    public int Quantity { get; init; }
    public decimal PnL { get; init; }
    public decimal Commission { get; init; }
}

public sealed record LearningStatus
{
    public string AgentId { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public DateTime EnabledAt { get; init; }
    public ModelVersion CurrentVersion { get; init; } = new();
    public int TotalTradesRecorded { get; init; }
    public double RecentWinRate { get; init; }
    public double RecentSharpe { get; init; }
    public double DriftScore { get; init; }
    public DateTime? LastRetrainedAt { get; init; }
    public DateTime? NextScheduledRetrain { get; init; }
}

public sealed record ModelVersion
{
    public string VersionId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? PreviousVersionId { get; init; }
    public int TradesUsed { get; init; }
    public double? Sharpe { get; init; }
    public double? WinRate { get; init; }
}

public sealed record ConceptDriftAlert
{
    public string AgentId { get; init; } = string.Empty;
    public double DriftScore { get; init; }
    public double Threshold { get; init; }
    public double RecentWinRate { get; init; }
    public double BaselineWinRate { get; init; }
    public DateTime DetectedAt { get; init; }
}

public sealed record PerformanceAlert
{
    public string AgentId { get; init; } = string.Empty;
    public double CurrentSharpe { get; init; }
    public double Threshold { get; init; }
    public DateTime DetectedAt { get; init; }
}

public sealed record RetrainingEvent
{
    public string AgentId { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public int TradesInBuffer { get; init; }
    public DateTime TriggeredAt { get; init; }
}

internal sealed class LearnerState
{
    public string AgentId { get; init; } = string.Empty;
    public ContinuousLearningConfig Config { get; init; } = new();
    public bool IsEnabled { get; set; }
    public DateTime EnabledAt { get; init; }
    public ModelVersion CurrentVersion { get; set; } = new();
    public List<ModelVersion> VersionHistory { get; } = new();
    public List<TradeResult> RecentTrades { get; } = new();
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public double RecentWinRate { get; set; }
    public double RecentSharpe { get; set; }
    public double DriftScore { get; set; }
    public DateTime? LastRetrainedAt { get; set; }
    public DateTime? NextScheduledRetrain { get; set; }
    public int TradesAtLastRetrain { get; set; }
}

#endregion
