using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace OoplesFinance.StockIndicators.Builder.DataProviders;

/// <summary>
/// Cache decorator for SEC filing providers.
/// Wraps an ISECFilingProvider with L1 in-memory caching with TTL-based expiration.
/// Uses longer TTLs than news/social caches since SEC filings don't change after publication.
/// </summary>
public sealed class SECFilingCacheDecorator : ISECFilingProvider
{
    private readonly ISECFilingProvider _inner;
    private readonly SECCacheOptions _options;
    private readonly ConcurrentDictionary<string, CachedSECItem<IReadOnlyList<SECFilingData>>> _filingsCache = new();
    private readonly ConcurrentDictionary<string, CachedSECItem<SECFilingData?>> _singleFilingCache = new();
    private readonly ConcurrentDictionary<string, CachedSECItem<string>> _contentCache = new();
    private readonly ConcurrentDictionary<string, CachedSECItem<IReadOnlyList<FilingSection>>> _sectionsCache = new();
    private readonly ConcurrentDictionary<string, CachedSECItem<IReadOnlyList<InsiderTransaction>>> _insiderCache = new();
    private readonly ConcurrentDictionary<string, CachedSECItem<IReadOnlyList<InstitutionalHolding>>> _institutionalCache = new();
    private readonly ConcurrentDictionary<string, CachedSECItem<CompanyInfo?>> _companyInfoCache = new();
    private readonly ConcurrentDictionary<string, string> _cikLookupCache = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    // Cache statistics
    private long _cacheHits;
    private long _cacheMisses;

    /// <summary>
    /// Creates a new cache decorator wrapping the specified provider.
    /// </summary>
    /// <param name="inner">The inner SEC filing provider to wrap.</param>
    /// <param name="options">Cache configuration options.</param>
    public SECFilingCacheDecorator(ISECFilingProvider inner, SECCacheOptions? options = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _options = options ?? new SECCacheOptions();

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
    public async Task<IReadOnlyList<SECFilingData>> GetFilingsAsync(
        string symbol,
        IEnumerable<string>? formTypes = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var formTypeList = formTypes?.ToList();
        var cacheKey = MakeFilingsCacheKey(symbol, formTypeList, startDate, endDate, limit);

        if (TryGetFromFilingsCache(cacheKey, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        Interlocked.Increment(ref _cacheMisses);
        var result = await _inner.GetFilingsAsync(symbol, formTypeList, startDate, endDate, limit, cancellationToken)
            .ConfigureAwait(false);

        CacheFilingsResult(cacheKey, result, _options.FilingsListTtl);

        return result;
    }

    /// <inheritdoc />
    public async Task<SECFilingData?> GetFilingByAccessionAsync(
        string accessionNumber,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"filing_{accessionNumber}";

        if (TryGetFromSingleFilingCache(cacheKey, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        Interlocked.Increment(ref _cacheMisses);
        var result = await _inner.GetFilingByAccessionAsync(accessionNumber, cancellationToken)
            .ConfigureAwait(false);

        // Single filings don't change, use long TTL
        CacheSingleFilingResult(cacheKey, result, _options.FilingDetailTtl);

        return result;
    }

    /// <inheritdoc />
    public async Task<string> GetFilingContentAsync(
        string accessionNumber,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"content_{accessionNumber}";

        if (TryGetFromContentCache(cacheKey, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        Interlocked.Increment(ref _cacheMisses);
        var result = await _inner.GetFilingContentAsync(accessionNumber, cancellationToken)
            .ConfigureAwait(false);

        // Filing content never changes, use very long TTL
        CacheContentResult(cacheKey, result, _options.FilingContentTtl);

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FilingSection>> GetFilingSectionsAsync(
        string accessionNumber,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"sections_{accessionNumber}";

        if (TryGetFromSectionsCache(cacheKey, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        Interlocked.Increment(ref _cacheMisses);
        var result = await _inner.GetFilingSectionsAsync(accessionNumber, cancellationToken)
            .ConfigureAwait(false);

        // Sections never change, use very long TTL
        CacheSectionsResult(cacheKey, result, _options.FilingSectionsTtl);

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InsiderTransaction>> GetInsiderTransactionsAsync(
        string symbol,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = MakeInsiderCacheKey(symbol, startDate, endDate);

        if (TryGetFromInsiderCache(cacheKey, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        Interlocked.Increment(ref _cacheMisses);
        var result = await _inner.GetInsiderTransactionsAsync(symbol, startDate, endDate, cancellationToken)
            .ConfigureAwait(false);

        CacheInsiderResult(cacheKey, result, _options.InsiderTransactionsTtl);

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InstitutionalHolding>> GetInstitutionalHoldingsAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"institutional_{symbol}";

        if (TryGetFromInstitutionalCache(cacheKey, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        Interlocked.Increment(ref _cacheMisses);
        var result = await _inner.GetInstitutionalHoldingsAsync(symbol, cancellationToken)
            .ConfigureAwait(false);

        // 13F data is quarterly, use moderate TTL
        CacheInstitutionalResult(cacheKey, result, _options.InstitutionalHoldingsTtl);

        return result;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SECFilingData> StreamNewFilingsAsync(
        IEnumerable<string> symbols,
        IEnumerable<string>? formTypes = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Streaming cannot be cached, but we cache individual filings as they arrive
        var seen = new HashSet<string>();

        await foreach (var filing in _inner.StreamNewFilingsAsync(symbols, formTypes, cancellationToken))
        {
            // Deduplicate by accession number
            if (!seen.Add(filing.AccessionNumber))
            {
                continue;
            }

            // Cache the filing
            var cacheKey = $"filing_{filing.AccessionNumber}";
            CacheSingleFilingResult(cacheKey, filing, _options.FilingDetailTtl);

            yield return filing;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SECFilingData>> GetRecentFilingsAsync(
        IEnumerable<string>? formTypes = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var formTypeList = formTypes?.ToList();
        var formKey = formTypeList is not null && formTypeList.Count > 0
            ? string.Join(",", formTypeList.OrderBy(f => f))
            : "all";
        var cacheKey = $"recent_{formKey}_{limit}";

        if (TryGetFromFilingsCache(cacheKey, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        Interlocked.Increment(ref _cacheMisses);
        var result = await _inner.GetRecentFilingsAsync(formTypeList, limit, cancellationToken)
            .ConfigureAwait(false);

        // Recent filings change frequently, use shorter TTL
        CacheFilingsResult(cacheKey, result, _options.RecentFilingsTtl);

        return result;
    }

    /// <inheritdoc />
    public async Task<CompanyInfo?> GetCompanyInfoAsync(
        string cik,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"company_{cik}";

        if (TryGetFromCompanyInfoCache(cacheKey, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        Interlocked.Increment(ref _cacheMisses);
        var result = await _inner.GetCompanyInfoAsync(cik, cancellationToken)
            .ConfigureAwait(false);

        // Company info rarely changes, use long TTL
        CacheCompanyInfoResult(cacheKey, result, _options.CompanyInfoTtl);

        return result;
    }

    /// <inheritdoc />
    public async Task<string?> LookupCikAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        // CIK lookups never change, cache indefinitely
        if (_cikLookupCache.TryGetValue(symbol.ToUpperInvariant(), out var cachedCik))
        {
            Interlocked.Increment(ref _cacheHits);
            return cachedCik;
        }

        Interlocked.Increment(ref _cacheMisses);
        var result = await _inner.LookupCikAsync(symbol, cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrEmpty(result))
        {
            _cikLookupCache[symbol.ToUpperInvariant()] = result;
        }

        return result;
    }

    /// <summary>
    /// Clears all cached data.
    /// </summary>
    public void ClearCache()
    {
        _filingsCache.Clear();
        _singleFilingCache.Clear();
        _contentCache.Clear();
        _sectionsCache.Clear();
        _insiderCache.Clear();
        _institutionalCache.Clear();
        _companyInfoCache.Clear();
        // Do not clear CIK lookup cache - it never changes
        Interlocked.Exchange(ref _cacheHits, 0);
        Interlocked.Exchange(ref _cacheMisses, 0);
    }

    /// <summary>
    /// Clears cached data for a specific symbol.
    /// </summary>
    /// <param name="symbol">The symbol to clear from cache.</param>
    public void ClearSymbolCache(string symbol)
    {
        // Clear filings cache entries for this symbol
        var filingsKeysToRemove = _filingsCache.Keys
            .Where(k => k.Contains($"_{symbol}_") || k.StartsWith($"filings_{symbol}_"))
            .ToList();

        foreach (var key in filingsKeysToRemove)
        {
            _filingsCache.TryRemove(key, out _);
        }

        // Clear insider cache for this symbol
        var insiderKeysToRemove = _insiderCache.Keys
            .Where(k => k.StartsWith($"insider_{symbol}_"))
            .ToList();

        foreach (var key in insiderKeysToRemove)
        {
            _insiderCache.TryRemove(key, out _);
        }

        // Clear institutional cache for this symbol
        _institutionalCache.TryRemove($"institutional_{symbol}", out _);
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public SECCacheStatistics GetStatistics()
    {
        var expiredFilings = _filingsCache.Values.Count(c => c.IsExpired);
        var expiredContent = _contentCache.Values.Count(c => c.IsExpired);
        var expiredInsider = _insiderCache.Values.Count(c => c.IsExpired);

        return new SECCacheStatistics
        {
            FilingsQueryCount = _filingsCache.Count,
            SingleFilingCount = _singleFilingCache.Count,
            ContentCacheCount = _contentCache.Count,
            SectionsCacheCount = _sectionsCache.Count,
            InsiderTransactionsCount = _insiderCache.Count,
            InstitutionalHoldingsCount = _institutionalCache.Count,
            CompanyInfoCount = _companyInfoCache.Count,
            CikLookupCount = _cikLookupCache.Count,
            ExpiredFilingsQueryCount = expiredFilings,
            ExpiredContentCount = expiredContent,
            ExpiredInsiderCount = expiredInsider,
            CacheHits = CacheHits,
            CacheMisses = CacheMisses,
            CacheHitRatio = CacheHitRatio
        };
    }

    private bool TryGetFromFilingsCache(string key, out IReadOnlyList<SECFilingData> result)
    {
        if (_filingsCache.TryGetValue(key, out var cached) && !cached.IsExpired)
        {
            result = cached.Value;
            return true;
        }
        result = Array.Empty<SECFilingData>();
        return false;
    }

    private bool TryGetFromSingleFilingCache(string key, out SECFilingData? result)
    {
        if (_singleFilingCache.TryGetValue(key, out var cached) && !cached.IsExpired)
        {
            result = cached.Value;
            return true;
        }
        result = null;
        return false;
    }

    private bool TryGetFromContentCache(string key, out string result)
    {
        if (_contentCache.TryGetValue(key, out var cached) && !cached.IsExpired)
        {
            result = cached.Value;
            return true;
        }
        result = string.Empty;
        return false;
    }

    private bool TryGetFromSectionsCache(string key, out IReadOnlyList<FilingSection> result)
    {
        if (_sectionsCache.TryGetValue(key, out var cached) && !cached.IsExpired)
        {
            result = cached.Value;
            return true;
        }
        result = Array.Empty<FilingSection>();
        return false;
    }

    private bool TryGetFromInsiderCache(string key, out IReadOnlyList<InsiderTransaction> result)
    {
        if (_insiderCache.TryGetValue(key, out var cached) && !cached.IsExpired)
        {
            result = cached.Value;
            return true;
        }
        result = Array.Empty<InsiderTransaction>();
        return false;
    }

    private bool TryGetFromInstitutionalCache(string key, out IReadOnlyList<InstitutionalHolding> result)
    {
        if (_institutionalCache.TryGetValue(key, out var cached) && !cached.IsExpired)
        {
            result = cached.Value;
            return true;
        }
        result = Array.Empty<InstitutionalHolding>();
        return false;
    }

    private bool TryGetFromCompanyInfoCache(string key, out CompanyInfo? result)
    {
        if (_companyInfoCache.TryGetValue(key, out var cached) && !cached.IsExpired)
        {
            result = cached.Value;
            return true;
        }
        result = null;
        return false;
    }

    private void CacheFilingsResult(string key, IReadOnlyList<SECFilingData> result, TimeSpan ttl)
    {
        if (_options.MaxCacheEntries > 0 && _filingsCache.Count >= _options.MaxCacheEntries)
        {
            EvictOldestEntries(_filingsCache, _options.MaxCacheEntries / 4);
        }

        _filingsCache[key] = new CachedSECItem<IReadOnlyList<SECFilingData>>(result, ttl);
    }

    private void CacheSingleFilingResult(string key, SECFilingData? result, TimeSpan ttl)
    {
        _singleFilingCache[key] = new CachedSECItem<SECFilingData?>(result, ttl);
    }

    private void CacheContentResult(string key, string result, TimeSpan ttl)
    {
        if (_options.MaxContentCacheSize > 0 && _contentCache.Count >= _options.MaxContentCacheSize)
        {
            EvictOldestEntries(_contentCache, _options.MaxContentCacheSize / 4);
        }

        _contentCache[key] = new CachedSECItem<string>(result, ttl);
    }

    private void CacheSectionsResult(string key, IReadOnlyList<FilingSection> result, TimeSpan ttl)
    {
        _sectionsCache[key] = new CachedSECItem<IReadOnlyList<FilingSection>>(result, ttl);
    }

    private void CacheInsiderResult(string key, IReadOnlyList<InsiderTransaction> result, TimeSpan ttl)
    {
        _insiderCache[key] = new CachedSECItem<IReadOnlyList<InsiderTransaction>>(result, ttl);
    }

    private void CacheInstitutionalResult(string key, IReadOnlyList<InstitutionalHolding> result, TimeSpan ttl)
    {
        _institutionalCache[key] = new CachedSECItem<IReadOnlyList<InstitutionalHolding>>(result, ttl);
    }

    private void CacheCompanyInfoResult(string key, CompanyInfo? result, TimeSpan ttl)
    {
        _companyInfoCache[key] = new CachedSECItem<CompanyInfo?>(result, ttl);
    }

    private static string MakeFilingsCacheKey(string symbol, IReadOnlyList<string>? formTypes, DateTime? startDate, DateTime? endDate, int limit)
    {
        var formKey = formTypes is not null && formTypes.Count > 0
            ? string.Join(",", formTypes.OrderBy(f => f))
            : "all";
        var startKey = startDate?.ToString("yyyyMMdd") ?? "none";
        var endKey = endDate?.ToString("yyyyMMdd") ?? "none";
        return $"filings_{symbol}_{formKey}_{startKey}_{endKey}_{limit}";
    }

    private static string MakeInsiderCacheKey(string symbol, DateTime? startDate, DateTime? endDate)
    {
        var startKey = startDate?.ToString("yyyyMMdd") ?? "none";
        var endKey = endDate?.ToString("yyyyMMdd") ?? "none";
        return $"insider_{symbol}_{startKey}_{endKey}";
    }

    private void Cleanup(object? state)
    {
        RemoveExpired(_filingsCache);
        RemoveExpired(_singleFilingCache);
        RemoveExpired(_contentCache);
        RemoveExpired(_sectionsCache);
        RemoveExpired(_insiderCache);
        RemoveExpired(_institutionalCache);
        RemoveExpired(_companyInfoCache);
    }

    private static void RemoveExpired<T>(ConcurrentDictionary<string, CachedSECItem<T>> cache)
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

    private static void EvictOldestEntries<T>(ConcurrentDictionary<string, CachedSECItem<T>> cache, int count)
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
/// Wrapper for cached SEC items with expiration.
/// </summary>
/// <typeparam name="T">The type of cached item.</typeparam>
internal readonly struct CachedSECItem<T>
{
    /// <summary>
    /// Creates a new cached item.
    /// </summary>
    public CachedSECItem(T value, TimeSpan ttl)
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
/// Configuration options for SEC filing cache.
/// SEC filings don't change after publication, so we use longer TTLs.
/// </summary>
public sealed class SECCacheOptions
{
    /// <summary>Gets or sets the TTL for filing list queries.</summary>
    public TimeSpan FilingsListTtl { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Gets or sets the TTL for recent filings queries (changes frequently).</summary>
    public TimeSpan RecentFilingsTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets the TTL for individual filing details (filings don't change).</summary>
    public TimeSpan FilingDetailTtl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Gets or sets the TTL for filing content (content never changes).</summary>
    public TimeSpan FilingContentTtl { get; set; } = TimeSpan.FromHours(48);

    /// <summary>Gets or sets the TTL for extracted sections (never change).</summary>
    public TimeSpan FilingSectionsTtl { get; set; } = TimeSpan.FromHours(48);

    /// <summary>Gets or sets the TTL for insider transactions (Form 4).</summary>
    public TimeSpan InsiderTransactionsTtl { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Gets or sets the TTL for institutional holdings (13F - quarterly).</summary>
    public TimeSpan InstitutionalHoldingsTtl { get; set; } = TimeSpan.FromHours(6);

    /// <summary>Gets or sets the TTL for company info (rarely changes).</summary>
    public TimeSpan CompanyInfoTtl { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Gets or sets the cleanup interval for expired entries.</summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets the maximum number of cached query results (0 for unlimited).</summary>
    public int MaxCacheEntries { get; set; } = 500;

    /// <summary>Gets or sets the maximum number of cached content items (filing text is large).</summary>
    public int MaxContentCacheSize { get; set; } = 100;
}

/// <summary>
/// Cache statistics for SEC filing cache.
/// </summary>
public sealed class SECCacheStatistics
{
    /// <summary>Gets or sets the number of cached filings list query results.</summary>
    public int FilingsQueryCount { get; set; }

    /// <summary>Gets or sets the number of cached individual filings.</summary>
    public int SingleFilingCount { get; set; }

    /// <summary>Gets or sets the number of cached filing content items.</summary>
    public int ContentCacheCount { get; set; }

    /// <summary>Gets or sets the number of cached section extractions.</summary>
    public int SectionsCacheCount { get; set; }

    /// <summary>Gets or sets the number of cached insider transaction queries.</summary>
    public int InsiderTransactionsCount { get; set; }

    /// <summary>Gets or sets the number of cached institutional holdings queries.</summary>
    public int InstitutionalHoldingsCount { get; set; }

    /// <summary>Gets or sets the number of cached company info entries.</summary>
    public int CompanyInfoCount { get; set; }

    /// <summary>Gets or sets the number of cached CIK lookups (permanent).</summary>
    public int CikLookupCount { get; set; }

    /// <summary>Gets or sets the number of expired filings query entries.</summary>
    public int ExpiredFilingsQueryCount { get; set; }

    /// <summary>Gets or sets the number of expired content entries.</summary>
    public int ExpiredContentCount { get; set; }

    /// <summary>Gets or sets the number of expired insider transaction entries.</summary>
    public int ExpiredInsiderCount { get; set; }

    /// <summary>Gets or sets the total cache hits.</summary>
    public long CacheHits { get; set; }

    /// <summary>Gets or sets the total cache misses.</summary>
    public long CacheMisses { get; set; }

    /// <summary>Gets or sets the cache hit ratio (0.0 to 1.0).</summary>
    public double CacheHitRatio { get; set; }

    /// <summary>Gets the total number of cached items.</summary>
    public int TotalCount => FilingsQueryCount + SingleFilingCount + ContentCacheCount +
                             SectionsCacheCount + InsiderTransactionsCount + InstitutionalHoldingsCount +
                             CompanyInfoCount + CikLookupCount;

    /// <summary>Gets the total number of expired items.</summary>
    public int TotalExpiredCount => ExpiredFilingsQueryCount + ExpiredContentCount + ExpiredInsiderCount;
}
