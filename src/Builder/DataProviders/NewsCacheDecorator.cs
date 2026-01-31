using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace OoplesFinance.StockIndicators.Builder.DataProviders;

/// <summary>
/// Cache decorator for news providers.
/// Wraps an INewsProvider with L1 in-memory caching with TTL-based expiration.
/// Supports deduplication and automatic cache cleanup.
/// </summary>
public sealed class NewsCacheDecorator : INewsProvider
{
    private readonly INewsProvider _inner;
    private readonly NewsCacheOptions _options;
    private readonly ConcurrentDictionary<string, CachedNewsItem<IReadOnlyList<NewsArticleData>>> _newsCache = new();
    private readonly ConcurrentDictionary<string, CachedNewsItem<IReadOnlyDictionary<string, IReadOnlyList<NewsArticleData>>>> _multiSymbolCache = new();
    private readonly ConcurrentDictionary<string, NewsArticleData> _articleCache = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    // Cache statistics
    private long _cacheHits;
    private long _cacheMisses;

    /// <summary>
    /// Creates a new cache decorator wrapping the specified provider.
    /// </summary>
    /// <param name="inner">The inner news provider to wrap.</param>
    /// <param name="options">Cache configuration options.</param>
    public NewsCacheDecorator(INewsProvider inner, NewsCacheOptions? options = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _options = options ?? new NewsCacheOptions();

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
    public async Task<IReadOnlyList<NewsArticleData>> GetNewsAsync(
        string symbol,
        DateTime startDate,
        DateTime endDate,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = MakeCacheKey("news", symbol, startDate, endDate, limit);

        if (TryGetFromCache(cacheKey, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        Interlocked.Increment(ref _cacheMisses);
        var result = await _inner.GetNewsAsync(symbol, startDate, endDate, limit, cancellationToken)
            .ConfigureAwait(false);

        CacheResult(cacheKey, result, _options.NewsTtl);
        CacheArticles(result);

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NewsArticleData>> GetLatestNewsAsync(
        string symbol,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = MakeCacheKey("latest", symbol, limit);

        if (TryGetFromCache(cacheKey, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        Interlocked.Increment(ref _cacheMisses);
        var result = await _inner.GetLatestNewsAsync(symbol, limit, cancellationToken)
            .ConfigureAwait(false);

        // Latest news has shorter TTL since it changes frequently
        CacheResult(cacheKey, result, _options.LatestNewsTtl);
        CacheArticles(result);

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<NewsArticleData>>> GetLatestNewsAsync(
        IEnumerable<string> symbols,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var symbolList = symbols.ToList();
        var cacheKey = MakeMultiSymbolCacheKey("latest_multi", symbolList, limit);

        if (TryGetFromMultiCache(cacheKey, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        Interlocked.Increment(ref _cacheMisses);
        var result = await _inner.GetLatestNewsAsync(symbolList, limit, cancellationToken)
            .ConfigureAwait(false);

        CacheMultiResult(cacheKey, result, _options.LatestNewsTtl);

        // Also cache individual symbol results
        foreach (var kvp in result)
        {
            var singleKey = MakeCacheKey("latest", kvp.Key, limit);
            CacheResult(singleKey, kvp.Value, _options.LatestNewsTtl);
            CacheArticles(kvp.Value);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NewsArticleData>> SearchNewsAsync(
        string query,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = MakeSearchCacheKey(query, startDate, endDate, limit);

        if (TryGetFromCache(cacheKey, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        Interlocked.Increment(ref _cacheMisses);
        var result = await _inner.SearchNewsAsync(query, startDate, endDate, limit, cancellationToken)
            .ConfigureAwait(false);

        CacheResult(cacheKey, result, _options.SearchTtl);
        CacheArticles(result);

        return result;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<NewsArticleData> StreamNewsAsync(
        IEnumerable<string> symbols,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Streaming cannot be cached, but we can deduplicate and cache individual articles
        var seen = new HashSet<string>();

        await foreach (var article in _inner.StreamNewsAsync(symbols, cancellationToken))
        {
            var articleKey = GetArticleKey(article);

            // Deduplicate within the stream
            if (!seen.Add(articleKey))
            {
                continue;
            }

            // Check if we've seen this article before (from any source)
            if (_options.DeduplicateStreams && _articleCache.ContainsKey(articleKey))
            {
                continue;
            }

            // Cache the article for deduplication
            CacheArticle(article);

            yield return article;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NewsArticleData>> GetMarketNewsAsync(
        NewsCategory? category = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = MakeCacheKey("market", category?.ToString() ?? "all", limit);

        if (TryGetFromCache(cacheKey, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        Interlocked.Increment(ref _cacheMisses);
        var result = await _inner.GetMarketNewsAsync(category, limit, cancellationToken)
            .ConfigureAwait(false);

        CacheResult(cacheKey, result, _options.MarketNewsTtl);
        CacheArticles(result);

        return result;
    }

    /// <summary>
    /// Gets a cached article by its ID if available.
    /// </summary>
    /// <param name="articleId">The article ID.</param>
    /// <returns>The cached article, or null if not in cache.</returns>
    public NewsArticleData? GetCachedArticle(string articleId)
    {
        if (_articleCache.TryGetValue(articleId, out var article))
        {
            return article;
        }
        return null;
    }

    /// <summary>
    /// Clears all cached data.
    /// </summary>
    public void ClearCache()
    {
        _newsCache.Clear();
        _multiSymbolCache.Clear();
        _articleCache.Clear();
        Interlocked.Exchange(ref _cacheHits, 0);
        Interlocked.Exchange(ref _cacheMisses, 0);
    }

    /// <summary>
    /// Clears cached data for a specific symbol.
    /// </summary>
    /// <param name="symbol">The symbol to clear from cache.</param>
    public void ClearSymbolCache(string symbol)
    {
        var keysToRemove = _newsCache.Keys
            .Where(k => k.Contains($"_{symbol}_") || k.EndsWith($"_{symbol}"))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _newsCache.TryRemove(key, out _);
        }

        // Clear articles that mention this symbol
        var articleKeysToRemove = _articleCache
            .Where(kvp => kvp.Value.Symbols.Contains(symbol))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in articleKeysToRemove)
        {
            _articleCache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public NewsCacheStatistics GetStatistics()
    {
        var expiredNews = _newsCache.Values.Count(c => c.IsExpired);
        var expiredMulti = _multiSymbolCache.Values.Count(c => c.IsExpired);

        return new NewsCacheStatistics
        {
            NewsQueryCount = _newsCache.Count,
            MultiSymbolQueryCount = _multiSymbolCache.Count,
            ArticleCount = _articleCache.Count,
            ExpiredNewsQueryCount = expiredNews,
            ExpiredMultiSymbolQueryCount = expiredMulti,
            CacheHits = CacheHits,
            CacheMisses = CacheMisses,
            CacheHitRatio = CacheHitRatio
        };
    }

    private bool TryGetFromCache(string key, out IReadOnlyList<NewsArticleData> result)
    {
        if (_newsCache.TryGetValue(key, out var cached) && !cached.IsExpired)
        {
            result = cached.Value;
            return true;
        }
        result = Array.Empty<NewsArticleData>();
        return false;
    }

    private bool TryGetFromMultiCache(string key, out IReadOnlyDictionary<string, IReadOnlyList<NewsArticleData>> result)
    {
        if (_multiSymbolCache.TryGetValue(key, out var cached) && !cached.IsExpired)
        {
            result = cached.Value;
            return true;
        }
        result = new Dictionary<string, IReadOnlyList<NewsArticleData>>();
        return false;
    }

    private void CacheResult(string key, IReadOnlyList<NewsArticleData> result, TimeSpan ttl)
    {
        if (_options.MaxCacheEntries > 0 && _newsCache.Count >= _options.MaxCacheEntries)
        {
            // Evict oldest entries if cache is full
            EvictOldestEntries(_newsCache, _options.MaxCacheEntries / 4);
        }

        _newsCache[key] = new CachedNewsItem<IReadOnlyList<NewsArticleData>>(result, ttl);
    }

    private void CacheMultiResult(string key, IReadOnlyDictionary<string, IReadOnlyList<NewsArticleData>> result, TimeSpan ttl)
    {
        _multiSymbolCache[key] = new CachedNewsItem<IReadOnlyDictionary<string, IReadOnlyList<NewsArticleData>>>(result, ttl);
    }

    private void CacheArticles(IReadOnlyList<NewsArticleData> articles)
    {
        if (!_options.CacheIndividualArticles)
            return;

        foreach (var article in articles)
        {
            CacheArticle(article);
        }
    }

    private void CacheArticle(NewsArticleData article)
    {
        if (_options.MaxArticlesCached > 0 && _articleCache.Count >= _options.MaxArticlesCached)
        {
            // Evict oldest articles
            EvictOldestArticles(_options.MaxArticlesCached / 4);
        }

        var key = GetArticleKey(article);
        _articleCache.TryAdd(key, article);
    }

    private static string GetArticleKey(NewsArticleData article)
    {
        // Use ID if available, otherwise use title + source + time hash
        if (!string.IsNullOrEmpty(article.Id))
            return $"{article.Provider}_{article.Id}";

        var titleHash = article.Title.ToLowerInvariant().GetHashCode();
        var timeKey = article.PublishedAt.ToString("yyyyMMddHHmm");
        return $"{article.Provider}_{titleHash}_{article.Source}_{timeKey}";
    }

    private static string MakeCacheKey(string prefix, string symbol, DateTime start, DateTime end, int limit)
    {
        return $"{prefix}_{symbol}_{start:yyyyMMdd}_{end:yyyyMMdd}_{limit}";
    }

    private static string MakeCacheKey(string prefix, string symbol, int limit)
    {
        return $"{prefix}_{symbol}_{limit}";
    }

    private static string MakeMultiSymbolCacheKey(string prefix, IEnumerable<string> symbols, int limit)
    {
        var symbolKey = string.Join(",", symbols.OrderBy(s => s));
        return $"{prefix}_{symbolKey.GetHashCode()}_{limit}";
    }

    private static string MakeSearchCacheKey(string query, DateTime? start, DateTime? end, int limit)
    {
        var queryHash = query.ToLowerInvariant().GetHashCode();
        var startKey = start?.ToString("yyyyMMdd") ?? "none";
        var endKey = end?.ToString("yyyyMMdd") ?? "none";
        return $"search_{queryHash}_{startKey}_{endKey}_{limit}";
    }

    private void Cleanup(object? state)
    {
        // Remove expired entries
        RemoveExpired(_newsCache);
        RemoveExpired(_multiSymbolCache);

        // Limit article cache size
        if (_options.MaxArticlesCached > 0 && _articleCache.Count > _options.MaxArticlesCached)
        {
            EvictOldestArticles(_articleCache.Count - _options.MaxArticlesCached);
        }
    }

    private static void RemoveExpired<T>(ConcurrentDictionary<string, CachedNewsItem<T>> cache)
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

    private static void EvictOldestEntries<T>(ConcurrentDictionary<string, CachedNewsItem<T>> cache, int count)
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

    private void EvictOldestArticles(int count)
    {
        var oldestKeys = _articleCache
            .OrderBy(kvp => kvp.Value.FetchedAt)
            .Take(count)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in oldestKeys)
        {
            _articleCache.TryRemove(key, out _);
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
/// Wrapper for cached news items with expiration.
/// </summary>
/// <typeparam name="T">The type of cached item.</typeparam>
internal readonly struct CachedNewsItem<T>
{
    /// <summary>
    /// Creates a new cached item.
    /// </summary>
    public CachedNewsItem(T value, TimeSpan ttl)
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
/// Configuration options for news cache.
/// </summary>
public sealed class NewsCacheOptions
{
    /// <summary>Gets or sets the TTL for historical news queries.</summary>
    public TimeSpan NewsTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets the TTL for latest news queries (shorter since it changes frequently).</summary>
    public TimeSpan LatestNewsTtl { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets the TTL for search results.</summary>
    public TimeSpan SearchTtl { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>Gets or sets the TTL for market news queries.</summary>
    public TimeSpan MarketNewsTtl { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Gets or sets the cleanup interval for expired entries.</summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets the maximum number of cached query results (0 for unlimited).</summary>
    public int MaxCacheEntries { get; set; } = 1000;

    /// <summary>Gets or sets the maximum number of individual articles to cache (0 for unlimited).</summary>
    public int MaxArticlesCached { get; set; } = 10000;

    /// <summary>Gets or sets whether to cache individual articles for deduplication.</summary>
    public bool CacheIndividualArticles { get; set; } = true;

    /// <summary>Gets or sets whether to deduplicate streaming results against cache.</summary>
    public bool DeduplicateStreams { get; set; } = true;
}

/// <summary>
/// Cache statistics for monitoring.
/// </summary>
public sealed class NewsCacheStatistics
{
    /// <summary>Gets or sets the number of cached news query results.</summary>
    public int NewsQueryCount { get; set; }

    /// <summary>Gets or sets the number of cached multi-symbol query results.</summary>
    public int MultiSymbolQueryCount { get; set; }

    /// <summary>Gets or sets the number of cached individual articles.</summary>
    public int ArticleCount { get; set; }

    /// <summary>Gets or sets the number of expired news query entries.</summary>
    public int ExpiredNewsQueryCount { get; set; }

    /// <summary>Gets or sets the number of expired multi-symbol query entries.</summary>
    public int ExpiredMultiSymbolQueryCount { get; set; }

    /// <summary>Gets or sets the total cache hits.</summary>
    public long CacheHits { get; set; }

    /// <summary>Gets or sets the total cache misses.</summary>
    public long CacheMisses { get; set; }

    /// <summary>Gets or sets the cache hit ratio (0.0 to 1.0).</summary>
    public double CacheHitRatio { get; set; }

    /// <summary>Gets the total number of cached items.</summary>
    public int TotalCount => NewsQueryCount + MultiSymbolQueryCount + ArticleCount;

    /// <summary>Gets the total number of expired items.</summary>
    public int TotalExpiredCount => ExpiredNewsQueryCount + ExpiredMultiSymbolQueryCount;
}
