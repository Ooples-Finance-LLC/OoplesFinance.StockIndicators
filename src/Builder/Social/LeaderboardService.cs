namespace OoplesFinance.StockIndicators.Builder.Social;

/// <summary>
/// Service for managing trader/strategy performance leaderboards.
/// Provides rankings based on returns, Sharpe ratio, consistency, and drawdown.
/// </summary>
public interface ILeaderboardService
{
    /// <summary>Gets the top traders for a given time period.</summary>
    Task<IReadOnlyList<LeaderboardEntry>> GetTopTradersAsync(
        LeaderboardPeriod period,
        LeaderboardMetric metric = LeaderboardMetric.Returns,
        int limit = 50,
        CancellationToken ct = default);

    /// <summary>Gets the top strategies for a given time period.</summary>
    Task<IReadOnlyList<StrategyLeaderboardEntry>> GetTopStrategiesAsync(
        LeaderboardPeriod period,
        LeaderboardMetric metric = LeaderboardMetric.SharpeRatio,
        int limit = 50,
        CancellationToken ct = default);

    /// <summary>Gets a specific trader's ranking.</summary>
    Task<LeaderboardEntry?> GetTraderRankingAsync(string traderId, LeaderboardPeriod period, CancellationToken ct = default);

    /// <summary>Gets a trader's public profile.</summary>
    Task<TraderProfile?> GetTraderProfileAsync(string traderId, CancellationToken ct = default);

    /// <summary>Updates performance metrics for a trader.</summary>
    Task UpdateTraderMetricsAsync(string traderId, PerformanceUpdate update, CancellationToken ct = default);
}

/// <summary>
/// Implementation of the leaderboard service.
/// </summary>
public sealed class LeaderboardService : ILeaderboardService
{
    private readonly Dictionary<string, TraderMetrics> _traderMetrics = new();
    private readonly Dictionary<string, StrategyMetrics> _strategyMetrics = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<IReadOnlyList<LeaderboardEntry>> GetTopTradersAsync(
        LeaderboardPeriod period,
        LeaderboardMetric metric = LeaderboardMetric.Returns,
        int limit = 50,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var traders = _traderMetrics.Values
                .Where(t => t.IsPublic && t.Periods.ContainsKey(period))
                .Select(t => new LeaderboardEntry
                {
                    TraderId = t.TraderId,
                    DisplayName = t.DisplayName,
                    AvatarUrl = t.AvatarUrl,
                    Returns = t.Periods[period].Returns,
                    SharpeRatio = t.Periods[period].SharpeRatio,
                    MaxDrawdown = t.Periods[period].MaxDrawdown,
                    WinRate = t.Periods[period].WinRate,
                    TotalTrades = t.Periods[period].TotalTrades,
                    FollowerCount = t.FollowerCount,
                    Period = period
                });

            var sorted = metric switch
            {
                LeaderboardMetric.Returns => traders.OrderByDescending(t => t.Returns),
                LeaderboardMetric.SharpeRatio => traders.OrderByDescending(t => t.SharpeRatio),
                LeaderboardMetric.Consistency => traders.OrderByDescending(t => t.WinRate),
                LeaderboardMetric.MinDrawdown => traders.OrderBy(t => Math.Abs(t.MaxDrawdown)),
                _ => traders.OrderByDescending(t => t.Returns)
            };

            var ranked = sorted.Take(limit).ToList();
            for (int i = 0; i < ranked.Count; i++)
            {
                ranked[i] = ranked[i] with { Rank = i + 1 };
            }

            return ranked;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<StrategyLeaderboardEntry>> GetTopStrategiesAsync(
        LeaderboardPeriod period,
        LeaderboardMetric metric = LeaderboardMetric.SharpeRatio,
        int limit = 50,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var strategies = _strategyMetrics.Values
                .Where(s => s.IsPublic && s.Periods.ContainsKey(period))
                .Select(s => new StrategyLeaderboardEntry
                {
                    StrategyId = s.StrategyId,
                    StrategyName = s.StrategyName,
                    CreatorId = s.CreatorId,
                    CreatorName = s.CreatorName,
                    Returns = s.Periods[period].Returns,
                    SharpeRatio = s.Periods[period].SharpeRatio,
                    MaxDrawdown = s.Periods[period].MaxDrawdown,
                    WinRate = s.Periods[period].WinRate,
                    TotalTrades = s.Periods[period].TotalTrades,
                    SubscriberCount = s.SubscriberCount,
                    Price = s.MonthlyPrice,
                    Period = period
                });

            var sorted = metric switch
            {
                LeaderboardMetric.Returns => strategies.OrderByDescending(s => s.Returns),
                LeaderboardMetric.SharpeRatio => strategies.OrderByDescending(s => s.SharpeRatio),
                LeaderboardMetric.Consistency => strategies.OrderByDescending(s => s.WinRate),
                LeaderboardMetric.MinDrawdown => strategies.OrderBy(s => Math.Abs(s.MaxDrawdown)),
                _ => strategies.OrderByDescending(s => s.SharpeRatio)
            };

            var ranked = sorted.Take(limit).ToList();
            for (int i = 0; i < ranked.Count; i++)
            {
                ranked[i] = ranked[i] with { Rank = i + 1 };
            }

            return ranked;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<LeaderboardEntry?> GetTraderRankingAsync(string traderId, LeaderboardPeriod period, CancellationToken ct = default)
    {
        var allTraders = await GetTopTradersAsync(period, LeaderboardMetric.Returns, int.MaxValue, ct);
        return allTraders.FirstOrDefault(t => t.TraderId == traderId);
    }

    public async Task<TraderProfile?> GetTraderProfileAsync(string traderId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_traderMetrics.TryGetValue(traderId, out var metrics))
                return null;

            return new TraderProfile
            {
                TraderId = metrics.TraderId,
                DisplayName = metrics.DisplayName,
                Bio = metrics.Bio,
                AvatarUrl = metrics.AvatarUrl,
                JoinedAt = metrics.JoinedAt,
                FollowerCount = metrics.FollowerCount,
                IsVerified = metrics.IsVerified,
                TradingStyle = metrics.TradingStyle,
                PreferredAssets = metrics.PreferredAssets,
                PerformanceHistory = metrics.Periods.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new PeriodPerformance
                    {
                        Returns = kvp.Value.Returns,
                        SharpeRatio = kvp.Value.SharpeRatio,
                        MaxDrawdown = kvp.Value.MaxDrawdown,
                        WinRate = kvp.Value.WinRate,
                        TotalTrades = kvp.Value.TotalTrades
                    })
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateTraderMetricsAsync(string traderId, PerformanceUpdate update, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_traderMetrics.TryGetValue(traderId, out var metrics))
            {
                metrics = new TraderMetrics
                {
                    TraderId = traderId,
                    DisplayName = update.DisplayName ?? "Anonymous",
                    JoinedAt = DateTime.UtcNow
                };
                _traderMetrics[traderId] = metrics;
            }

            // Update the relevant period
            var period = DeterminePeriod(update.Timestamp);
            if (!metrics.Periods.ContainsKey(period))
            {
                metrics.Periods[period] = new PeriodStats();
            }

            var stats = metrics.Periods[period];
            stats.Returns = update.TotalReturns;
            stats.SharpeRatio = update.SharpeRatio;
            stats.MaxDrawdown = update.MaxDrawdown;
            stats.TotalTrades = update.TotalTrades;
            stats.WinRate = update.TotalTrades > 0 ? (double)update.WinningTrades / update.TotalTrades : 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static LeaderboardPeriod DeterminePeriod(DateTime timestamp)
    {
        var age = DateTime.UtcNow - timestamp;
        if (age.TotalDays <= 1) return LeaderboardPeriod.Daily;
        if (age.TotalDays <= 7) return LeaderboardPeriod.Weekly;
        if (age.TotalDays <= 30) return LeaderboardPeriod.Monthly;
        return LeaderboardPeriod.AllTime;
    }
}

#region Types

public enum LeaderboardPeriod
{
    Daily,
    Weekly,
    Monthly,
    Yearly,
    AllTime
}

public enum LeaderboardMetric
{
    Returns,
    SharpeRatio,
    Consistency,
    MinDrawdown
}

public sealed record LeaderboardEntry
{
    public int Rank { get; init; }
    public string TraderId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public double Returns { get; init; }
    public double SharpeRatio { get; init; }
    public double MaxDrawdown { get; init; }
    public double WinRate { get; init; }
    public int TotalTrades { get; init; }
    public int FollowerCount { get; init; }
    public LeaderboardPeriod Period { get; init; }
}

public sealed record StrategyLeaderboardEntry
{
    public int Rank { get; init; }
    public string StrategyId { get; init; } = string.Empty;
    public string StrategyName { get; init; } = string.Empty;
    public string CreatorId { get; init; } = string.Empty;
    public string CreatorName { get; init; } = string.Empty;
    public double Returns { get; init; }
    public double SharpeRatio { get; init; }
    public double MaxDrawdown { get; init; }
    public double WinRate { get; init; }
    public int TotalTrades { get; init; }
    public int SubscriberCount { get; init; }
    public decimal Price { get; init; }
    public LeaderboardPeriod Period { get; init; }
}

public sealed record TraderProfile
{
    public string TraderId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Bio { get; init; }
    public string? AvatarUrl { get; init; }
    public DateTime JoinedAt { get; init; }
    public int FollowerCount { get; init; }
    public bool IsVerified { get; init; }
    public string? TradingStyle { get; init; }
    public IReadOnlyList<string> PreferredAssets { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<LeaderboardPeriod, PeriodPerformance> PerformanceHistory { get; init; }
        = new Dictionary<LeaderboardPeriod, PeriodPerformance>();
}

public sealed record PeriodPerformance
{
    public double Returns { get; init; }
    public double SharpeRatio { get; init; }
    public double MaxDrawdown { get; init; }
    public double WinRate { get; init; }
    public int TotalTrades { get; init; }
}

public sealed record PerformanceUpdate
{
    public string? DisplayName { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public double TotalReturns { get; init; }
    public double SharpeRatio { get; init; }
    public double MaxDrawdown { get; init; }
    public int TotalTrades { get; init; }
    public int WinningTrades { get; init; }
}

internal sealed class TraderMetrics
{
    public string TraderId { get; init; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime JoinedAt { get; init; }
    public int FollowerCount { get; set; }
    public bool IsVerified { get; set; }
    public bool IsPublic { get; set; } = true;
    public string? TradingStyle { get; set; }
    public List<string> PreferredAssets { get; } = new();
    public Dictionary<LeaderboardPeriod, PeriodStats> Periods { get; } = new();
}

internal sealed class PeriodStats
{
    public double Returns { get; set; }
    public double SharpeRatio { get; set; }
    public double MaxDrawdown { get; set; }
    public double WinRate { get; set; }
    public int TotalTrades { get; set; }
}

internal sealed class StrategyMetrics
{
    public string StrategyId { get; init; } = string.Empty;
    public string StrategyName { get; set; } = string.Empty;
    public string CreatorId { get; init; } = string.Empty;
    public string CreatorName { get; set; } = string.Empty;
    public bool IsPublic { get; set; } = true;
    public int SubscriberCount { get; set; }
    public decimal MonthlyPrice { get; set; }
    public Dictionary<LeaderboardPeriod, PeriodStats> Periods { get; } = new();
}

#endregion
