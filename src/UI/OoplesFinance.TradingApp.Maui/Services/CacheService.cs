using System.Collections.Concurrent;
using System.Text.Json;
using StackExchange.Redis;

namespace OoplesFinance.TradingApp.Maui.Services;

/// <summary>
/// Cache service interface for distributed caching
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);
    Task<long> IncrementAsync(string key, long value = 1, CancellationToken cancellationToken = default);
    Task<bool> LockAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);
    Task ReleaseLockAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cache key builder for consistent key generation
/// </summary>
public static class CacheKeys
{
    private const string Prefix = "oopfin";

    // Market Data
    public static string Quote(string symbol) => $"{Prefix}:quote:{symbol.ToUpperInvariant()}";
    public static string QuotesBatch(string symbols) => $"{Prefix}:quotes:{symbols}";
    public static string MarketStatus() => $"{Prefix}:market:status";
    public static string HistoricalBars(string symbol, string timeframe, string start, string end)
        => $"{Prefix}:bars:{symbol.ToUpperInvariant()}:{timeframe}:{start}:{end}";

    // User Data
    public static string UserProfile(string userId) => $"{Prefix}:user:{userId}:profile";
    public static string UserPositions(string userId) => $"{Prefix}:user:{userId}:positions";
    public static string UserOrders(string userId) => $"{Prefix}:user:{userId}:orders";
    public static string UserWatchlists(string userId) => $"{Prefix}:user:{userId}:watchlists";
    public static string UserStrategies(string userId) => $"{Prefix}:user:{userId}:strategies";
    public static string UserAlerts(string userId) => $"{Prefix}:user:{userId}:alerts";
    public static string UserSettings(string userId) => $"{Prefix}:user:{userId}:settings";

    // Session
    public static string Session(string sessionId) => $"{Prefix}:session:{sessionId}";
    public static string RateLimit(string userId, string endpoint) => $"{Prefix}:ratelimit:{userId}:{endpoint}";

    // Strategies (public)
    public static string PublicStrategies(int page, int pageSize) => $"{Prefix}:strategies:public:{page}:{pageSize}";
    public static string StrategyDetails(string strategyId) => $"{Prefix}:strategy:{strategyId}";
    public static string SubscriptionTiers() => $"{Prefix}:tiers";

    // Locks
    public static string OrderLock(string userId) => $"{Prefix}:lock:order:{userId}";
    public static string PositionLock(string userId, string symbol) => $"{Prefix}:lock:position:{userId}:{symbol}";
}

/// <summary>
/// Cache TTL policies
/// </summary>
public static class CacheTtl
{
    public static readonly TimeSpan Quote = TimeSpan.FromSeconds(5);           // Real-time quotes - very short
    public static readonly TimeSpan MarketStatus = TimeSpan.FromMinutes(1);    // Market open/close status
    public static readonly TimeSpan HistoricalData = TimeSpan.FromHours(1);    // Historical bars
    public static readonly TimeSpan UserProfile = TimeSpan.FromMinutes(15);    // User profile data
    public static readonly TimeSpan Positions = TimeSpan.FromSeconds(30);      // Positions - refresh often
    public static readonly TimeSpan Orders = TimeSpan.FromSeconds(10);         // Active orders - very fresh
    public static readonly TimeSpan Watchlists = TimeSpan.FromMinutes(5);      // Watchlists
    public static readonly TimeSpan Strategies = TimeSpan.FromMinutes(10);     // User strategies
    public static readonly TimeSpan PublicStrategies = TimeSpan.FromMinutes(5);// Public marketplace
    public static readonly TimeSpan SubscriptionTiers = TimeSpan.FromHours(24);// Rarely changes
    public static readonly TimeSpan Session = TimeSpan.FromHours(1);           // Session data
    public static readonly TimeSpan Lock = TimeSpan.FromSeconds(30);           // Distributed locks
}

/// <summary>
/// Redis-based distributed cache implementation
/// </summary>
public class RedisCacheService : ICacheService, IAsyncDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    public RedisCacheService(string connectionString)
    {
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 3;
        options.ConnectTimeout = 5000;
        options.SyncTimeout = 5000;
        options.AsyncTimeout = 5000;

        _redis = ConnectionMultiplexer.Connect(options);
        _db = _redis.GetDatabase();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _db.StringGetAsync(key);
            if (value.IsNullOrEmpty)
                return default;

            if (typeof(T) == typeof(string))
                return (T)(object)value.ToString();

            return JsonSerializer.Deserialize<T>(value!, _jsonOptions);
        }
        catch (RedisConnectionException)
        {
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        try
        {
            string serialized;
            if (value is string str)
                serialized = str;
            else
                serialized = JsonSerializer.Serialize(value, _jsonOptions);

            await _db.StringSetAsync(key, serialized, expiry);
        }
        catch (RedisConnectionException)
        {
            // Silently fail - cache is optional
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.KeyDeleteAsync(key);
        }
        catch (RedisConnectionException)
        {
            // Silently fail
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.KeyExistsAsync(key);
        }
        catch (RedisConnectionException)
        {
            return false;
        }
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var value = await factory();
        await SetAsync(key, value, expiry, cancellationToken);
        return value;
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoints = _redis.GetEndPoints();
            foreach (var endpoint in endpoints)
            {
                var server = _redis.GetServer(endpoint);
                var keys = server.Keys(pattern: pattern).ToArray();
                if (keys.Length > 0)
                {
                    await _db.KeyDeleteAsync(keys);
                }
            }
        }
        catch (RedisConnectionException)
        {
            // Silently fail
        }
    }

    public async Task<long> IncrementAsync(string key, long value = 1, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.StringIncrementAsync(key, value);
        }
        catch (RedisConnectionException)
        {
            return 0;
        }
    }

    public async Task<bool> LockAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.StringSetAsync(key, "locked", expiry, When.NotExists);
        }
        catch (RedisConnectionException)
        {
            return true; // Allow operation if Redis is down
        }
    }

    public async Task ReleaseLockAsync(string key, CancellationToken cancellationToken = default)
    {
        await RemoveAsync(key, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            await _redis.CloseAsync();
            _redis.Dispose();
        }
    }
}

/// <summary>
/// In-memory cache fallback for local development without Redis
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly Timer _cleanupTimer;

    public MemoryCacheService()
    {
        // Clean up expired entries every minute
        _cleanupTimer = new Timer(CleanupExpired, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    private void CleanupExpired(object? state)
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.ExpiresAt.HasValue && kvp.Value.ExpiresAt < now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt.HasValue && entry.ExpiresAt < DateTime.UtcNow)
            {
                _cache.TryRemove(key, out _);
                return Task.FromResult<T?>(default);
            }

            if (entry.Value is T typedValue)
                return Task.FromResult<T?>(typedValue);

            // Try JSON deserialization for complex types
            if (entry.Value is string json)
            {
                try
                {
                    var deserialized = JsonSerializer.Deserialize<T>(json);
                    return Task.FromResult(deserialized);
                }
                catch
                {
                    return Task.FromResult<T?>(default);
                }
            }
        }

        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var entry = new CacheEntry
        {
            Value = value!,
            ExpiresAt = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : null
        };

        _cache[key] = entry;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt.HasValue && entry.ExpiresAt < DateTime.UtcNow)
            {
                _cache.TryRemove(key, out _);
                return Task.FromResult(false);
            }
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var value = await factory();
        await SetAsync(key, value, expiry, cancellationToken);
        return value;
    }

    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        // Convert Redis pattern to regex
        var regexPattern = "^" + pattern.Replace("*", ".*").Replace("?", ".") + "$";
        var regex = new System.Text.RegularExpressions.Regex(regexPattern,
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromSeconds(1));

        var keysToRemove = _cache.Keys.Where(k => regex.IsMatch(k)).ToList();
        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task<long> IncrementAsync(string key, long value = 1, CancellationToken cancellationToken = default)
    {
        var newValue = _cache.AddOrUpdate(
            key,
            new CacheEntry { Value = value },
            (_, existing) =>
            {
                if (existing.Value is long l)
                    return new CacheEntry { Value = l + value, ExpiresAt = existing.ExpiresAt };
                return new CacheEntry { Value = value, ExpiresAt = existing.ExpiresAt };
            });

        return Task.FromResult((long)newValue.Value);
    }

    private readonly ConcurrentDictionary<string, bool> _locks = new();

    public Task<bool> LockAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var acquired = _locks.TryAdd(key, true);
        if (acquired)
        {
            // Auto-release after expiry
            _ = Task.Delay(expiry, cancellationToken).ContinueWith(_ =>
            {
                _locks.TryRemove(key, out _);
            }, TaskScheduler.Default);
        }
        return Task.FromResult(acquired);
    }

    public Task ReleaseLockAsync(string key, CancellationToken cancellationToken = default)
    {
        _locks.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private class CacheEntry
    {
        public required object Value { get; init; }
        public DateTime? ExpiresAt { get; init; }
    }
}

/// <summary>
/// Cached market data service decorator
/// </summary>
public class CachedMarketDataService : IMarketDataService
{
    private readonly IMarketDataService _inner;
    private readonly ICacheService _cache;

    public CachedMarketDataService(IMarketDataService inner, ICacheService cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<Quote?> GetQuoteAsync(string symbol)
    {
        var key = CacheKeys.Quote(symbol);
        return await _cache.GetOrSetAsync(key,
            () => _inner.GetQuoteAsync(symbol),
            CacheTtl.Quote);
    }

    public async Task<List<Quote>> GetQuotesAsync(IEnumerable<string> symbols)
    {
        var symbolList = symbols.ToList();
        var cacheKey = CacheKeys.QuotesBatch(string.Join(",", symbolList.OrderBy(s => s)));

        return await _cache.GetOrSetAsync(cacheKey,
            () => _inner.GetQuotesAsync(symbolList),
            CacheTtl.Quote) ?? new List<Quote>();
    }

    public async Task<List<Bar>> GetHistoricalBarsAsync(string symbol, string timeframe, DateTime start, DateTime end)
    {
        var key = CacheKeys.HistoricalBars(symbol, timeframe,
            start.ToString("yyyyMMdd"), end.ToString("yyyyMMdd"));

        return await _cache.GetOrSetAsync(key,
            () => _inner.GetHistoricalBarsAsync(symbol, timeframe, start, end),
            CacheTtl.HistoricalData) ?? new List<Bar>();
    }

    public Task SubscribeToQuotesAsync(IEnumerable<string> symbols, Action<Quote> onQuote)
    {
        // Real-time subscriptions bypass cache
        return _inner.SubscribeToQuotesAsync(symbols, onQuote);
    }

    public Task UnsubscribeFromQuotesAsync(IEnumerable<string> symbols)
    {
        return _inner.UnsubscribeFromQuotesAsync(symbols);
    }
}

/// <summary>
/// Cached portfolio service decorator
/// </summary>
public class CachedPortfolioService : IPortfolioService
{
    private readonly IPortfolioService _inner;
    private readonly ICacheService _cache;
    private readonly string _userId;

    public CachedPortfolioService(IPortfolioService inner, ICacheService cache, string userId)
    {
        _inner = inner;
        _cache = cache;
        _userId = userId;
    }

    public async Task<List<Position>> GetPositionsAsync()
    {
        var key = CacheKeys.UserPositions(_userId);
        return await _cache.GetOrSetAsync(key,
            () => _inner.GetPositionsAsync(),
            CacheTtl.Positions) ?? new List<Position>();
    }

    public async Task<Position?> GetPositionAsync(string symbol)
    {
        var positions = await GetPositionsAsync();
        return positions.FirstOrDefault(p => p.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<AccountInfo> GetAccountInfoAsync()
    {
        var key = CacheKeys.UserProfile(_userId);
        return await _cache.GetOrSetAsync(key,
            () => _inner.GetAccountInfoAsync(),
            CacheTtl.UserProfile) ?? new AccountInfo();
    }

    public async Task<PortfolioHistory> GetPortfolioHistoryAsync(string period = "1M")
    {
        // Portfolio history can be cached longer
        var key = $"{CacheKeys.UserPositions(_userId)}:history:{period}";
        return await _cache.GetOrSetAsync(key,
            () => _inner.GetPortfolioHistoryAsync(period),
            CacheTtl.HistoricalData) ?? new PortfolioHistory();
    }

    public async Task InvalidateCacheAsync()
    {
        await _cache.RemoveByPatternAsync($"*:user:{_userId}:*");
    }
}
