using System.Runtime.CompilerServices;
using OoplesFinance.StockIndicators.Builder.ML.Sentiment;

namespace OoplesFinance.StockIndicators.Builder.DataProviders;

/// <summary>
/// Interface for social media sentiment data providers.
/// Provides posts from social platforms for sentiment analysis.
/// </summary>
public interface ISocialSentimentProvider : IDisposable
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
    /// Gets social media posts for a symbol.
    /// </summary>
    /// <param name="symbol">The stock symbol to get posts for.</param>
    /// <param name="startTime">Optional start time filter.</param>
    /// <param name="limit">Maximum number of posts to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of social media posts.</returns>
    Task<IReadOnlyList<SocialPost>> GetPostsAsync(
        string symbol,
        DateTime? startTime = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregated social metrics for a symbol.
    /// </summary>
    /// <param name="symbol">The stock symbol.</param>
    /// <param name="window">Time window for aggregation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated social metrics.</returns>
    Task<SocialMetrics> GetAggregatedMetricsAsync(
        string symbol,
        TimeSpan window,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets trending tickers based on social activity.
    /// </summary>
    /// <param name="limit">Maximum number of tickers to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of trending tickers with metrics.</returns>
    Task<IReadOnlyList<TrendingTicker>> GetTrendingTickersAsync(
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams real-time posts for the specified symbols.
    /// </summary>
    /// <param name="symbols">The symbols to stream posts for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of social posts.</returns>
    IAsyncEnumerable<SocialPost> StreamPostsAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets posts from specific users (for influencer tracking).
    /// </summary>
    /// <param name="usernames">The usernames to get posts from.</param>
    /// <param name="limit">Maximum number of posts per user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of username to posts.</returns>
    Task<IReadOnlyDictionary<string, IReadOnlyList<SocialPost>>> GetUserPostsAsync(
        IEnumerable<string> usernames,
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches posts by keyword or phrase.
    /// </summary>
    /// <param name="query">Search query.</param>
    /// <param name="startTime">Optional start time filter.</param>
    /// <param name="limit">Maximum number of posts to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching posts.</returns>
    Task<IReadOnlyList<SocialPost>> SearchPostsAsync(
        string query,
        DateTime? startTime = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Aggregated social metrics for a symbol.
/// </summary>
public sealed class SocialMetrics
{
    /// <summary>Gets or sets the symbol.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the total number of mentions.</summary>
    public int TotalMentions { get; set; }

    /// <summary>Gets or sets the number of unique sources/users.</summary>
    public int UniqueSources { get; set; }

    /// <summary>Gets or sets the average sentiment score (-1 to 1).</summary>
    public decimal AverageSentiment { get; set; }

    /// <summary>Gets or sets the sentiment standard deviation.</summary>
    public decimal SentimentStdDev { get; set; }

    /// <summary>Gets or sets the count of bullish posts.</summary>
    public int BullishCount { get; set; }

    /// <summary>Gets or sets the count of bearish posts.</summary>
    public int BearishCount { get; set; }

    /// <summary>Gets or sets the count of neutral posts.</summary>
    public int NeutralCount { get; set; }

    /// <summary>Gets or sets the mention velocity (mentions per hour).</summary>
    public decimal MentionVelocity { get; set; }

    /// <summary>Gets or sets the total engagement (likes + shares + comments).</summary>
    public long TotalEngagement { get; set; }

    /// <summary>Gets or sets the average engagement per post.</summary>
    public decimal AverageEngagement { get; set; }

    /// <summary>Gets or sets the window start time.</summary>
    public DateTime WindowStart { get; set; }

    /// <summary>Gets or sets the window end time.</summary>
    public DateTime WindowEnd { get; set; }

    /// <summary>Gets or sets the platform breakdown of mentions.</summary>
    public IReadOnlyDictionary<SocialPlatform, int> MentionsByPlatform { get; set; } =
        new Dictionary<SocialPlatform, int>();

    /// <summary>Gets or sets top hashtags used with this symbol.</summary>
    public IReadOnlyList<string> TopHashtags { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets top influencers discussing this symbol.</summary>
    public IReadOnlyList<string> TopInfluencers { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Trending ticker information.
/// </summary>
public sealed class TrendingTicker
{
    /// <summary>Gets or sets the ticker symbol.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the trend rank.</summary>
    public int Rank { get; set; }

    /// <summary>Gets or sets the mention count.</summary>
    public int MentionCount { get; set; }

    /// <summary>Gets or sets the mention change from previous period (percentage).</summary>
    public decimal MentionChangePercent { get; set; }

    /// <summary>Gets or sets the sentiment score (-1 to 1).</summary>
    public decimal SentimentScore { get; set; }

    /// <summary>Gets or sets the sentiment change from previous period.</summary>
    public decimal SentimentChange { get; set; }

    /// <summary>Gets or sets total engagement.</summary>
    public long TotalEngagement { get; set; }

    /// <summary>Gets or sets whether this is a new trending ticker.</summary>
    public bool IsNew { get; set; }

    /// <summary>Gets or sets the primary platform driving mentions.</summary>
    public SocialPlatform PrimaryPlatform { get; set; }

    /// <summary>Gets or sets sample posts for context.</summary>
    public IReadOnlyList<SocialPost> SamplePosts { get; set; } = Array.Empty<SocialPost>();
}

/// <summary>
/// Options for configuring social sentiment providers.
/// </summary>
public sealed class SocialProviderOptions
{
    /// <summary>Gets or sets the API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the API secret if required.</summary>
    public string? ApiSecret { get; set; }

    /// <summary>Gets or sets the OAuth access token if required.</summary>
    public string? AccessToken { get; set; }

    /// <summary>Gets or sets the base URL override.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Gets or sets the request timeout.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the maximum requests per minute.</summary>
    public int MaxRequestsPerMinute { get; set; } = 30;

    /// <summary>Gets or sets whether to include replies in posts.</summary>
    public bool IncludeReplies { get; set; } = false;

    /// <summary>Gets or sets whether to include retweets/reposts.</summary>
    public bool IncludeReposts { get; set; } = false;

    /// <summary>Gets or sets the minimum follower count filter.</summary>
    public int MinimumFollowerCount { get; set; } = 0;

    /// <summary>Gets or sets retry options.</summary>
    public RetryOptions RetryOptions { get; set; } = new();
}

/// <summary>
/// Aggregates social sentiment from multiple providers.
/// </summary>
public sealed class AggregatedSocialSentimentProvider : ISocialSentimentProvider
{
    private readonly IReadOnlyList<ISocialSentimentProvider> _providers;
    private readonly AggregatedSocialOptions _options;
    private bool _disposed;

    /// <summary>
    /// Creates an aggregated social sentiment provider.
    /// </summary>
    public AggregatedSocialSentimentProvider(
        IEnumerable<ISocialSentimentProvider> providers,
        AggregatedSocialOptions? options = null)
    {
        _providers = providers.ToList();
        _options = options ?? new AggregatedSocialOptions();
    }

    /// <inheritdoc />
    public string ProviderName => "Aggregated";

    /// <inheritdoc />
    public bool IsConnected => _providers.Any(p => p.IsConnected);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SocialPost>> GetPostsAsync(
        string symbol,
        DateTime? startTime = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var allPosts = new List<SocialPost>();
        var tasks = _providers.Select(p => SafeGetPostsAsync(p, symbol, startTime, limit, cancellationToken));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var result in results)
        {
            allPosts.AddRange(result);
        }

        return DeduplicateAndSort(allPosts, limit);
    }

    /// <inheritdoc />
    public async Task<SocialMetrics> GetAggregatedMetricsAsync(
        string symbol,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        var posts = await GetPostsAsync(
            symbol,
            DateTime.UtcNow - window,
            limit: 1000,
            cancellationToken).ConfigureAwait(false);

        if (posts.Count == 0)
        {
            return new SocialMetrics
            {
                Symbol = symbol,
                WindowStart = DateTime.UtcNow - window,
                WindowEnd = DateTime.UtcNow
            };
        }

        // Use SocialSentimentAnalyzer for consistent analysis
        var analyzer = new SocialSentimentAnalyzer();
        var analysisResult = analyzer.Analyze(posts);

        var mentionsByPlatform = posts
            .GroupBy(p => p.Platform)
            .ToDictionary(g => g.Key, g => g.Count());

        var topHashtags = posts
            .SelectMany(p => p.Hashtags)
            .GroupBy(h => h.ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToList();

        var topInfluencers = posts
            .GroupBy(p => p.Username)
            .OrderByDescending(g => g.First().FollowerCount)
            .Take(10)
            .Select(g => g.Key)
            .ToList();

        return new SocialMetrics
        {
            Symbol = symbol,
            TotalMentions = posts.Count,
            UniqueSources = posts.Select(p => p.Username).Distinct().Count(),
            AverageSentiment = (decimal)analysisResult.AggregateScore,
            BullishCount = analysisResult.BullishCount,
            BearishCount = analysisResult.BearishCount,
            NeutralCount = analysisResult.NeutralCount,
            MentionVelocity = (decimal)analysisResult.PostsPerHour,
            TotalEngagement = analysisResult.TotalEngagement,
            AverageEngagement = (decimal)analysisResult.AverageEngagement,
            WindowStart = DateTime.UtcNow - window,
            WindowEnd = DateTime.UtcNow,
            MentionsByPlatform = mentionsByPlatform,
            TopHashtags = topHashtags,
            TopInfluencers = topInfluencers
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TrendingTicker>> GetTrendingTickersAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var allTrending = new Dictionary<string, TrendingTicker>();

        foreach (var provider in _providers)
        {
            try
            {
                var trending = await provider.GetTrendingTickersAsync(limit, cancellationToken)
                    .ConfigureAwait(false);

                foreach (var ticker in trending)
                {
                    if (!allTrending.TryGetValue(ticker.Symbol, out var existing))
                    {
                        allTrending[ticker.Symbol] = ticker;
                    }
                    else
                    {
                        // Merge metrics
                        existing.MentionCount += ticker.MentionCount;
                        existing.TotalEngagement += ticker.TotalEngagement;
                        // Average sentiment
                        existing.SentimentScore = (existing.SentimentScore + ticker.SentimentScore) / 2;
                    }
                }
            }
            catch
            {
                // Continue with other providers
            }
        }

        // Re-rank by total mentions
        var ranked = allTrending.Values
            .OrderByDescending(t => t.MentionCount)
            .Take(limit)
            .ToList();

        for (int i = 0; i < ranked.Count; i++)
        {
            ranked[i].Rank = i + 1;
        }

        return ranked;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SocialPost> StreamPostsAsync(
        IEnumerable<string> symbols,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var symbolList = symbols.ToList();
        var seen = new HashSet<string>();

        var streams = _providers.Select(p => p.StreamPostsAsync(symbolList, cancellationToken));

        await foreach (var post in MergeStreams(streams, cancellationToken))
        {
            var key = GetDeduplicationKey(post);
            if (seen.Add(key))
            {
                yield return post;
            }
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<SocialPost>>> GetUserPostsAsync(
        IEnumerable<string> usernames,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, List<SocialPost>>();
        var usernameList = usernames.ToList();

        foreach (var provider in _providers)
        {
            try
            {
                var userPosts = await provider.GetUserPostsAsync(usernameList, limit, cancellationToken)
                    .ConfigureAwait(false);

                foreach (var (username, posts) in userPosts)
                {
                    if (!result.TryGetValue(username, out var existing))
                    {
                        existing = new List<SocialPost>();
                        result[username] = existing;
                    }
                    existing.AddRange(posts);
                }
            }
            catch
            {
                // Continue with other providers
            }
        }

        // Deduplicate and limit per user
        return result.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<SocialPost>)DeduplicateAndSort(kvp.Value, limit));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SocialPost>> SearchPostsAsync(
        string query,
        DateTime? startTime = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var allPosts = new List<SocialPost>();
        var tasks = _providers.Select(p => SafeSearchPostsAsync(p, query, startTime, limit, cancellationToken));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var result in results)
        {
            allPosts.AddRange(result);
        }

        return DeduplicateAndSort(allPosts, limit);
    }

    private static async Task<IReadOnlyList<SocialPost>> SafeGetPostsAsync(
        ISocialSentimentProvider provider,
        string symbol,
        DateTime? startTime,
        int limit,
        CancellationToken ct)
    {
        try
        {
            return await provider.GetPostsAsync(symbol, startTime, limit, ct).ConfigureAwait(false);
        }
        catch
        {
            return Array.Empty<SocialPost>();
        }
    }

    private static async Task<IReadOnlyList<SocialPost>> SafeSearchPostsAsync(
        ISocialSentimentProvider provider,
        string query,
        DateTime? startTime,
        int limit,
        CancellationToken ct)
    {
        try
        {
            return await provider.SearchPostsAsync(query, startTime, limit, ct).ConfigureAwait(false);
        }
        catch
        {
            return Array.Empty<SocialPost>();
        }
    }

    private IReadOnlyList<SocialPost> DeduplicateAndSort(List<SocialPost> posts, int limit)
    {
        var seen = new HashSet<string>();
        var deduplicated = new List<SocialPost>();

        foreach (var post in posts.OrderByDescending(p => p.Timestamp))
        {
            var key = GetDeduplicationKey(post);
            if (seen.Add(key))
            {
                deduplicated.Add(post);
                if (deduplicated.Count >= limit)
                    break;
            }
        }

        return deduplicated;
    }

    private static string GetDeduplicationKey(SocialPost post)
    {
        // Use platform + username + content hash + approximate time
        var timeKey = post.Timestamp.ToString("yyyyMMddHHmm");
        return $"{post.Platform}_{post.Username}_{post.Content.GetHashCode()}_{timeKey}";
    }

    private static async IAsyncEnumerable<SocialPost> MergeStreams(
        IEnumerable<IAsyncEnumerable<SocialPost>> streams,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = System.Threading.Channels.Channel.CreateUnbounded<SocialPost>();

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
                // Expected
            }
            catch
            {
                // Log but continue
            }
        }).ToList();

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
/// Options for aggregated social sentiment provider.
/// </summary>
public sealed class AggregatedSocialOptions
{
    /// <summary>Gets or sets the deduplication time window.</summary>
    public TimeSpan DeduplicationWindow { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets provider priority order.</summary>
    public IReadOnlyList<string> ProviderPriority { get; set; } = Array.Empty<string>();
}
