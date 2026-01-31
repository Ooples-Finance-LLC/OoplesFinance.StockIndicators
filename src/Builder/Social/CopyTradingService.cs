using OoplesFinance.StockIndicators.Builder.Trading;
using OoplesFinance.StockIndicators.Builder.Compliance;

namespace OoplesFinance.StockIndicators.Builder.Social;

/// <summary>
/// Service for copy trading functionality.
/// Allows users to automatically mirror trades from other traders.
/// </summary>
public interface ICopyTradingService
{
    /// <summary>Starts following a trader for copy trading.</summary>
    Task<CopyTradingSubscription> FollowTraderAsync(
        string followerId,
        string leaderId,
        CopyTradingSettings settings,
        CancellationToken ct = default);

    /// <summary>Stops following a trader.</summary>
    Task UnfollowTraderAsync(string followerId, string leaderId, CancellationToken ct = default);

    /// <summary>Gets all active subscriptions for a follower.</summary>
    Task<IReadOnlyList<CopyTradingSubscription>> GetSubscriptionsAsync(string followerId, CancellationToken ct = default);

    /// <summary>Gets all followers for a leader.</summary>
    Task<IReadOnlyList<FollowerInfo>> GetFollowersAsync(string leaderId, CancellationToken ct = default);

    /// <summary>Updates copy trading settings for a subscription.</summary>
    Task UpdateSettingsAsync(string followerId, string leaderId, CopyTradingSettings settings, CancellationToken ct = default);

    /// <summary>Processes a trade signal from a leader for distribution to followers.</summary>
    Task ProcessLeaderTradeAsync(string leaderId, TradeSignal signal, CancellationToken ct = default);

    /// <summary>Gets copy trading statistics for a follower.</summary>
    Task<CopyTradingStats> GetStatsAsync(string followerId, CancellationToken ct = default);

    /// <summary>Pauses copy trading for a subscription.</summary>
    Task PauseSubscriptionAsync(string followerId, string leaderId, CancellationToken ct = default);

    /// <summary>Resumes copy trading for a subscription.</summary>
    Task ResumeSubscriptionAsync(string followerId, string leaderId, CancellationToken ct = default);
}

/// <summary>
/// Implementation of the copy trading service.
/// </summary>
public sealed class CopyTradingService : ICopyTradingService
{
    private readonly Dictionary<string, List<CopyTradingSubscription>> _subscriptionsByFollower = new();
    private readonly Dictionary<string, List<string>> _followersByLeader = new();
    private readonly Dictionary<string, CopyTradingStats> _stats = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event Func<string, CopiedTrade, Task>? OnTradeToExecute;

    public async Task<CopyTradingSubscription> FollowTraderAsync(
        string followerId,
        string leaderId,
        CopyTradingSettings settings,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var subscription = new CopyTradingSubscription
            {
                FollowerId = followerId,
                LeaderId = leaderId,
                Settings = settings,
                StartedAt = DateTime.UtcNow,
                Status = CopyTradingStatus.Active
            };

            if (!_subscriptionsByFollower.ContainsKey(followerId))
            {
                _subscriptionsByFollower[followerId] = new List<CopyTradingSubscription>();
            }
            _subscriptionsByFollower[followerId].Add(subscription);

            if (!_followersByLeader.ContainsKey(leaderId))
            {
                _followersByLeader[leaderId] = new List<string>();
            }
            if (!_followersByLeader[leaderId].Contains(followerId))
            {
                _followersByLeader[leaderId].Add(followerId);
            }

            return subscription;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UnfollowTraderAsync(string followerId, string leaderId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_subscriptionsByFollower.TryGetValue(followerId, out var subs))
            {
                subs.RemoveAll(s => s.LeaderId == leaderId);
            }

            if (_followersByLeader.TryGetValue(leaderId, out var followers))
            {
                followers.Remove(followerId);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<CopyTradingSubscription>> GetSubscriptionsAsync(string followerId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return _subscriptionsByFollower.TryGetValue(followerId, out var subs)
                ? subs.ToList()
                : Array.Empty<CopyTradingSubscription>();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<FollowerInfo>> GetFollowersAsync(string leaderId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_followersByLeader.TryGetValue(leaderId, out var followerIds))
                return Array.Empty<FollowerInfo>();

            var followers = new List<FollowerInfo>();
            foreach (var followerId in followerIds)
            {
                if (_subscriptionsByFollower.TryGetValue(followerId, out var subs))
                {
                    var sub = subs.FirstOrDefault(s => s.LeaderId == leaderId);
                    if (sub is not null)
                    {
                        followers.Add(new FollowerInfo
                        {
                            FollowerId = followerId,
                            StartedAt = sub.StartedAt,
                            Status = sub.Status,
                            CopyRatio = sub.Settings.CopyRatio,
                            TotalCopiedTrades = sub.TotalCopiedTrades,
                            TotalPnL = sub.TotalPnL
                        });
                    }
                }
            }

            return followers;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateSettingsAsync(string followerId, string leaderId, CopyTradingSettings settings, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_subscriptionsByFollower.TryGetValue(followerId, out var subs))
            {
                var sub = subs.FirstOrDefault(s => s.LeaderId == leaderId);
                if (sub is not null)
                {
                    var index = subs.IndexOf(sub);
                    subs[index] = sub with { Settings = settings };
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ProcessLeaderTradeAsync(string leaderId, TradeSignal signal, CancellationToken ct = default)
    {
        List<string> activeFollowers;

        await _lock.WaitAsync(ct);
        try
        {
            if (!_followersByLeader.TryGetValue(leaderId, out var followers))
                return;

            activeFollowers = followers.ToList();
        }
        finally
        {
            _lock.Release();
        }

        foreach (var followerId in activeFollowers)
        {
            await _lock.WaitAsync(ct);
            CopyTradingSubscription? subscription = null;
            try
            {
                if (_subscriptionsByFollower.TryGetValue(followerId, out var subs))
                {
                    subscription = subs.FirstOrDefault(s => s.LeaderId == leaderId && s.Status == CopyTradingStatus.Active);
                }
            }
            finally
            {
                _lock.Release();
            }

            if (subscription is null)
                continue;

            // Apply copy trading settings
            var copiedTrade = CreateCopiedTrade(signal, subscription.Settings);

            // Check if trade passes filters
            if (!PassesFilters(copiedTrade, subscription.Settings))
                continue;

            // Apply delay if configured
            if (subscription.Settings.DelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(subscription.Settings.DelaySeconds), ct);
            }

            // Notify for execution
            if (OnTradeToExecute is not null)
            {
                await OnTradeToExecute(followerId, copiedTrade);
            }

            // Update statistics
            await UpdateStatsAsync(followerId, leaderId, copiedTrade, ct);
        }
    }

    public async Task<CopyTradingStats> GetStatsAsync(string followerId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return _stats.TryGetValue(followerId, out var stats)
                ? stats
                : new CopyTradingStats { FollowerId = followerId };
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task PauseSubscriptionAsync(string followerId, string leaderId, CancellationToken ct = default)
    {
        await SetSubscriptionStatusAsync(followerId, leaderId, CopyTradingStatus.Paused, ct);
    }

    public async Task ResumeSubscriptionAsync(string followerId, string leaderId, CancellationToken ct = default)
    {
        await SetSubscriptionStatusAsync(followerId, leaderId, CopyTradingStatus.Active, ct);
    }

    private async Task SetSubscriptionStatusAsync(string followerId, string leaderId, CopyTradingStatus status, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_subscriptionsByFollower.TryGetValue(followerId, out var subs))
            {
                var sub = subs.FirstOrDefault(s => s.LeaderId == leaderId);
                if (sub is not null)
                {
                    var index = subs.IndexOf(sub);
                    subs[index] = sub with { Status = status };
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private CopiedTrade CreateCopiedTrade(TradeSignal signal, CopyTradingSettings settings)
    {
        // Scale position size by copy ratio
        var scaledQuantity = (int)Math.Floor(signal.Quantity * settings.CopyRatio);

        // Apply max position size limit
        if (settings.MaxPositionSize.HasValue)
        {
            scaledQuantity = Math.Min(scaledQuantity, (int)settings.MaxPositionSize.Value);
        }

        return new CopiedTrade
        {
            OriginalSignalId = signal.SignalId,
            Symbol = signal.Symbol,
            Side = signal.Side,
            Quantity = scaledQuantity,
            OriginalQuantity = signal.Quantity,
            Price = signal.Price,
            OrderType = signal.OrderType,
            StopLoss = signal.StopLoss,
            TakeProfit = signal.TakeProfit,
            CreatedAt = DateTime.UtcNow
        };
    }

    private bool PassesFilters(CopiedTrade trade, CopyTradingSettings settings)
    {
        // Check asset class filters
        if (settings.AllowedAssetClasses.Count > 0)
        {
            var assetClass = DetermineAssetClass(trade.Symbol);
            if (!settings.AllowedAssetClasses.Contains(assetClass))
                return false;
        }

        // Check excluded symbols
        if (settings.ExcludedSymbols.Contains(trade.Symbol))
            return false;

        // Check minimum quantity
        if (trade.Quantity < 1)
            return false;

        return true;
    }

    private AssetClass DetermineAssetClass(string symbol)
    {
        if (symbol.Contains("/"))
            return AssetClass.Forex;
        if (symbol.EndsWith("USDT") || symbol.EndsWith("BTC") || symbol.EndsWith("ETH"))
            return AssetClass.Crypto;
        if (symbol.Contains(" ") && (symbol.Contains("C") || symbol.Contains("P")))
            return AssetClass.Options;
        return AssetClass.Equity;
    }

    private async Task UpdateStatsAsync(string followerId, string leaderId, CopiedTrade trade, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_stats.ContainsKey(followerId))
            {
                _stats[followerId] = new CopyTradingStats { FollowerId = followerId };
            }

            var stats = _stats[followerId];
            stats.TotalCopiedTrades++;

            // Update subscription stats
            if (_subscriptionsByFollower.TryGetValue(followerId, out var subs))
            {
                var sub = subs.FirstOrDefault(s => s.LeaderId == leaderId);
                if (sub is not null)
                {
                    var index = subs.IndexOf(sub);
                    subs[index] = sub with { TotalCopiedTrades = sub.TotalCopiedTrades + 1 };
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}

#region Types

public enum CopyTradingStatus
{
    Active,
    Paused,
    Stopped
}

public enum AssetClass
{
    Equity,
    Options,
    Forex,
    Crypto,
    Futures
}

public sealed record CopyTradingSubscription
{
    public string FollowerId { get; init; } = string.Empty;
    public string LeaderId { get; init; } = string.Empty;
    public CopyTradingSettings Settings { get; init; } = new();
    public DateTime StartedAt { get; init; }
    public CopyTradingStatus Status { get; init; }
    public int TotalCopiedTrades { get; init; }
    public decimal TotalPnL { get; init; }
}

public sealed record CopyTradingSettings
{
    /// <summary>Ratio of leader's position size to copy (0.0 to 1.0+).</summary>
    public double CopyRatio { get; init; } = 1.0;

    /// <summary>Maximum position size in dollars.</summary>
    public decimal? MaxPositionSize { get; init; }

    /// <summary>Delay in seconds before copying trade.</summary>
    public int DelaySeconds { get; init; } = 0;

    /// <summary>Stop copying if leader's drawdown exceeds this percentage.</summary>
    public double? StopCopyingOnDrawdown { get; init; }

    /// <summary>Allowed asset classes to copy.</summary>
    public HashSet<AssetClass> AllowedAssetClasses { get; init; } = new();

    /// <summary>Symbols to exclude from copying.</summary>
    public HashSet<string> ExcludedSymbols { get; init; } = new();

    /// <summary>Whether to copy stop loss and take profit levels.</summary>
    public bool CopyRiskManagement { get; init; } = true;
}

public sealed record FollowerInfo
{
    public string FollowerId { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
    public CopyTradingStatus Status { get; init; }
    public double CopyRatio { get; init; }
    public int TotalCopiedTrades { get; init; }
    public decimal TotalPnL { get; init; }
}

public sealed record TradeSignal
{
    public string SignalId { get; init; } = Guid.NewGuid().ToString();
    public string Symbol { get; init; } = string.Empty;
    public TradeSide Side { get; init; }
    public int Quantity { get; init; }
    public decimal? Price { get; init; }
    public OrderType OrderType { get; init; } = OrderType.Market;
    public decimal? StopLoss { get; init; }
    public decimal? TakeProfit { get; init; }
}

public sealed record CopiedTrade
{
    public string OriginalSignalId { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public TradeSide Side { get; init; }
    public int Quantity { get; init; }
    public int OriginalQuantity { get; init; }
    public decimal? Price { get; init; }
    public OrderType OrderType { get; init; }
    public decimal? StopLoss { get; init; }
    public decimal? TakeProfit { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class CopyTradingStats
{
    public string FollowerId { get; init; } = string.Empty;
    public int TotalCopiedTrades { get; set; }
    public decimal TotalPnL { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal BestTrade { get; set; }
    public decimal WorstTrade { get; set; }
}

#endregion
