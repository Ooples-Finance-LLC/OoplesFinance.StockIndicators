using System.Runtime.CompilerServices;

namespace OoplesFinance.StockIndicators.Builder.DataProviders;

/// <summary>
/// Interface for news data providers.
/// Provides news articles for sentiment analysis and trading signals.
/// </summary>
public interface INewsProvider : IDisposable
{
    /// <summary>
    /// Gets the provider name.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets whether the provider is connected and ready.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets news articles for a symbol within a date range.
    /// </summary>
    /// <param name="symbol">The stock symbol to get news for.</param>
    /// <param name="startDate">Start date for news search.</param>
    /// <param name="endDate">End date for news search.</param>
    /// <param name="limit">Maximum number of articles to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of news articles.</returns>
    Task<IReadOnlyList<NewsArticleData>> GetNewsAsync(
        string symbol,
        DateTime startDate,
        DateTime endDate,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest news articles for a symbol.
    /// </summary>
    /// <param name="symbol">The stock symbol to get news for.</param>
    /// <param name="limit">Maximum number of articles to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of latest news articles.</returns>
    Task<IReadOnlyList<NewsArticleData>> GetLatestNewsAsync(
        string symbol,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest news articles for multiple symbols.
    /// </summary>
    /// <param name="symbols">The stock symbols to get news for.</param>
    /// <param name="limit">Maximum number of articles per symbol.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of symbol to news articles.</returns>
    Task<IReadOnlyDictionary<string, IReadOnlyList<NewsArticleData>>> GetLatestNewsAsync(
        IEnumerable<string> symbols,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches news articles by keyword or phrase.
    /// </summary>
    /// <param name="query">Search query.</param>
    /// <param name="startDate">Optional start date filter.</param>
    /// <param name="endDate">Optional end date filter.</param>
    /// <param name="limit">Maximum number of articles to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching news articles.</returns>
    Task<IReadOnlyList<NewsArticleData>> SearchNewsAsync(
        string query,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams real-time news articles for the specified symbols.
    /// </summary>
    /// <param name="symbols">The symbols to stream news for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of news articles.</returns>
    IAsyncEnumerable<NewsArticleData> StreamNewsAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets market-wide news (not symbol-specific).
    /// </summary>
    /// <param name="category">Optional news category filter.</param>
    /// <param name="limit">Maximum number of articles to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of market news articles.</returns>
    Task<IReadOnlyList<NewsArticleData>> GetMarketNewsAsync(
        NewsCategory? category = null,
        int limit = 50,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a news article from a data provider.
/// This is the provider-level model that feeds into ML.Sentiment.NewsArticle for analysis.
/// </summary>
public sealed class NewsArticleData
{
    /// <summary>Gets or sets the unique article ID from the provider.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the article title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the full article content/body.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Gets or sets a brief summary of the article.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Gets or sets the news source name (e.g., "Reuters", "Bloomberg").</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets the article URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the author name.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>Gets or sets the publication timestamp.</summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>Gets or sets the stock symbols mentioned in the article.</summary>
    public IReadOnlyList<string> Symbols { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets the article topics/categories.</summary>
    public IReadOnlyList<string> Topics { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets the provider-supplied sentiment score if available (-1 to 1).</summary>
    public decimal? SourceSentiment { get; set; }

    /// <summary>Gets or sets the image URL if available.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Gets or sets the news category.</summary>
    public NewsCategory Category { get; set; } = NewsCategory.General;

    /// <summary>Gets or sets the language code (e.g., "en").</summary>
    public string Language { get; set; } = "en";

    /// <summary>Gets or sets whether this is breaking news.</summary>
    public bool IsBreaking { get; set; }

    /// <summary>Gets or sets the data provider that supplied this article.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Gets or sets when this article was fetched/cached.</summary>
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Converts this provider model to the ML sentiment analyzer model.
    /// </summary>
    public ML.Sentiment.NewsArticle ToSentimentAnalyzerModel()
    {
        return new ML.Sentiment.NewsArticle
        {
            Id = Id,
            Title = Title,
            Content = string.IsNullOrWhiteSpace(Content) ? Summary : Content,
            Source = Source,
            PublishedAt = PublishedAt,
            Url = Url,
            Symbols = Symbols
        };
    }
}

/// <summary>
/// News categories for filtering.
/// </summary>
public enum NewsCategory
{
    /// <summary>General market news.</summary>
    General,

    /// <summary>Earnings and financial results.</summary>
    Earnings,

    /// <summary>Mergers and acquisitions.</summary>
    MergersAcquisitions,

    /// <summary>IPOs and new listings.</summary>
    IPO,

    /// <summary>Analyst ratings and recommendations.</summary>
    AnalystRatings,

    /// <summary>Regulatory and SEC filings.</summary>
    Regulatory,

    /// <summary>Economic indicators and macro news.</summary>
    Economic,

    /// <summary>Cryptocurrency news.</summary>
    Crypto,

    /// <summary>Forex and currency news.</summary>
    Forex,

    /// <summary>Commodities news.</summary>
    Commodities,

    /// <summary>Technology sector news.</summary>
    Technology,

    /// <summary>Healthcare sector news.</summary>
    Healthcare,

    /// <summary>Energy sector news.</summary>
    Energy,

    /// <summary>Financial sector news.</summary>
    Financial,

    /// <summary>Consumer sector news.</summary>
    Consumer,

    /// <summary>Insider trading activity.</summary>
    InsiderTrading,

    /// <summary>Options unusual activity.</summary>
    OptionsActivity,

    /// <summary>Short interest and squeeze potential.</summary>
    ShortInterest
}

/// <summary>
/// Options for configuring news providers.
/// </summary>
public sealed class NewsProviderOptions
{
    /// <summary>Gets or sets the API key for the provider.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the API secret if required.</summary>
    public string? ApiSecret { get; set; }

    /// <summary>Gets or sets the base URL override.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Gets or sets the request timeout.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the maximum requests per minute (rate limiting).</summary>
    public int MaxRequestsPerMinute { get; set; } = 60;

    /// <summary>Gets or sets whether to include full article content (may cost extra).</summary>
    public bool IncludeFullContent { get; set; } = true;

    /// <summary>Gets or sets the default language filter.</summary>
    public string DefaultLanguage { get; set; } = "en";

    /// <summary>Gets or sets retry options for failed requests.</summary>
    public RetryOptions RetryOptions { get; set; } = new();
}

/// <summary>
/// Retry options for HTTP requests.
/// </summary>
public sealed class RetryOptions
{
    /// <summary>Gets or sets the maximum number of retry attempts.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Gets or sets the initial delay between retries.</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets or sets the maximum delay between retries.</summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the backoff multiplier.</summary>
    public double BackoffMultiplier { get; set; } = 2.0;
}

/// <summary>
/// Aggregates news from multiple providers with deduplication.
/// </summary>
public sealed class AggregatedNewsProvider : INewsProvider
{
    private readonly IReadOnlyList<INewsProvider> _providers;
    private readonly AggregatedNewsOptions _options;
    private bool _disposed;

    /// <summary>
    /// Creates an aggregated news provider from multiple sources.
    /// </summary>
    /// <param name="providers">The news providers to aggregate.</param>
    /// <param name="options">Aggregation options.</param>
    public AggregatedNewsProvider(
        IEnumerable<INewsProvider> providers,
        AggregatedNewsOptions? options = null)
    {
        _providers = providers.ToList();
        _options = options ?? new AggregatedNewsOptions();
    }

    /// <inheritdoc />
    public string ProviderName => "Aggregated";

    /// <inheritdoc />
    public bool IsConnected => _providers.Any(p => p.IsConnected);

    /// <inheritdoc />
    public async Task<IReadOnlyList<NewsArticleData>> GetNewsAsync(
        string symbol,
        DateTime startDate,
        DateTime endDate,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var allArticles = new List<NewsArticleData>();
        var tasks = _providers.Select(p => SafeGetNewsAsync(p, symbol, startDate, endDate, limit, cancellationToken));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var result in results)
        {
            allArticles.AddRange(result);
        }

        return DeduplicateAndSort(allArticles, limit);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NewsArticleData>> GetLatestNewsAsync(
        string symbol,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var allArticles = new List<NewsArticleData>();
        var tasks = _providers.Select(p => SafeGetLatestNewsAsync(p, symbol, limit, cancellationToken));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var result in results)
        {
            allArticles.AddRange(result);
        }

        return DeduplicateAndSort(allArticles, limit);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<NewsArticleData>>> GetLatestNewsAsync(
        IEnumerable<string> symbols,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var symbolList = symbols.ToList();
        var result = new Dictionary<string, IReadOnlyList<NewsArticleData>>();

        var tasks = symbolList.Select(async symbol =>
        {
            var news = await GetLatestNewsAsync(symbol, limit, cancellationToken).ConfigureAwait(false);
            return (symbol, news);
        });

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var (symbol, news) in results)
        {
            result[symbol] = news;
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
        var allArticles = new List<NewsArticleData>();
        var tasks = _providers.Select(p => SafeSearchNewsAsync(p, query, startDate, endDate, limit, cancellationToken));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var result in results)
        {
            allArticles.AddRange(result);
        }

        return DeduplicateAndSort(allArticles, limit);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<NewsArticleData> StreamNewsAsync(
        IEnumerable<string> symbols,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var symbolList = symbols.ToList();
        var seen = new HashSet<string>();

        // Create merged stream from all providers
        var streams = _providers.Select(p => p.StreamNewsAsync(symbolList, cancellationToken));

        await foreach (var article in MergeStreams(streams, cancellationToken))
        {
            var key = GetDeduplicationKey(article);
            if (seen.Add(key))
            {
                yield return article;
            }
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NewsArticleData>> GetMarketNewsAsync(
        NewsCategory? category = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var allArticles = new List<NewsArticleData>();
        var tasks = _providers.Select(p => SafeGetMarketNewsAsync(p, category, limit, cancellationToken));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var result in results)
        {
            allArticles.AddRange(result);
        }

        return DeduplicateAndSort(allArticles, limit);
    }

    private static async Task<IReadOnlyList<NewsArticleData>> SafeGetNewsAsync(
        INewsProvider provider,
        string symbol,
        DateTime startDate,
        DateTime endDate,
        int limit,
        CancellationToken ct)
    {
        try
        {
            return await provider.GetNewsAsync(symbol, startDate, endDate, limit, ct).ConfigureAwait(false);
        }
        catch
        {
            return Array.Empty<NewsArticleData>();
        }
    }

    private static async Task<IReadOnlyList<NewsArticleData>> SafeGetLatestNewsAsync(
        INewsProvider provider,
        string symbol,
        int limit,
        CancellationToken ct)
    {
        try
        {
            return await provider.GetLatestNewsAsync(symbol, limit, ct).ConfigureAwait(false);
        }
        catch
        {
            return Array.Empty<NewsArticleData>();
        }
    }

    private static async Task<IReadOnlyList<NewsArticleData>> SafeSearchNewsAsync(
        INewsProvider provider,
        string query,
        DateTime? startDate,
        DateTime? endDate,
        int limit,
        CancellationToken ct)
    {
        try
        {
            return await provider.SearchNewsAsync(query, startDate, endDate, limit, ct).ConfigureAwait(false);
        }
        catch
        {
            return Array.Empty<NewsArticleData>();
        }
    }

    private static async Task<IReadOnlyList<NewsArticleData>> SafeGetMarketNewsAsync(
        INewsProvider provider,
        NewsCategory? category,
        int limit,
        CancellationToken ct)
    {
        try
        {
            return await provider.GetMarketNewsAsync(category, limit, ct).ConfigureAwait(false);
        }
        catch
        {
            return Array.Empty<NewsArticleData>();
        }
    }

    private IReadOnlyList<NewsArticleData> DeduplicateAndSort(List<NewsArticleData> articles, int limit)
    {
        var seen = new HashSet<string>();
        var deduplicated = new List<NewsArticleData>();

        // Sort by publication time descending first
        var sorted = articles.OrderByDescending(a => a.PublishedAt);

        foreach (var article in sorted)
        {
            var key = GetDeduplicationKey(article);
            if (seen.Add(key))
            {
                deduplicated.Add(article);
                if (deduplicated.Count >= limit)
                    break;
            }
        }

        return deduplicated;
    }

    private string GetDeduplicationKey(NewsArticleData article)
    {
        // Use title + source + approximate time for deduplication
        var timeKey = article.PublishedAt.ToString("yyyyMMddHHmm");
        return $"{article.Title.ToLowerInvariant().GetHashCode()}_{article.Source}_{timeKey}";
    }

    private static async IAsyncEnumerable<NewsArticleData> MergeStreams(
        IEnumerable<IAsyncEnumerable<NewsArticleData>> streams,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = System.Threading.Channels.Channel.CreateUnbounded<NewsArticleData>();

        var producerTasks = streams.Select(async stream =>
        {
            try
            {
                await foreach (var item in stream.WithCancellation(cancellationToken))
                {
                    await channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
            catch
            {
                // Log but don't fail entire stream
            }
        }).ToList();

        // Complete channel when all producers are done
        _ = Task.WhenAll(producerTasks).ContinueWith(_ => channel.Writer.Complete(), cancellationToken);

        await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return item;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var provider in _providers)
            {
                provider.Dispose();
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// Options for aggregated news provider.
/// </summary>
public sealed class AggregatedNewsOptions
{
    /// <summary>Gets or sets the deduplication time window (articles within this window with same title are considered duplicates).</summary>
    public TimeSpan DeduplicationWindow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets the provider priority order (first provider's articles preferred in case of duplicates).</summary>
    public IReadOnlyList<string> ProviderPriority { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets whether to fail fast if any provider fails.</summary>
    public bool FailFast { get; set; } = false;
}
