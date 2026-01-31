using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using OoplesFinance.StockIndicators.Builder.ML.Sentiment;

namespace OoplesFinance.StockIndicators.Builder.DataProviders;

/// <summary>
/// Cache decorator for social sentiment providers.
/// Wraps an ISocialSentimentProvider with L1 in-memory caching with TTL-based expiration.
/// Uses shorter TTLs than news cache since social data is more real-time.
/// </summary>
public sealed class SocialSentimentCacheDecorator : ISocialSentimentProvider
{
    private readonly ISocialSentimentProvider _inner;
    private readonly SocialCacheOptions _options;
    private readonly ConcurrentDictionary<string, CachedSocialItem<IReadOnlyList<SocialPost>>> _postsCache = new();
    private readonly ConcurrentDictionary<string, CachedSocialItem<SocialMetrics>> _metricsCache = new();
    private readonly ConcurrentDictionary<string, CachedSocialItem<IReadOnlyList<TrendingTicker>>> _trendingCache = new();
    private readonly ConcurrentDictionary<string, CachedSocialItem<IReadOnlyDictionary<string, IReadOnlyList<SocialPost>>>> _userPostsCache = new();
    private readonly ConcurrentDictionary<string, SocialPost> _postDeduplicationCache = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    // Cache statistics
    private long _cacheHits;
    private long _cacheMisses;

    /// <summary>
    /// Creates a new cache decorator wrapping the specified provider.
    /// </summary>
    /// <param name="inner">The inner social sentiment provider to wrap.</param>
    /// <param name="options">Cache configuration options.</param>
    public SocialSentimentCacheDecorator(ISocialSentimentProvider inner, SocialCacheOptions? options = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _options = options ?? new SocialCacheOptions();

        // Set up periodic cleanup
        _cleanupTimer = new Timer(
            Cleanup,
            null,
            _options.CleanupInterval,
            _options.CleanupInterval);
    }

    /// <inheritdoc />
    public string ProviderName => $"{_inner.ProviderName} (Cached)";

    /// <inheritdoc />
    public bool IsConnected => _inner.IsConnected;

    /// <summary>Gets the number of cache hits.</summary>
    public long CacheHits => Interlocked.Read(ref _cacheHits);

    /// <summary>Gets the number of cache misses.</summary>
    public long CacheMisses => Interlocked.Read(ref _cacheMisses);

    /// <summary>Gets the cache hit ratio (0.0 to 1.0).</summary>
    public double CacheHitRatio
    {
        get
        {
            var total = CacheHits + CacheMisses;
            return total > 0 ? (double)CacheHits / total : 0.0;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SocialPost>> GetPostsAsync(
        string symbol,
        DateTime? startTime = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = MakePostsCacheKey(symbol, startTime, limit);

        if (TryGetFromPostsCache(cacheKey, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        Interlocked.Increment(ref _cacheMisses);
        var result = await _inner.GetPostsAsync(symbol, startTime, limit, cancellationToken)
            .ConfigureAwait(false);

        CachePostsResult(cacheKey, result, _options.PostsTtl);
        CachePostsForDeduplication(result);

        return result;
    }

    /// <inheritdoc />
    public async Task<SocialMetrics> GetAggregatedMetricsAsync(
        string symbol,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = MakeMetricsCacheKey(symbol, window);

        if (TryGetFromMetricsCache(cacheKey, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        Interlocked.Increment(ref _cacheMisses);
        var result = await _inner.GetAggregatedMetricsAsync(symbol, window, cancellationToken)
            .ConfigureAwait(false);

        CacheMetricsResult(cacheKey, result, _options.MetricsTtl);

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TrendingTicker>> GetTrendingTickersAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"trending_{limit}";

        if (TryGetFromTrendingCache(cacheKey, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        Interlocked.Increment(ref _cacheMisses);
        var result = await _inner.GetTrendingTickersAsync(limit, cancellationToken)
            .ConfigureAwait(false);

        CacheTrendingResult(cacheKey, result, _options.TrendingTtl);

        return result;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SocialPost> StreamPostsAsync(
        IEnumerable<string> symbols,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Streaming cannot be cached, but we can deduplicate
        var seen = new HashSet<string>();

        await foreach (var post in _inner.StreamPostsAsync(symbols, cancellationToken))
        {
            var postKey = GetPostDeduplicationKey(post);

            // Deduplicate within the stream
            if (!seen.Add(postKey))
            {
                continue;
            }

            // Check if we've seen this post before (from any source)
            if (_options.DeduplicateStreams && _postDeduplicationCache.ContainsKey(postKey))
            {
                continue;
            }

            // Cache the post for deduplication
            CachePostForDeduplication(post);

            yield return post;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<SocialPost>>> GetUserPostsAsync(
        IEnumerable<string> usernames,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var usernameList = usernames.ToList();
        var cacheKey = MakeUserPostsCacheKey(usernameList, limit);

        if (TryGetFromUserPostsCache(cacheKey, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        Interlocked.Increment(ref _cacheMisses);
        var result = await _inner.GetUserPostsAsync(usernameList, limit, cancellationToken)
            .ConfigureAwait(false);

        CacheUserPostsResult(cacheKey, result, _options.UserPostsTtl);

        // Cache individual posts for deduplication
        foreach (var kvp in result)
        {
            CachePostsForDeduplication(kvp.Value);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SocialPost>> SearchPostsAsync(
        string query,
        DateTime? startTime = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = MakeSearchCacheKey(query, startTime, limit);

        if (TryGetFromPostsCache(cacheKey, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        Interlocked.Increment(ref _cacheMisses);
        var result = await _inner.SearchPostsAsync(query, startTime, limit, cancellationToken)
            .ConfigureAwait(false);

        CachePostsResult(cacheKey, result, _options.SearchTtl);
        CachePostsForDeduplication(result);

        return result;
    }

    /// <summary>
    /// Gets a cached post by its deduplication key if available.
    /// </summary>
    /// <param name="postKey">The post deduplication key.</param>
    /// <returns>The cached post, or null if not in cache.</returns>
    public SocialPost? GetCachedPost(string postKey)
    {
        if (_postDeduplicationCache.TryGetValue(postKey, out var post))
        {
            return post;
        }
        return null;
    }

    /// <summary>
    /// Clears all cached data.
    /// </summary>
    public void ClearCache()
    {
        _postsCache.Clear();
        _metricsCache.Clear();
        _trendingCache.Clear();
        _userPostsCache.Clear();
        _postDeduplicationCache.Clear();
        Interlocked.Exchange(ref _cacheHits, 0);
        Interlocked.Exchange(ref _cacheMisses, 0);
    }

    /// <summary>
    /// Clears cached data for a specific symbol.
    /// </summary>
    /// <param name="symbol">The symbol to clear from cache.</param>
    public void ClearSymbolCache(string symbol)
    {
        // Clear posts cache entries for this symbol
        var postsKeysToRemove = _postsCache.Keys
            .Where(k => k.Contains($"_{symbol}_") || k.StartsWith($"posts_{symbol}_"))
            .ToList();

        foreach (var key in postsKeysToRemove)
        {
            _postsCache.TryRemove(key, out _);
        }

        // Clear metrics cache for this symbol
        var metricsKeysToRemove = _metricsCache.Keys
            .Where(k => k.StartsWith($"metrics_{symbol}_"))
            .ToList();

        foreach (var key in metricsKeysToRemove)
        {
            _metricsCache.TryRemove(key, out _);
        }

        // Clear posts that mention this symbol from deduplication cache
        var postKeysToRemove = _postDeduplicationCache
            .Where(kvp => kvp.Value.Cashtags.Contains(symbol) || kvp.Value.Cashtags.Contains($"${symbol}"))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in postKeysToRemove)
        {
            _postDeduplicationCache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public SocialCacheStatistics GetStatistics()
    {
        var expiredPosts = _postsCache.Values.Count(c => c.IsExpired);
        var expiredMetrics = _metricsCache.Values.Count(c => c.IsExpired);
        var expiredTrending = _trendingCache.Values.Count(c => c.IsExpired);
        var expiredUserPosts = _userPostsCache.Values.Count(c => c.IsExpired);

        return new SocialCacheStatistics
        {
            PostsQueryCount = _postsCache.Count,
            MetricsQueryCount = _metricsCache.Count,
            TrendingQueryCount = _trendingCache.Count,
            UserPostsQueryCount = _userPostsCache.Count,
            DeduplicationCacheCount = _postDeduplicationCache.Count,
            ExpiredPostsQueryCount = expiredPosts,
            ExpiredMetricsQueryCount = expiredMetrics,
            ExpiredTrendingQueryCount = expiredTrending,
            ExpiredUserPostsQueryCount = expiredUserPosts,
            CacheHits = CacheHits,
            CacheMisses = CacheMisses,
            CacheHitRatio = CacheHitRatio
        };
    }

    private bool TryGetFromPostsCache(string key, out IReadOnlyList<SocialPost> result)
    {
        if (_postsCache.TryGetValue(key, out var cached) && !cached.IsExpired)
        {
            result = cached.Value;
            return true;
        }
        result = Array.Empty<SocialPost>();
        return false;
    }

    private bool TryGetFromMetricsCache(string key, out SocialMetrics result)
    {
        if (_metricsCache.TryGetValue(key, out var cached) && !cached.IsExpired)
        {
            result = cached.Value;
            return true;
        }
        result = new SocialMetrics();
        return false;
    }

    private bool TryGetFromTrendingCache(string key, out IReadOnlyList<TrendingTicker> result)
    {
        if (_trendingCache.TryGetValue(key, out var cached) && !cached.IsExpired)
        {
            result = cached.Value;
            return true;
        }
        result = Array.Empty<TrendingTicker>();
        return false;
    }

    private bool TryGetFromUserPostsCache(string key, out IReadOnlyDictionary<string, IReadOnlyList<SocialPost>> result)
    {
        if (_userPostsCache.TryGetValue(key, out var cached) && !cached.IsExpired)
        {
            result = cached.Value;
            return true;
        }
        result = new Dictionary<string, IReadOnlyList<SocialPost>>();
        return false;
    }

    private void CachePostsResult(string key, IReadOnlyList<SocialPost> result, TimeSpan ttl)
    {
        if (_options.MaxCacheEntries > 0 && _postsCache.Count >= _options.MaxCacheEntries)
        {
            EvictOldestEntries(_postsCache, _options.MaxCacheEntries / 4);
        }

        _postsCache[key] = new CachedSocialItem<IReadOnlyList<SocialPost>>(result, ttl);
    }

    private void CacheMetricsResult(string key, SocialMetrics result, TimeSpan ttl)
    {
        if (_options.MaxCacheEntries > 0 && _metricsCache.Count >= _options.MaxCacheEntries)
        {
            EvictOldestEntries(_metricsCache, _options.MaxCacheEntries / 4);
        }

        _metricsCache[key] = new CachedSocialItem<SocialMetrics>(result, ttl);
    }

    private void CacheTrendingResult(string key, IReadOnlyList<TrendingTicker> result, TimeSpan ttl)
    {
        _trendingCache[key] = new CachedSocialItem<IReadOnlyList<TrendingTicker>>(result, ttl);
    }

    private void CacheUserPostsResult(string key, IReadOnlyDictionary<string, IReadOnlyList<SocialPost>> result, TimeSpan ttl)
    {
        _userPostsCache[key] = new CachedSocialItem<IReadOnlyDictionary<string, IReadOnlyList<SocialPost>>>(result, ttl);
    }

    private void CachePostsForDeduplication(IReadOnlyList<SocialPost> posts)
    {
        if (!_options.CachePostsForDeduplication)
            return;

        foreach (var post in posts)
        {
            CachePostForDeduplication(post);
        }
    }

    private void CachePostForDeduplication(SocialPost post)
    {
        if (_options.MaxDeduplicationCacheSize > 0 && _postDeduplicationCache.Count >= _options.MaxDeduplicationCacheSize)
        {
            EvictOldestPosts(_options.MaxDeduplicationCacheSize / 4);
        }

        var key = GetPostDeduplicationKey(post);
        _postDeduplicationCache.TryAdd(key, post);
    }

    private static string GetPostDeduplicationKey(SocialPost post)
    {
        // Use platform + username + content hash + approximate time
        var timeKey = post.Timestamp.ToString("yyyyMMddHHmm");
        return $"{post.Platform}_{post.Username}_{post.Content.GetHashCode()}_{timeKey}";
    }

    private static string MakePostsCacheKey(string symbol, DateTime? startTime, int limit)
    {
        var startKey = startTime?.ToString("yyyyMMddHHmm") ?? "none";
        return $"posts_{symbol}_{startKey}_{limit}";
    }

    private static string MakeMetricsCacheKey(string symbol, TimeSpan window)
    {
        return $"metrics_{symbol}_{window.TotalMinutes:F0}m";
    }

    private static string MakeUserPostsCacheKey(IEnumerable<string> usernames, int limit)
    {
        var usernameKey = string.Join(",", usernames.OrderBy(u => u));
        return $"userposts_{usernameKey.GetHashCode()}_{limit}";
    }

    private static string MakeSearchCacheKey(string query, DateTime? startTime, int limit)
    {
        var queryHash = query.ToLowerInvariant().GetHashCode();
        var startKey = startTime?.ToString("yyyyMMddHHmm") ?? "none";
        return $"search_{queryHash}_{startKey}_{limit}";
    }

    private void Cleanup(object? state)
    {
        // Remove expired entries
        RemoveExpired(_postsCache);
        RemoveExpired(_metricsCache);
        RemoveExpired(_trendingCache);
        RemoveExpired(_userPostsCache);

        // Limit deduplication cache size
        if (_options.MaxDeduplicationCacheSize > 0 && _postDeduplicationCache.Count > _options.MaxDeduplicationCacheSize)
        {
            EvictOldestPosts(_postDeduplicationCache.Count - _options.MaxDeduplicationCacheSize);
        }
    }

    private static void RemoveExpired<T>(ConcurrentDictionary<string, CachedSocialItem<T>> cache)
    {
        var expiredKeys = cache
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            cache.TryRemove(key, out _);
        }
    }

    private static void EvictOldestEntries<T>(ConcurrentDictionary<string, CachedSocialItem<T>> cache, int count)
    {
        var oldestKeys = cache
            .OrderBy(kvp => kvp.Value.CachedAt)
            .Take(count)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in oldestKeys)
        {
            cache.TryRemove(key, out _);
        }
    }

    private void EvictOldestPosts(int count)
    {
        var oldestKeys = _postDeduplicationCache
            .OrderBy(kvp => kvp.Value.Timestamp)
            .Take(count)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in oldestKeys)
        {
            _postDeduplicationCache.TryRemove(key, out _);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _cleanupTimer.Dispose();
            _inner.Dispose();
            ClearCache();
            _disposed = true;
        }
    }
}

/// <summary>
/// Wrapper for cached social items with expiration.
/// </summary>
/// <typeparam name="T">The type of cached item.</typeparam>
internal readonly struct CachedSocialItem<T>
{
    /// <summary>
    /// Creates a new cached item.
    /// </summary>
    public CachedSocialItem(T value, TimeSpan ttl)
    {
        Value = value;
        CachedAt = DateTime.UtcNow;
        ExpiresAt = CachedAt + ttl;
    }

    /// <summary>Gets the cached value.</summary>
    public T Value { get; }

    /// <summary>Gets when this item was cached.</summary>
    public DateTime CachedAt { get; }

    /// <summary>Gets the expiration time.</summary>
    public DateTime ExpiresAt { get; }

    /// <summary>Gets whether this item has expired.</summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}

/// <summary>
/// Configuration options for social sentiment cache.
/// </summary>
public sealed class SocialCacheOptions
{
    /// <summary>Gets or sets the TTL for posts queries (shorter since social data is real-time).</summary>
    public TimeSpan PostsTtl { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the TTL for aggregated metrics.</summary>
    public TimeSpan MetricsTtl { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets the TTL for trending tickers (changes frequently).</summary>
    public TimeSpan TrendingTtl { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the TTL for user posts queries.</summary>
    public TimeSpan UserPostsTtl { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets the TTL for search results.</summary>
    public TimeSpan SearchTtl { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the cleanup interval for expired entries.</summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the maximum number of cached query results (0 for unlimited).</summary>
    public int MaxCacheEntries { get; set; } = 500;

    /// <summary>Gets or sets the maximum size of the deduplication cache (0 for unlimited).</summary>
    public int MaxDeduplicationCacheSize { get; set; } = 5000;

    /// <summary>Gets or sets whether to cache posts for deduplication.</summary>
    public bool CachePostsForDeduplication { get; set; } = true;

    /// <summary>Gets or sets whether to deduplicate streaming results against cache.</summary>
    public bool DeduplicateStreams { get; set; } = true;
}

/// <summary>
/// Cache statistics for monitoring.
/// </summary>
public sealed class SocialCacheStatistics
{
    /// <summary>Gets or sets the number of cached posts query results.</summary>
    public int PostsQueryCount { get; set; }

    /// <summary>Gets or sets the number of cached metrics query results.</summary>
    public int MetricsQueryCount { get; set; }

    /// <summary>Gets or sets the number of cached trending query results.</summary>
    public int TrendingQueryCount { get; set; }

    /// <summary>Gets or sets the number of cached user posts query results.</summary>
    public int UserPostsQueryCount { get; set; }

    /// <summary>Gets or sets the number of posts in deduplication cache.</summary>
    public int DeduplicationCacheCount { get; set; }

    /// <summary>Gets or sets the number of expired posts query entries.</summary>
    public int ExpiredPostsQueryCount { get; set; }

    /// <summary>Gets or sets the number of expired metrics query entries.</summary>
    public int ExpiredMetricsQueryCount { get; set; }

    /// <summary>Gets or sets the number of expired trending query entries.</summary>
    public int ExpiredTrendingQueryCount { get; set; }

    /// <summary>Gets or sets the number of expired user posts query entries.</summary>
    public int ExpiredUserPostsQueryCount { get; set; }

    /// <summary>Gets or sets the total cache hits.</summary>
    public long CacheHits { get; set; }

    /// <summary>Gets or sets the total cache misses.</summary>
    public long CacheMisses { get; set; }

    /// <summary>Gets or sets the cache hit ratio (0.0 to 1.0).</summary>
    public double CacheHitRatio { get; set; }

    /// <summary>Gets the total number of cached query items.</summary>
    public int TotalQueryCount => PostsQueryCount + MetricsQueryCount + TrendingQueryCount + UserPostsQueryCount;

    /// <summary>Gets the total number of expired query items.</summary>
    public int TotalExpiredCount => ExpiredPostsQueryCount + ExpiredMetricsQueryCount + ExpiredTrendingQueryCount + ExpiredUserPostsQueryCount;
}
