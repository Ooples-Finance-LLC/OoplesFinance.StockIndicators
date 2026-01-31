using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using OoplesFinance.StockIndicators.Builder.ML.Sentiment;

namespace OoplesFinance.StockIndicators.Builder.DataProviders.Providers;

/// <summary>
/// Social sentiment provider for StockTwits API.
/// StockTwits is a social network for investors and traders.
/// </summary>
public sealed class StockTwitsProvider : ISocialSentimentProvider
{
    private readonly HttpClient _httpClient;
    private readonly SocialProviderOptions _options;
    private readonly SemaphoreSlim _rateLimiter;
    private readonly JsonSerializerOptions _jsonOptions;
    private DateTime _lastRequestTime = DateTime.MinValue;
    private bool _disposed;

    private const string BaseUrl = "https://api.stocktwits.com/api/2";

    /// <summary>
    /// Creates a new StockTwits provider.
    /// </summary>
    /// <param name="options">Provider options.</param>
    /// <param name="httpClient">Optional HTTP client (for testing).</param>
    public StockTwitsProvider(SocialProviderOptions? options = null, HttpClient? httpClient = null)
    {
        _options = options ?? new SocialProviderOptions();

        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress = new Uri(_options.BaseUrl ?? BaseUrl);
        _httpClient.Timeout = _options.Timeout;
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        _rateLimiter = new SemaphoreSlim(1, 1);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <inheritdoc />
    public string ProviderName => "StockTwits";

    /// <inheritdoc />
    public bool IsConnected => !_disposed;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SocialPost>> GetPostsAsync(
        string symbol,
        DateTime? startTime = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var allPosts = new List<SocialPost>();
        long? maxId = null;

        while (allPosts.Count < limit)
        {
            var endpoint = $"/streams/symbol/{symbol.ToUpperInvariant()}.json";
            var parameters = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(_options.AccessToken))
            {
                parameters["access_token"] = _options.AccessToken;
            }

            if (maxId.HasValue)
            {
                parameters["max"] = maxId.Value.ToString();
            }

            var response = await ExecuteRequestAsync<StockTwitsStreamResponse>(
                endpoint, parameters, cancellationToken).ConfigureAwait(false);

            if (response?.Messages is null || response.Messages.Count == 0)
                break;

            var posts = response.Messages.Select(m => ConvertToSocialPost(m, symbol)).ToList();

            // Filter by start time if specified
            if (startTime.HasValue)
            {
                posts = posts.Where(p => p.Timestamp >= startTime.Value).ToList();
            }

            allPosts.AddRange(posts);

            // Get max id for pagination
            maxId = response.Messages.Min(m => m.Id) - 1;

            // Check if we've gone past the start time
            if (startTime.HasValue && response.Messages.Any(m => m.CreatedAt < startTime.Value))
                break;

            // StockTwits rate limit
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        return allPosts.Take(limit).ToList();
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
            limit: 500,
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
            MentionsByPlatform = new Dictionary<SocialPlatform, int> { { SocialPlatform.StockTwits, posts.Count } },
            TopHashtags = topHashtags,
            TopInfluencers = topInfluencers
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TrendingTicker>> GetTrendingTickersAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var response = await ExecuteRequestAsync<StockTwitsTrendingResponse>(
            "/trending/symbols.json",
            new Dictionary<string, string>(),
            cancellationToken).ConfigureAwait(false);

        if (response?.Symbols is null)
            return Array.Empty<TrendingTicker>();

        var tickers = new List<TrendingTicker>();
        int rank = 1;

        foreach (var symbol in response.Symbols.Take(limit))
        {
            var ticker = new TrendingTicker
            {
                Symbol = symbol.Symbol ?? string.Empty,
                Rank = rank++,
                MentionCount = symbol.WatchlistCount,
                PrimaryPlatform = SocialPlatform.StockTwits
            };

            // Get sample posts for this ticker
            try
            {
                var posts = await GetPostsAsync(ticker.Symbol, null, 3, cancellationToken)
                    .ConfigureAwait(false);
                ticker.SamplePosts = posts;

                // Calculate sentiment from sample posts
                if (posts.Count > 0)
                {
                    var analyzer = new SocialSentimentAnalyzer();
                    var result = analyzer.Analyze(posts);
                    ticker.SentimentScore = (decimal)result.AggregateScore;
                }
            }
            catch
            {
                // Continue without sample posts
            }

            tickers.Add(ticker);
        }

        return tickers;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SocialPost> StreamPostsAsync(
        IEnumerable<string> symbols,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var symbolList = symbols.ToList();
        var lastSeenIds = new Dictionary<string, long>();

        // StockTwits doesn't have true streaming, so we poll
        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var symbol in symbolList)
            {
                var posts = await GetPostsAsync(symbol, null, 20, cancellationToken)
                    .ConfigureAwait(false);

                foreach (var post in posts.OrderBy(p => p.Timestamp))
                {
                    if (long.TryParse(post.Id, out var postId))
                    {
                        if (!lastSeenIds.TryGetValue(symbol, out var lastId) || postId > lastId)
                        {
                            lastSeenIds[symbol] = postId;
                            yield return post;
                        }
                    }
                    else
                    {
                        yield return post;
                    }
                }
            }

            // Poll every 30 seconds
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<SocialPost>>> GetUserPostsAsync(
        IEnumerable<string> usernames,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, IReadOnlyList<SocialPost>>();

        foreach (var username in usernames)
        {
            try
            {
                var endpoint = $"/streams/user/{username}.json";
                var parameters = new Dictionary<string, string>();

                if (!string.IsNullOrWhiteSpace(_options.AccessToken))
                {
                    parameters["access_token"] = _options.AccessToken;
                }

                var response = await ExecuteRequestAsync<StockTwitsStreamResponse>(
                    endpoint, parameters, cancellationToken).ConfigureAwait(false);

                if (response?.Messages is not null)
                {
                    var posts = response.Messages
                        .Take(limit)
                        .Select(m => ConvertToSocialPost(m))
                        .ToList();

                    result[username] = posts;
                }
                else
                {
                    result[username] = Array.Empty<SocialPost>();
                }
            }
            catch
            {
                result[username] = Array.Empty<SocialPost>();
            }
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
        // StockTwits doesn't have a direct search API
        // If the query looks like a symbol, search for that symbol's stream
        var upperQuery = query.ToUpperInvariant().TrimStart('$');

        if (upperQuery.Length >= 1 && upperQuery.Length <= 5 && upperQuery.All(char.IsLetter))
        {
            return await GetPostsAsync(upperQuery, startTime, limit, cancellationToken)
                .ConfigureAwait(false);
        }

        // For general queries, return empty (StockTwits requires auth for search)
        return Array.Empty<SocialPost>();
    }

    /// <summary>
    /// Gets detailed symbol information from StockTwits.
    /// </summary>
    public async Task<StockTwitsSymbolInfo?> GetSymbolInfoAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var endpoint = $"/streams/symbol/{symbol.ToUpperInvariant()}.json";
        var response = await ExecuteRequestAsync<StockTwitsStreamResponse>(
            endpoint, new Dictionary<string, string>(), cancellationToken).ConfigureAwait(false);

        return response?.Symbol;
    }

    private async Task<T?> ExecuteRequestAsync<T>(
        string endpoint,
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        await RateLimitAsync(cancellationToken).ConfigureAwait(false);

        var queryString = parameters.Count > 0
            ? "?" + string.Join("&", parameters.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"))
            : string.Empty;

        var requestUri = $"{endpoint}{queryString}";

        for (int attempt = 0; attempt <= _options.RetryOptions.MaxRetries; attempt++)
        {
            try
            {
                var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken)
                        .ConfigureAwait(false);
                    return JsonSerializer.Deserialize<T>(content, _jsonOptions);
                }

                if ((int)response.StatusCode == 429) // Rate limited
                {
                    var delay = CalculateBackoff(attempt);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // StockTwits returns 404 for symbols with no messages
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return default;
                }

                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException) when (attempt < _options.RetryOptions.MaxRetries)
            {
                var delay = CalculateBackoff(attempt);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        return default;
    }

    private async Task RateLimitAsync(CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // StockTwits: 200 requests per hour without auth, 400 with auth
            var minInterval = TimeSpan.FromMinutes(1) / (_options.MaxRequestsPerMinute > 0 ? _options.MaxRequestsPerMinute : 3);
            var elapsed = DateTime.UtcNow - _lastRequestTime;

            if (elapsed < minInterval)
            {
                await Task.Delay(minInterval - elapsed, cancellationToken).ConfigureAwait(false);
            }

            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private TimeSpan CalculateBackoff(int attempt)
    {
        var delay = TimeSpan.FromSeconds(
            _options.RetryOptions.InitialDelay.TotalSeconds *
            Math.Pow(_options.RetryOptions.BackoffMultiplier, attempt));

        return delay > _options.RetryOptions.MaxDelay ? _options.RetryOptions.MaxDelay : delay;
    }

    private SocialPost ConvertToSocialPost(StockTwitsMessage message, string? symbol = null)
    {
        var cashtags = message.Symbols?.Select(s => s.Symbol ?? string.Empty).ToList() ?? new List<string>();
        if (!string.IsNullOrWhiteSpace(symbol) && !cashtags.Contains(symbol, StringComparer.OrdinalIgnoreCase))
        {
            cashtags.Add(symbol.ToUpperInvariant());
        }

        var hashtags = ExtractHashtags(message.Body ?? string.Empty);

        return new SocialPost
        {
            Id = message.Id.ToString(),
            Platform = SocialPlatform.StockTwits,
            Username = message.User?.Username ?? string.Empty,
            Content = message.Body ?? string.Empty,
            Timestamp = message.CreatedAt,
            Likes = message.LikesCount,
            Retweets = message.ReshareCount,
            Comments = message.ReplyCount,
            FollowerCount = message.User?.Followers ?? 0,
            IsVerified = message.User?.Official ?? false,
            Hashtags = hashtags,
            Cashtags = cashtags
        };
    }

    private static List<string> ExtractHashtags(string content)
    {
        var hashtags = new List<string>();
        var words = content.Split([' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            if (word.StartsWith('#') && word.Length > 1)
            {
                var tag = word.TrimEnd([',', '.', '!', '?', ':', ';']);
                if (tag.Length > 1)
                {
                    hashtags.Add(tag.Substring(1).ToLowerInvariant());
                }
            }
        }

        return hashtags.Distinct().ToList();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _rateLimiter.Dispose();
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// StockTwits symbol information.
/// </summary>
public sealed class StockTwitsSymbolInfo
{
    /// <summary>Gets or sets the symbol ID.</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>Gets or sets the symbol.</summary>
    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    /// <summary>Gets or sets the title/name.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>Gets or sets the watchlist count.</summary>
    [JsonPropertyName("watchlist_count")]
    public int WatchlistCount { get; set; }

    /// <summary>Gets or sets whether this is trending.</summary>
    [JsonPropertyName("is_trending")]
    public bool IsTrending { get; set; }
}

#region StockTwits API Response Models

internal sealed class StockTwitsStreamResponse
{
    [JsonPropertyName("response")]
    public StockTwitsResponseInfo? Response { get; set; }

    [JsonPropertyName("symbol")]
    public StockTwitsSymbolInfo? Symbol { get; set; }

    [JsonPropertyName("messages")]
    public IReadOnlyList<StockTwitsMessage>? Messages { get; set; }

    [JsonPropertyName("cursor")]
    public StockTwitsCursor? Cursor { get; set; }
}

internal sealed class StockTwitsResponseInfo
{
    [JsonPropertyName("status")]
    public int Status { get; set; }
}

internal sealed class StockTwitsMessage
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("user")]
    public StockTwitsUser? User { get; set; }

    [JsonPropertyName("symbols")]
    public IReadOnlyList<StockTwitsSymbolRef>? Symbols { get; set; }

    [JsonPropertyName("likes")]
    public StockTwitsLikes? Likes { get; set; }

    [JsonPropertyName("sentiment")]
    public StockTwitsSentiment? Sentiment { get; set; }

    [JsonIgnore]
    public int LikesCount => Likes?.Total ?? 0;

    [JsonPropertyName("reshare_count")]
    public int ReshareCount { get; set; }

    [JsonPropertyName("reply_count")]
    public int ReplyCount { get; set; }
}

internal sealed class StockTwitsUser
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("followers")]
    public int Followers { get; set; }

    [JsonPropertyName("following")]
    public int Following { get; set; }

    [JsonPropertyName("official")]
    public bool Official { get; set; }

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; set; }
}

internal sealed class StockTwitsSymbolRef
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}

internal sealed class StockTwitsLikes
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("user_ids")]
    public IReadOnlyList<int>? UserIds { get; set; }
}

internal sealed class StockTwitsSentiment
{
    [JsonPropertyName("basic")]
    public string? Basic { get; set; } // "Bullish", "Bearish", null
}

internal sealed class StockTwitsCursor
{
    [JsonPropertyName("more")]
    public bool More { get; set; }

    [JsonPropertyName("since")]
    public long Since { get; set; }

    [JsonPropertyName("max")]
    public long Max { get; set; }
}

internal sealed class StockTwitsTrendingResponse
{
    [JsonPropertyName("symbols")]
    public IReadOnlyList<StockTwitsTrendingSymbol>? Symbols { get; set; }
}

internal sealed class StockTwitsTrendingSymbol
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("watchlist_count")]
    public int WatchlistCount { get; set; }
}

#endregion
