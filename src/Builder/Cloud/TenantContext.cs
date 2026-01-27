namespace OoplesFinance.StockIndicators.Builder.Cloud;

/// <summary>
/// Multi-tenant context for SaaS deployments.
/// Provides tenant isolation and resource management.
/// </summary>
public sealed class TenantContext : IDisposable
{
    private static readonly AsyncLocal<TenantContext?> _current = new();
    private readonly Dictionary<string, object> _properties = new();
    private bool _disposed;

    /// <summary>Gets or sets the current tenant context.</summary>
    public static TenantContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; }

    /// <summary>Gets the tenant name.</summary>
    public string TenantName { get; }

    /// <summary>Gets the subscription tier.</summary>
    public SubscriptionTier Tier { get; }

    /// <summary>Gets the tenant's resource limits.</summary>
    public ResourceLimits Limits { get; }

    /// <summary>Gets the tenant's feature flags.</summary>
    public FeatureFlags Features { get; }

    /// <summary>Gets when the tenant was created.</summary>
    public DateTime CreatedAt { get; }

    /// <summary>Gets the tenant's timezone.</summary>
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;

    /// <summary>Gets the tenant's preferred currency.</summary>
    public string PreferredCurrency { get; set; } = "USD";

    /// <summary>
    /// Creates a new tenant context.
    /// </summary>
    public TenantContext(
        string tenantId,
        string tenantName,
        SubscriptionTier tier = SubscriptionTier.Free)
    {
        TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
        TenantName = tenantName ?? throw new ArgumentNullException(nameof(tenantName));
        Tier = tier;
        Limits = ResourceLimits.ForTier(tier);
        Features = FeatureFlags.ForTier(tier);
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets a custom property on the tenant context.
    /// </summary>
    public void SetProperty<T>(string key, T value)
    {
        ThrowIfDisposed();
        if (value is null)
        {
            _properties.Remove(key);
        }
        else
        {
            _properties[key] = value;
        }
    }

    /// <summary>
    /// Gets a custom property from the tenant context.
    /// </summary>
    public T? GetProperty<T>(string key)
    {
        ThrowIfDisposed();
        return _properties.TryGetValue(key, out var value) && value is T typed ? typed : default;
    }

    /// <summary>
    /// Creates a scoped tenant context that resets when disposed.
    /// </summary>
    public static IDisposable BeginScope(TenantContext context)
    {
        var previous = Current;
        Current = context;
        return new TenantScope(previous);
    }

    /// <summary>
    /// Validates that the current operation is allowed for this tenant.
    /// </summary>
    public void ValidateOperation(OperationType operation)
    {
        ThrowIfDisposed();

        if (!Features.IsEnabled(operation))
        {
            throw new TenantOperationNotAllowedException(TenantId, operation,
                $"Operation '{operation}' is not available for tier '{Tier}'");
        }
    }

    /// <summary>
    /// Checks if a resource limit would be exceeded.
    /// </summary>
    public bool WouldExceedLimit(ResourceType resource, int additionalCount)
    {
        ThrowIfDisposed();
        var currentUsage = GetProperty<int>($"usage_{resource}");
        var limit = Limits.GetLimit(resource);
        return currentUsage + additionalCount > limit;
    }

    /// <summary>
    /// Increments resource usage.
    /// </summary>
    public void IncrementUsage(ResourceType resource, int count = 1)
    {
        ThrowIfDisposed();
        var key = $"usage_{resource}";
        var current = GetProperty<int>(key);
        SetProperty(key, current + count);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TenantContext));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _properties.Clear();
            _disposed = true;
        }
    }

    private sealed class TenantScope : IDisposable
    {
        private readonly TenantContext? _previous;

        public TenantScope(TenantContext? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            Current = _previous;
        }
    }
}

/// <summary>
/// Subscription tiers for SaaS pricing.
/// </summary>
public enum SubscriptionTier
{
    /// <summary>Free tier with limited features.</summary>
    Free = 0,

    /// <summary>Basic paid tier.</summary>
    Basic = 1,

    /// <summary>Professional tier with advanced features.</summary>
    Professional = 2,

    /// <summary>Enterprise tier with all features.</summary>
    Enterprise = 3,

    /// <summary>Custom tier with negotiated limits.</summary>
    Custom = 4
}

/// <summary>
/// Resource limits per tier.
/// </summary>
public sealed class ResourceLimits
{
    /// <summary>Gets the maximum number of strategies.</summary>
    public int MaxStrategies { get; init; }

    /// <summary>Gets the maximum number of active positions.</summary>
    public int MaxPositions { get; init; }

    /// <summary>Gets the maximum number of broker connections.</summary>
    public int MaxBrokerConnections { get; init; }

    /// <summary>Gets the maximum API calls per minute.</summary>
    public int MaxApiCallsPerMinute { get; init; }

    /// <summary>Gets the maximum data lookback in days.</summary>
    public int MaxDataLookbackDays { get; init; }

    /// <summary>Gets the maximum concurrent backtests.</summary>
    public int MaxConcurrentBacktests { get; init; }

    /// <summary>Gets the maximum symbols per watchlist.</summary>
    public int MaxSymbolsPerWatchlist { get; init; }

    /// <summary>Gets the maximum alerts.</summary>
    public int MaxAlerts { get; init; }

    /// <summary>
    /// Gets the limit for a specific resource type.
    /// </summary>
    public int GetLimit(ResourceType resource) => resource switch
    {
        ResourceType.Strategies => MaxStrategies,
        ResourceType.Positions => MaxPositions,
        ResourceType.BrokerConnections => MaxBrokerConnections,
        ResourceType.ApiCalls => MaxApiCallsPerMinute,
        ResourceType.ConcurrentBacktests => MaxConcurrentBacktests,
        ResourceType.WatchlistSymbols => MaxSymbolsPerWatchlist,
        ResourceType.Alerts => MaxAlerts,
        _ => 0
    };

    /// <summary>
    /// Gets resource limits for a subscription tier.
    /// </summary>
    public static ResourceLimits ForTier(SubscriptionTier tier) => tier switch
    {
        SubscriptionTier.Free => new ResourceLimits
        {
            MaxStrategies = 3,
            MaxPositions = 5,
            MaxBrokerConnections = 1,
            MaxApiCallsPerMinute = 60,
            MaxDataLookbackDays = 30,
            MaxConcurrentBacktests = 1,
            MaxSymbolsPerWatchlist = 10,
            MaxAlerts = 5
        },
        SubscriptionTier.Basic => new ResourceLimits
        {
            MaxStrategies = 10,
            MaxPositions = 25,
            MaxBrokerConnections = 2,
            MaxApiCallsPerMinute = 300,
            MaxDataLookbackDays = 365,
            MaxConcurrentBacktests = 3,
            MaxSymbolsPerWatchlist = 50,
            MaxAlerts = 25
        },
        SubscriptionTier.Professional => new ResourceLimits
        {
            MaxStrategies = 50,
            MaxPositions = 100,
            MaxBrokerConnections = 5,
            MaxApiCallsPerMinute = 1000,
            MaxDataLookbackDays = 365 * 5,
            MaxConcurrentBacktests = 10,
            MaxSymbolsPerWatchlist = 200,
            MaxAlerts = 100
        },
        SubscriptionTier.Enterprise or SubscriptionTier.Custom => new ResourceLimits
        {
            MaxStrategies = int.MaxValue,
            MaxPositions = int.MaxValue,
            MaxBrokerConnections = int.MaxValue,
            MaxApiCallsPerMinute = int.MaxValue,
            MaxDataLookbackDays = int.MaxValue,
            MaxConcurrentBacktests = int.MaxValue,
            MaxSymbolsPerWatchlist = int.MaxValue,
            MaxAlerts = int.MaxValue
        },
        _ => ForTier(SubscriptionTier.Free)
    };
}

/// <summary>
/// Resource types for limit tracking.
/// </summary>
public enum ResourceType
{
    /// <summary>Trading strategies.</summary>
    Strategies,

    /// <summary>Active positions.</summary>
    Positions,

    /// <summary>Broker connections.</summary>
    BrokerConnections,

    /// <summary>API calls.</summary>
    ApiCalls,

    /// <summary>Concurrent backtests.</summary>
    ConcurrentBacktests,

    /// <summary>Watchlist symbols.</summary>
    WatchlistSymbols,

    /// <summary>Price alerts.</summary>
    Alerts
}

/// <summary>
/// Feature flags per tier.
/// </summary>
public sealed class FeatureFlags
{
    /// <summary>Gets whether paper trading is enabled.</summary>
    public bool PaperTrading { get; init; }

    /// <summary>Gets whether live trading is enabled.</summary>
    public bool LiveTrading { get; init; }

    /// <summary>Gets whether options trading is enabled.</summary>
    public bool OptionsTrading { get; init; }

    /// <summary>Gets whether futures trading is enabled.</summary>
    public bool FuturesTrading { get; init; }

    /// <summary>Gets whether forex trading is enabled.</summary>
    public bool ForexTrading { get; init; }

    /// <summary>Gets whether crypto trading is enabled.</summary>
    public bool CryptoTrading { get; init; }

    /// <summary>Gets whether advanced order types are enabled.</summary>
    public bool AdvancedOrderTypes { get; init; }

    /// <summary>Gets whether smart order routing is enabled.</summary>
    public bool SmartOrderRouting { get; init; }

    /// <summary>Gets whether dark pool access is enabled.</summary>
    public bool DarkPoolAccess { get; init; }

    /// <summary>Gets whether Monte Carlo simulation is enabled.</summary>
    public bool MonteCarloSimulation { get; init; }

    /// <summary>Gets whether walk-forward analysis is enabled.</summary>
    public bool WalkForwardAnalysis { get; init; }

    /// <summary>Gets whether API access is enabled.</summary>
    public bool ApiAccess { get; init; }

    /// <summary>Gets whether webhook notifications are enabled.</summary>
    public bool WebhookNotifications { get; init; }

    /// <summary>Gets whether priority support is enabled.</summary>
    public bool PrioritySupport { get; init; }

    /// <summary>
    /// Checks if an operation type is enabled.
    /// </summary>
    public bool IsEnabled(OperationType operation) => operation switch
    {
        OperationType.PaperTrade => PaperTrading,
        OperationType.LiveTrade => LiveTrading,
        OperationType.OptionsOrder => OptionsTrading,
        OperationType.FuturesOrder => FuturesTrading,
        OperationType.ForexOrder => ForexTrading,
        OperationType.CryptoOrder => CryptoTrading,
        OperationType.BracketOrder or OperationType.OcoOrder => AdvancedOrderTypes,
        OperationType.SmartRoute => SmartOrderRouting,
        OperationType.DarkPoolRoute => DarkPoolAccess,
        OperationType.MonteCarlo => MonteCarloSimulation,
        OperationType.WalkForward => WalkForwardAnalysis,
        OperationType.ApiCall => ApiAccess,
        OperationType.Webhook => WebhookNotifications,
        _ => true
    };

    /// <summary>
    /// Gets feature flags for a subscription tier.
    /// </summary>
    public static FeatureFlags ForTier(SubscriptionTier tier) => tier switch
    {
        SubscriptionTier.Free => new FeatureFlags
        {
            PaperTrading = true,
            LiveTrading = false,
            OptionsTrading = false,
            FuturesTrading = false,
            ForexTrading = false,
            CryptoTrading = false,
            AdvancedOrderTypes = false,
            SmartOrderRouting = false,
            DarkPoolAccess = false,
            MonteCarloSimulation = false,
            WalkForwardAnalysis = false,
            ApiAccess = false,
            WebhookNotifications = false,
            PrioritySupport = false
        },
        SubscriptionTier.Basic => new FeatureFlags
        {
            PaperTrading = true,
            LiveTrading = true,
            OptionsTrading = false,
            FuturesTrading = false,
            ForexTrading = false,
            CryptoTrading = true,
            AdvancedOrderTypes = true,
            SmartOrderRouting = false,
            DarkPoolAccess = false,
            MonteCarloSimulation = false,
            WalkForwardAnalysis = false,
            ApiAccess = true,
            WebhookNotifications = true,
            PrioritySupport = false
        },
        SubscriptionTier.Professional => new FeatureFlags
        {
            PaperTrading = true,
            LiveTrading = true,
            OptionsTrading = true,
            FuturesTrading = true,
            ForexTrading = true,
            CryptoTrading = true,
            AdvancedOrderTypes = true,
            SmartOrderRouting = true,
            DarkPoolAccess = false,
            MonteCarloSimulation = true,
            WalkForwardAnalysis = true,
            ApiAccess = true,
            WebhookNotifications = true,
            PrioritySupport = true
        },
        SubscriptionTier.Enterprise or SubscriptionTier.Custom => new FeatureFlags
        {
            PaperTrading = true,
            LiveTrading = true,
            OptionsTrading = true,
            FuturesTrading = true,
            ForexTrading = true,
            CryptoTrading = true,
            AdvancedOrderTypes = true,
            SmartOrderRouting = true,
            DarkPoolAccess = true,
            MonteCarloSimulation = true,
            WalkForwardAnalysis = true,
            ApiAccess = true,
            WebhookNotifications = true,
            PrioritySupport = true
        },
        _ => ForTier(SubscriptionTier.Free)
    };
}

/// <summary>
/// Operation types for feature gating.
/// </summary>
public enum OperationType
{
    /// <summary>Paper trading operation.</summary>
    PaperTrade,

    /// <summary>Live trading operation.</summary>
    LiveTrade,

    /// <summary>Options order.</summary>
    OptionsOrder,

    /// <summary>Futures order.</summary>
    FuturesOrder,

    /// <summary>Forex order.</summary>
    ForexOrder,

    /// <summary>Crypto order.</summary>
    CryptoOrder,

    /// <summary>Bracket order.</summary>
    BracketOrder,

    /// <summary>OCO order.</summary>
    OcoOrder,

    /// <summary>Smart order routing.</summary>
    SmartRoute,

    /// <summary>Dark pool routing.</summary>
    DarkPoolRoute,

    /// <summary>Monte Carlo simulation.</summary>
    MonteCarlo,

    /// <summary>Walk-forward analysis.</summary>
    WalkForward,

    /// <summary>API call.</summary>
    ApiCall,

    /// <summary>Webhook notification.</summary>
    Webhook
}

/// <summary>
/// Exception thrown when a tenant operation is not allowed.
/// </summary>
public sealed class TenantOperationNotAllowedException : Exception
{
    /// <summary>Gets the tenant ID.</summary>
    public string TenantId { get; }

    /// <summary>Gets the operation that was denied.</summary>
    public OperationType Operation { get; }

    /// <summary>
    /// Creates a new tenant operation not allowed exception.
    /// </summary>
    public TenantOperationNotAllowedException(string tenantId, OperationType operation, string message)
        : base(message)
    {
        TenantId = tenantId;
        Operation = operation;
    }
}
