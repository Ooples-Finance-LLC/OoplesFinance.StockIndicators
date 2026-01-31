namespace OoplesFinance.StockIndicators.Builder.Social;

/// <summary>
/// Marketplace for publishing and subscribing to trading strategies.
/// Supports free and premium strategies with revenue sharing.
/// </summary>
public interface IStrategyMarketplace
{
    /// <summary>Publishes a strategy to the marketplace.</summary>
    Task<MarketplaceStrategy> PublishStrategyAsync(
        string creatorId,
        StrategyPublishRequest request,
        CancellationToken ct = default);

    /// <summary>Updates a published strategy.</summary>
    Task<MarketplaceStrategy> UpdateStrategyAsync(
        string strategyId,
        StrategyUpdateRequest request,
        CancellationToken ct = default);

    /// <summary>Unpublishes a strategy from the marketplace.</summary>
    Task UnpublishStrategyAsync(string strategyId, string creatorId, CancellationToken ct = default);

    /// <summary>Searches strategies in the marketplace.</summary>
    Task<MarketplaceSearchResult> SearchStrategiesAsync(
        StrategySearchQuery query,
        CancellationToken ct = default);

    /// <summary>Gets a specific strategy by ID.</summary>
    Task<MarketplaceStrategy?> GetStrategyAsync(string strategyId, CancellationToken ct = default);

    /// <summary>Subscribes a user to a strategy.</summary>
    Task<StrategySubscription> SubscribeAsync(
        string userId,
        string strategyId,
        CancellationToken ct = default);

    /// <summary>Unsubscribes a user from a strategy.</summary>
    Task UnsubscribeAsync(string userId, string strategyId, CancellationToken ct = default);

    /// <summary>Gets all subscriptions for a user.</summary>
    Task<IReadOnlyList<StrategySubscription>> GetUserSubscriptionsAsync(string userId, CancellationToken ct = default);

    /// <summary>Rates a strategy.</summary>
    Task RateStrategyAsync(string userId, string strategyId, int rating, string? review, CancellationToken ct = default);

    /// <summary>Gets creator earnings summary.</summary>
    Task<CreatorEarnings> GetCreatorEarningsAsync(string creatorId, CancellationToken ct = default);
}

/// <summary>
/// Implementation of the strategy marketplace.
/// </summary>
public sealed class StrategyMarketplace : IStrategyMarketplace
{
    private readonly Dictionary<string, MarketplaceStrategy> _strategies = new();
    private readonly Dictionary<string, List<StrategySubscription>> _subscriptionsByUser = new();
    private readonly Dictionary<string, List<StrategyRating>> _ratingsByStrategy = new();
    private readonly Dictionary<string, CreatorEarnings> _earnings = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Revenue share: 70% creator, 30% platform
    private const decimal CREATOR_REVENUE_SHARE = 0.70m;

    public async Task<MarketplaceStrategy> PublishStrategyAsync(
        string creatorId,
        StrategyPublishRequest request,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var strategyId = Guid.NewGuid().ToString();
            var strategy = new MarketplaceStrategy
            {
                StrategyId = strategyId,
                CreatorId = creatorId,
                Name = request.Name,
                Description = request.Description,
                LongDescription = request.LongDescription,
                Category = request.Category,
                Tags = request.Tags,
                AssetClasses = request.AssetClasses,
                RiskLevel = request.RiskLevel,
                MinimumCapital = request.MinimumCapital,
                PricingModel = request.PricingModel,
                MonthlyPrice = request.MonthlyPrice,
                PerformanceFeePercent = request.PerformanceFeePercent,
                BacktestResults = request.BacktestResults,
                StrategyCode = request.StrategyCode,
                Version = "1.0.0",
                Status = StrategyStatus.PendingReview,
                PublishedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            _strategies[strategyId] = strategy;

            // Initialize creator earnings
            if (!_earnings.ContainsKey(creatorId))
            {
                _earnings[creatorId] = new CreatorEarnings { CreatorId = creatorId };
            }

            // Auto-approve for now (in production, would have review process)
            strategy = strategy with { Status = StrategyStatus.Active };
            _strategies[strategyId] = strategy;

            return strategy;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<MarketplaceStrategy> UpdateStrategyAsync(
        string strategyId,
        StrategyUpdateRequest request,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_strategies.TryGetValue(strategyId, out var strategy))
                throw new KeyNotFoundException($"Strategy {strategyId} not found");

            // Increment version
            var versionParts = strategy.Version.Split('.');
            var minorVersion = int.Parse(versionParts[1]) + 1;
            var newVersion = $"{versionParts[0]}.{minorVersion}.0";

            strategy = strategy with
            {
                Description = request.Description ?? strategy.Description,
                LongDescription = request.LongDescription ?? strategy.LongDescription,
                Tags = request.Tags ?? strategy.Tags,
                MonthlyPrice = request.MonthlyPrice ?? strategy.MonthlyPrice,
                PerformanceFeePercent = request.PerformanceFeePercent ?? strategy.PerformanceFeePercent,
                StrategyCode = request.StrategyCode ?? strategy.StrategyCode,
                BacktestResults = request.BacktestResults ?? strategy.BacktestResults,
                Version = newVersion,
                LastUpdatedAt = DateTime.UtcNow
            };

            _strategies[strategyId] = strategy;
            return strategy;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UnpublishStrategyAsync(string strategyId, string creatorId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_strategies.TryGetValue(strategyId, out var strategy))
                return;

            if (strategy.CreatorId != creatorId)
                throw new UnauthorizedAccessException("Only the creator can unpublish a strategy");

            strategy = strategy with { Status = StrategyStatus.Unpublished };
            _strategies[strategyId] = strategy;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<MarketplaceSearchResult> SearchStrategiesAsync(
        StrategySearchQuery query,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var strategies = _strategies.Values
                .Where(s => s.Status == StrategyStatus.Active);

            // Apply filters
            if (query.SearchTerm is { Length: > 0 } searchTerm)
            {
                var term = searchTerm.ToLowerInvariant();
                strategies = strategies.Where(s =>
                    s.Name.ToLowerInvariant().Contains(term) ||
                    s.Description.ToLowerInvariant().Contains(term) ||
                    s.Tags.Any(t => t.ToLowerInvariant().Contains(term)));
            }

            if (query.Category.HasValue)
            {
                strategies = strategies.Where(s => s.Category == query.Category.Value);
            }

            if (query.AssetClasses.Count > 0)
            {
                strategies = strategies.Where(s => s.AssetClasses.Intersect(query.AssetClasses).Any());
            }

            if (query.RiskLevel.HasValue)
            {
                strategies = strategies.Where(s => s.RiskLevel == query.RiskLevel.Value);
            }

            if (query.PricingModel.HasValue)
            {
                strategies = strategies.Where(s => s.PricingModel == query.PricingModel.Value);
            }

            if (query.MaxMonthlyPrice.HasValue)
            {
                strategies = strategies.Where(s => s.MonthlyPrice <= query.MaxMonthlyPrice.Value);
            }

            if (query.MinimumRating.HasValue)
            {
                strategies = strategies.Where(s => GetAverageRating(s.StrategyId) >= query.MinimumRating.Value);
            }

            // Apply sorting
            strategies = query.SortBy switch
            {
                StrategySortBy.Newest => strategies.OrderByDescending(s => s.PublishedAt),
                StrategySortBy.MostPopular => strategies.OrderByDescending(s => s.SubscriberCount),
                StrategySortBy.HighestRated => strategies.OrderByDescending(s => GetAverageRating(s.StrategyId)),
                StrategySortBy.BestReturns => strategies.OrderByDescending(s => s.BacktestResults?.TotalReturn ?? 0),
                StrategySortBy.LowestPrice => strategies.OrderBy(s => s.MonthlyPrice),
                _ => strategies.OrderByDescending(s => s.SubscriberCount)
            };

            var totalCount = strategies.Count();
            var items = strategies
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            return new MarketplaceSearchResult
            {
                Strategies = items,
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<MarketplaceStrategy?> GetStrategyAsync(string strategyId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return _strategies.TryGetValue(strategyId, out var strategy) ? strategy : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<StrategySubscription> SubscribeAsync(
        string userId,
        string strategyId,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_strategies.TryGetValue(strategyId, out var strategy))
                throw new KeyNotFoundException($"Strategy {strategyId} not found");

            var subscription = new StrategySubscription
            {
                UserId = userId,
                StrategyId = strategyId,
                StrategyName = strategy.Name,
                CreatorId = strategy.CreatorId,
                SubscribedAt = DateTime.UtcNow,
                Status = SubscriptionStatus.Active,
                MonthlyPrice = strategy.MonthlyPrice,
                PerformanceFeePercent = strategy.PerformanceFeePercent
            };

            if (!_subscriptionsByUser.ContainsKey(userId))
            {
                _subscriptionsByUser[userId] = new List<StrategySubscription>();
            }
            _subscriptionsByUser[userId].Add(subscription);

            // Update subscriber count
            strategy = strategy with { SubscriberCount = strategy.SubscriberCount + 1 };
            _strategies[strategyId] = strategy;

            // Process payment and update earnings
            if (strategy.MonthlyPrice > 0)
            {
                var creatorShare = strategy.MonthlyPrice * CREATOR_REVENUE_SHARE;
                if (_earnings.TryGetValue(strategy.CreatorId, out var earnings))
                {
                    earnings.TotalEarnings += creatorShare;
                    earnings.PendingPayout += creatorShare;
                    earnings.TotalSubscribers++;
                }
            }

            return subscription;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UnsubscribeAsync(string userId, string strategyId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_subscriptionsByUser.TryGetValue(userId, out var subs))
            {
                var sub = subs.FirstOrDefault(s => s.StrategyId == strategyId);
                if (sub is not null)
                {
                    subs.Remove(sub);

                    // Update subscriber count
                    if (_strategies.TryGetValue(strategyId, out var strategy))
                    {
                        strategy = strategy with { SubscriberCount = Math.Max(0, strategy.SubscriberCount - 1) };
                        _strategies[strategyId] = strategy;
                    }
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<StrategySubscription>> GetUserSubscriptionsAsync(string userId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return _subscriptionsByUser.TryGetValue(userId, out var subs)
                ? subs.ToList()
                : Array.Empty<StrategySubscription>();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RateStrategyAsync(string userId, string strategyId, int rating, string? review, CancellationToken ct = default)
    {
        if (rating < 1 || rating > 5)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5");

        await _lock.WaitAsync(ct);
        try
        {
            if (!_ratingsByStrategy.ContainsKey(strategyId))
            {
                _ratingsByStrategy[strategyId] = new List<StrategyRating>();
            }

            // Remove existing rating from this user
            _ratingsByStrategy[strategyId].RemoveAll(r => r.UserId == userId);

            // Add new rating
            _ratingsByStrategy[strategyId].Add(new StrategyRating
            {
                UserId = userId,
                StrategyId = strategyId,
                Rating = rating,
                Review = review,
                CreatedAt = DateTime.UtcNow
            });

            // Update strategy rating
            if (_strategies.TryGetValue(strategyId, out var strategy))
            {
                var avgRating = GetAverageRating(strategyId);
                var ratingCount = _ratingsByStrategy[strategyId].Count;
                strategy = strategy with { AverageRating = avgRating, RatingCount = ratingCount };
                _strategies[strategyId] = strategy;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<CreatorEarnings> GetCreatorEarningsAsync(string creatorId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return _earnings.TryGetValue(creatorId, out var earnings)
                ? earnings
                : new CreatorEarnings { CreatorId = creatorId };
        }
        finally
        {
            _lock.Release();
        }
    }

    private double GetAverageRating(string strategyId)
    {
        if (!_ratingsByStrategy.TryGetValue(strategyId, out var ratings) || ratings.Count == 0)
            return 0;

        return ratings.Average(r => r.Rating);
    }
}

#region Types

public enum StrategyCategory
{
    TrendFollowing,
    MeanReversion,
    Momentum,
    Arbitrage,
    MarketMaking,
    MachineLearning,
    Options,
    Forex,
    Crypto,
    MultiAsset
}

public enum StrategyRiskLevel
{
    Conservative,
    Moderate,
    Aggressive,
    VeryAggressive
}

public enum StrategyPricingModel
{
    Free,
    Monthly,
    PerformanceFee,
    Hybrid
}

public enum StrategyStatus
{
    Draft,
    PendingReview,
    Active,
    Suspended,
    Unpublished
}

public enum SubscriptionStatus
{
    Active,
    Paused,
    Cancelled,
    Expired
}

public enum StrategySortBy
{
    Newest,
    MostPopular,
    HighestRated,
    BestReturns,
    LowestPrice
}

public sealed record MarketplaceStrategy
{
    public string StrategyId { get; init; } = string.Empty;
    public string CreatorId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? LongDescription { get; init; }
    public StrategyCategory Category { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<AssetClass> AssetClasses { get; init; } = Array.Empty<AssetClass>();
    public StrategyRiskLevel RiskLevel { get; init; }
    public decimal MinimumCapital { get; init; }
    public StrategyPricingModel PricingModel { get; init; }
    public decimal MonthlyPrice { get; init; }
    public decimal PerformanceFeePercent { get; init; }
    public StrategyBacktestResults? BacktestResults { get; init; }
    public string? StrategyCode { get; init; }
    public string Version { get; init; } = "1.0.0";
    public StrategyStatus Status { get; init; }
    public DateTime PublishedAt { get; init; }
    public DateTime LastUpdatedAt { get; init; }
    public int SubscriberCount { get; init; }
    public double AverageRating { get; init; }
    public int RatingCount { get; init; }
}

public sealed record StrategyPublishRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? LongDescription { get; init; }
    public StrategyCategory Category { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<AssetClass> AssetClasses { get; init; } = Array.Empty<AssetClass>();
    public StrategyRiskLevel RiskLevel { get; init; }
    public decimal MinimumCapital { get; init; }
    public StrategyPricingModel PricingModel { get; init; }
    public decimal MonthlyPrice { get; init; }
    public decimal PerformanceFeePercent { get; init; }
    public StrategyBacktestResults? BacktestResults { get; init; }
    public string? StrategyCode { get; init; }
}

public sealed record StrategyUpdateRequest
{
    public string? Description { get; init; }
    public string? LongDescription { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public decimal? MonthlyPrice { get; init; }
    public decimal? PerformanceFeePercent { get; init; }
    public string? StrategyCode { get; init; }
    public StrategyBacktestResults? BacktestResults { get; init; }
}

public sealed record StrategySearchQuery
{
    public string? SearchTerm { get; init; }
    public StrategyCategory? Category { get; init; }
    public HashSet<AssetClass> AssetClasses { get; init; } = new();
    public StrategyRiskLevel? RiskLevel { get; init; }
    public StrategyPricingModel? PricingModel { get; init; }
    public decimal? MaxMonthlyPrice { get; init; }
    public double? MinimumRating { get; init; }
    public StrategySortBy SortBy { get; init; } = StrategySortBy.MostPopular;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record MarketplaceSearchResult
{
    public IReadOnlyList<MarketplaceStrategy> Strategies { get; init; } = Array.Empty<MarketplaceStrategy>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record StrategySubscription
{
    public string UserId { get; init; } = string.Empty;
    public string StrategyId { get; init; } = string.Empty;
    public string StrategyName { get; init; } = string.Empty;
    public string CreatorId { get; init; } = string.Empty;
    public DateTime SubscribedAt { get; init; }
    public SubscriptionStatus Status { get; init; }
    public decimal MonthlyPrice { get; init; }
    public decimal PerformanceFeePercent { get; init; }
}

public sealed record StrategyBacktestResults
{
    public decimal TotalReturn { get; init; }
    public decimal AnnualizedReturn { get; init; }
    public double SharpeRatio { get; init; }
    public double SortinoRatio { get; init; }
    public decimal MaxDrawdown { get; init; }
    public double WinRate { get; init; }
    public int TotalTrades { get; init; }
    public decimal ProfitFactor { get; init; }
    public DateTime BacktestStart { get; init; }
    public DateTime BacktestEnd { get; init; }
}

public sealed record StrategyRating
{
    public string UserId { get; init; } = string.Empty;
    public string StrategyId { get; init; } = string.Empty;
    public int Rating { get; init; }
    public string? Review { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class CreatorEarnings
{
    public string CreatorId { get; init; } = string.Empty;
    public decimal TotalEarnings { get; set; }
    public decimal PendingPayout { get; set; }
    public decimal TotalPaidOut { get; set; }
    public int TotalSubscribers { get; set; }
    public int ActiveSubscribers { get; set; }
}

#endregion
