using System.Collections.Concurrent;

namespace OoplesFinance.StockIndicators.Builder.MarketData;

/// <summary>
/// Thread-safe cache for market data with configurable TTL.
/// Reduces API calls by caching quotes, trades, and bars.
/// </summary>
public sealed class MarketDataCache
{
    private readonly ConcurrentDictionary<string, CachedItem<Quote>> _quoteCache = new();
    private readonly ConcurrentDictionary<string, CachedItem<Trade>> _tradeCache = new();
    private readonly ConcurrentDictionary<string, CachedItem<Level2OrderBook>> _orderBookCache = new();
    private readonly ConcurrentDictionary<string, CachedItem<IReadOnlyList<Bar>>> _barCache = new();
    private readonly TimeSpan _quoteTtl;
    private readonly TimeSpan _tradeTtl;
    private readonly TimeSpan _orderBookTtl;
    private readonly TimeSpan _barTtl;
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    /// <summary>
    /// Creates a new market data cache with default TTLs.
    /// </summary>
    public MarketDataCache()
        : this(
            quoteTtl: TimeSpan.FromSeconds(1),
            tradeTtl: TimeSpan.FromSeconds(1),
            orderBookTtl: TimeSpan.FromMilliseconds(500),
            barTtl: TimeSpan.FromSeconds(5))
    {
    }

    /// <summary>
    /// Creates a new market data cache with custom TTLs.
    /// </summary>
    /// <param name="quoteTtl">TTL for quote data.</param>
    /// <param name="tradeTtl">TTL for trade data.</param>
    /// <param name="orderBookTtl">TTL for order book data.</param>
    /// <param name="barTtl">TTL for bar data.</param>
    public MarketDataCache(
        TimeSpan quoteTtl,
        TimeSpan tradeTtl,
        TimeSpan orderBookTtl,
        TimeSpan barTtl)
    {
        _quoteTtl = quoteTtl;
        _tradeTtl = tradeTtl;
        _orderBookTtl = orderBookTtl;
        _barTtl = barTtl;

        // Set up periodic cleanup
        _cleanupTimer = new Timer(Cleanup, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Gets a cached quote for a symbol.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The cached quote, or null if not cached or expired.</returns>
    public Quote? GetQuote(string symbol)
    {
        if (_quoteCache.TryGetValue(symbol, out var cached) && !cached.IsExpired)
        {
            return cached.Value;
        }
        return null;
    }

    /// <summary>
    /// Caches a quote.
    /// </summary>
    /// <param name="quote">The quote to cache.</param>
    public void SetQuote(Quote quote)
    {
        _quoteCache[quote.Symbol] = new CachedItem<Quote>(quote, _quoteTtl);
    }

    /// <summary>
    /// Gets cached quotes for multiple symbols.
    /// </summary>
    /// <param name="symbols">The symbols.</param>
    /// <returns>Dictionary of symbol to quote for cached items.</returns>
    public IReadOnlyDictionary<string, Quote> GetQuotes(IEnumerable<string> symbols)
    {
        var result = new Dictionary<string, Quote>();
        foreach (var symbol in symbols)
        {
            var quote = GetQuote(symbol);
            if (quote is not null)
            {
                result[symbol] = quote;
            }
        }
        return result;
    }

    /// <summary>
    /// Gets a cached trade for a symbol.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The cached trade, or null if not cached or expired.</returns>
    public Trade? GetTrade(string symbol)
    {
        if (_tradeCache.TryGetValue(symbol, out var cached) && !cached.IsExpired)
        {
            return cached.Value;
        }
        return null;
    }

    /// <summary>
    /// Caches a trade.
    /// </summary>
    /// <param name="trade">The trade to cache.</param>
    public void SetTrade(Trade trade)
    {
        _tradeCache[trade.Symbol] = new CachedItem<Trade>(trade, _tradeTtl);
    }

    /// <summary>
    /// Gets a cached order book for a symbol.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The cached order book, or null if not cached or expired.</returns>
    public Level2OrderBook? GetOrderBook(string symbol)
    {
        if (_orderBookCache.TryGetValue(symbol, out var cached) && !cached.IsExpired)
        {
            return cached.Value;
        }
        return null;
    }

    /// <summary>
    /// Caches an order book.
    /// </summary>
    /// <param name="orderBook">The order book to cache.</param>
    public void SetOrderBook(Level2OrderBook orderBook)
    {
        _orderBookCache[orderBook.Symbol] = new CachedItem<Level2OrderBook>(orderBook, _orderBookTtl);
    }

    /// <summary>
    /// Gets cached bars for a symbol and date range.
    /// </summary>
    /// <param name="cacheKey">The cache key (symbol + timeframe + dates).</param>
    /// <returns>The cached bars, or null if not cached or expired.</returns>
    public IReadOnlyList<Bar>? GetBars(string cacheKey)
    {
        if (_barCache.TryGetValue(cacheKey, out var cached) && !cached.IsExpired)
        {
            return cached.Value;
        }
        return null;
    }

    /// <summary>
    /// Caches bars.
    /// </summary>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="bars">The bars to cache.</param>
    public void SetBars(string cacheKey, IReadOnlyList<Bar> bars)
    {
        _barCache[cacheKey] = new CachedItem<IReadOnlyList<Bar>>(bars, _barTtl);
    }

    /// <summary>
    /// Creates a cache key for bar data.
    /// </summary>
    public static string MakeBarCacheKey(string symbol, DateTime start, DateTime end, BarTimeframe timeframe)
    {
        return $"{symbol}_{timeframe}_{start:yyyyMMddHHmm}_{end:yyyyMMddHHmm}";
    }

    /// <summary>
    /// Clears all cached data.
    /// </summary>
    public void Clear()
    {
        _quoteCache.Clear();
        _tradeCache.Clear();
        _orderBookCache.Clear();
        _barCache.Clear();
    }

    /// <summary>
    /// Clears cached data for a specific symbol.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    public void ClearSymbol(string symbol)
    {
        _quoteCache.TryRemove(symbol, out _);
        _tradeCache.TryRemove(symbol, out _);
        _orderBookCache.TryRemove(symbol, out _);

        // Clear bar cache entries for this symbol
        var keysToRemove = _barCache.Keys.Where(k => k.StartsWith(symbol + "_")).ToList();
        foreach (var key in keysToRemove)
        {
            _barCache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            QuoteCount = _quoteCache.Count,
            TradeCount = _tradeCache.Count,
            OrderBookCount = _orderBookCache.Count,
            BarCacheCount = _barCache.Count,
            QuoteExpiredCount = _quoteCache.Values.Count(c => c.IsExpired),
            TradeExpiredCount = _tradeCache.Values.Count(c => c.IsExpired),
            OrderBookExpiredCount = _orderBookCache.Values.Count(c => c.IsExpired),
            BarExpiredCount = _barCache.Values.Count(c => c.IsExpired)
        };
    }

    private void Cleanup(object? state)
    {
        // Remove expired entries
        RemoveExpired(_quoteCache);
        RemoveExpired(_tradeCache);
        RemoveExpired(_orderBookCache);
        RemoveExpired(_barCache);
    }

    private static void RemoveExpired<T>(ConcurrentDictionary<string, CachedItem<T>> cache)
    {
        var expiredKeys = cache.Where(kvp => kvp.Value.IsExpired).Select(kvp => kvp.Key).ToList();
        foreach (var key in expiredKeys)
        {
            cache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Disposes the cache and stops cleanup timer.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _cleanupTimer.Dispose();
            Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// Wrapper for cached items with expiration.
/// </summary>
/// <typeparam name="T">The type of cached item.</typeparam>
internal readonly struct CachedItem<T>
{
    /// <summary>
    /// Creates a new cached item.
    /// </summary>
    public CachedItem(T value, TimeSpan ttl)
    {
        Value = value;
        ExpiresAt = DateTime.UtcNow + ttl;
    }

    /// <summary>Gets the cached value.</summary>
    public T Value { get; }

    /// <summary>Gets the expiration time.</summary>
    public DateTime ExpiresAt { get; }

    /// <summary>Gets whether this item has expired.</summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}

/// <summary>
/// Cache statistics for monitoring.
/// </summary>
public sealed class CacheStatistics
{
    /// <summary>Gets or sets the number of cached quotes.</summary>
    public int QuoteCount { get; set; }

    /// <summary>Gets or sets the number of cached trades.</summary>
    public int TradeCount { get; set; }

    /// <summary>Gets or sets the number of cached order books.</summary>
    public int OrderBookCount { get; set; }

    /// <summary>Gets or sets the number of cached bar sets.</summary>
    public int BarCacheCount { get; set; }

    /// <summary>Gets or sets the number of expired quotes.</summary>
    public int QuoteExpiredCount { get; set; }

    /// <summary>Gets or sets the number of expired trades.</summary>
    public int TradeExpiredCount { get; set; }

    /// <summary>Gets or sets the number of expired order books.</summary>
    public int OrderBookExpiredCount { get; set; }

    /// <summary>Gets or sets the number of expired bar sets.</summary>
    public int BarExpiredCount { get; set; }

    /// <summary>Gets the total count of cached items.</summary>
    public int TotalCount => QuoteCount + TradeCount + OrderBookCount + BarCacheCount;

    /// <summary>Gets the total count of expired items.</summary>
    public int TotalExpiredCount => QuoteExpiredCount + TradeExpiredCount + OrderBookExpiredCount + BarExpiredCount;
}
