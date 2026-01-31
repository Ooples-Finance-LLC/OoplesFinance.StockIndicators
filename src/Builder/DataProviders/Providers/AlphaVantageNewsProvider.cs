using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OoplesFinance.StockIndicators.Builder.DataProviders.Providers;

/// <summary>
/// News provider implementation for Alpha Vantage News & Sentiment API.
/// Alpha Vantage provides news with AI-powered sentiment analysis.
/// Note: Free tier limited to 25 requests/day, premium required for production use.
/// </summary>
public sealed class AlphaVantageNewsProvider : INewsProvider
{
    private readonly HttpClient _httpClient;
    private readonly NewsProviderOptions _options;
    private readonly SemaphoreSlim _rateLimiter;
    private readonly JsonSerializerOptions _jsonOptions;
    private DateTime _lastRequestTime = DateTime.MinValue;
    private int _requestsToday;
    private DateTime _requestCountResetTime = DateTime.UtcNow.Date;
    private bool _disposed;

    private const string BaseUrl = "https://www.alphavantage.co/query";
    private const int FreeTierDailyLimit = 25;

    /// <summary>
    /// Creates a new Alpha Vantage news provider.
    /// </summary>
    /// <param name="options">Provider options including API key.</param>
    /// <param name="httpClient">Optional HTTP client (for testing).</param>
    public AlphaVantageNewsProvider(NewsProviderOptions options, HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new ArgumentException("API key is required for Alpha Vantage", nameof(options));
        }

        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress = new Uri(options.BaseUrl ?? BaseUrl);
        _httpClient.Timeout = options.Timeout;
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        _rateLimiter = new SemaphoreSlim(1, 1);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <inheritdoc />
    public string ProviderName => "AlphaVantage";

    /// <inheritdoc />
    public bool IsConnected => !_disposed && !string.IsNullOrWhiteSpace(_options.ApiKey);

    /// <inheritdoc />
    public async Task<IReadOnlyList<NewsArticleData>> GetNewsAsync(
        string symbol,
        DateTime startDate,
        DateTime endDate,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["function"] = "NEWS_SENTIMENT",
            ["apikey"] = _options.ApiKey,
            ["tickers"] = symbol.ToUpperInvariant(),
            ["time_from"] = startDate.ToString("yyyyMMddTHHmm"),
            ["time_to"] = endDate.ToString("yyyyMMddTHHmm"),
            ["limit"] = Math.Min(limit, 1000).ToString(),
            ["sort"] = "LATEST"
        };

        var response = await ExecuteRequestAsync<AlphaVantageNewsResponse>(
            parameters, cancellationToken).ConfigureAwait(false);

        return ConvertToArticles(response?.Feed ?? Array.Empty<AlphaVantageArticle>(), symbol);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NewsArticleData>> GetLatestNewsAsync(
        string symbol,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["function"] = "NEWS_SENTIMENT",
            ["apikey"] = _options.ApiKey,
            ["tickers"] = symbol.ToUpperInvariant(),
            ["limit"] = Math.Min(limit, 50).ToString(),
            ["sort"] = "LATEST"
        };

        var response = await ExecuteRequestAsync<AlphaVantageNewsResponse>(
            parameters, cancellationToken).ConfigureAwait(false);

        return ConvertToArticles(response?.Feed ?? Array.Empty<AlphaVantageArticle>(), symbol);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<NewsArticleData>>> GetLatestNewsAsync(
        IEnumerable<string> symbols,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var symbolList = symbols.ToList();

        // Alpha Vantage supports multiple tickers separated by comma
        var parameters = new Dictionary<string, string>
        {
            ["function"] = "NEWS_SENTIMENT",
            ["apikey"] = _options.ApiKey,
            ["tickers"] = string.Join(",", symbolList.Select(s => s.ToUpperInvariant())),
            ["limit"] = Math.Min(limit * symbolList.Count, 200).ToString(),
            ["sort"] = "LATEST"
        };

        var response = await ExecuteRequestAsync<AlphaVantageNewsResponse>(
            parameters, cancellationToken).ConfigureAwait(false);

        var allArticles = response?.Feed ?? Array.Empty<AlphaVantageArticle>();

        // Group by symbol
        var result = new Dictionary<string, IReadOnlyList<NewsArticleData>>();
        foreach (var symbol in symbolList)
        {
            var symbolArticles = allArticles
                .Where(a => a.TickerSentiment?.Any(ts =>
                    ts.Ticker?.Equals(symbol, StringComparison.OrdinalIgnoreCase) ?? false) ?? false)
                .Take(limit)
                .Select(a => ConvertToArticle(a, symbol))
                .ToList();

            result[symbol] = symbolArticles;
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
        // Alpha Vantage doesn't have a direct keyword search, but we can search by topics
        var parameters = new Dictionary<string, string>
        {
            ["function"] = "NEWS_SENTIMENT",
            ["apikey"] = _options.ApiKey,
            ["limit"] = Math.Min(limit, 200).ToString(),
            ["sort"] = "LATEST"
        };

        // Map common search terms to topics
        var topics = MapQueryToTopics(query);
        if (topics.Count > 0)
        {
            parameters["topics"] = string.Join(",", topics);
        }

        if (startDate.HasValue)
        {
            parameters["time_from"] = startDate.Value.ToString("yyyyMMddTHHmm");
        }

        if (endDate.HasValue)
        {
            parameters["time_to"] = endDate.Value.ToString("yyyyMMddTHHmm");
        }

        var response = await ExecuteRequestAsync<AlphaVantageNewsResponse>(
            parameters, cancellationToken).ConfigureAwait(false);

        var articles = ConvertToArticles(response?.Feed ?? Array.Empty<AlphaVantageArticle>());

        // Further filter by query keyword if present
        if (!string.IsNullOrWhiteSpace(query))
        {
            articles = articles
                .Where(a =>
                    a.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    a.Summary.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return articles;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<NewsArticleData> StreamNewsAsync(
        IEnumerable<string> symbols,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var symbolList = symbols.ToList();
        var lastSeenUrls = new HashSet<string>();

        // Alpha Vantage doesn't have true streaming, so we poll
        // With free tier, we need to be very conservative
        while (!cancellationToken.IsCancellationRequested)
        {
            // Check daily limit
            ResetDailyCounterIfNeeded();
            if (_requestsToday >= FreeTierDailyLimit)
            {
                // Wait until tomorrow
                var tomorrow = DateTime.UtcNow.Date.AddDays(1);
                var delay = tomorrow - DateTime.UtcNow;
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var articles = await GetLatestNewsAsync(
                string.Join(",", symbolList),
                limit: 10,
                cancellationToken).ConfigureAwait(false);

            foreach (var article in articles.OrderBy(a => a.PublishedAt))
            {
                if (lastSeenUrls.Add(article.Url))
                {
                    yield return article;
                }
            }

            // Limit to max 25 requests/day, poll every 1 hour with free tier
            await Task.Delay(TimeSpan.FromHours(1), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NewsArticleData>> GetMarketNewsAsync(
        NewsCategory? category = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["function"] = "NEWS_SENTIMENT",
            ["apikey"] = _options.ApiKey,
            ["limit"] = Math.Min(limit, 200).ToString(),
            ["sort"] = "LATEST"
        };

        if (category.HasValue)
        {
            var topic = MapCategoryToTopic(category.Value);
            if (!string.IsNullOrEmpty(topic))
            {
                parameters["topics"] = topic;
            }
        }

        var response = await ExecuteRequestAsync<AlphaVantageNewsResponse>(
            parameters, cancellationToken).ConfigureAwait(false);

        return ConvertToArticles(response?.Feed ?? Array.Empty<AlphaVantageArticle>());
    }

    /// <summary>
    /// Gets the overall market sentiment from Alpha Vantage.
    /// </summary>
    public async Task<MarketSentimentSummary> GetMarketSentimentAsync(
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["function"] = "NEWS_SENTIMENT",
            ["apikey"] = _options.ApiKey,
            ["limit"] = "200",
            ["sort"] = "LATEST"
        };

        var response = await ExecuteRequestAsync<AlphaVantageNewsResponse>(
            parameters, cancellationToken).ConfigureAwait(false);

        if (response?.Feed is null || response.Feed.Count == 0)
        {
            return new MarketSentimentSummary();
        }

        var overallSentiment = response.Feed
            .Where(a => a.OverallSentimentScore.HasValue)
            .Average(a => a.OverallSentimentScore!.Value);

        var sentimentBySource = response.Feed
            .Where(a => !string.IsNullOrWhiteSpace(a.Source) && a.OverallSentimentScore.HasValue)
            .GroupBy(a => a.Source!)
            .ToDictionary(
                g => g.Key,
                g => g.Average(a => a.OverallSentimentScore!.Value));

        return new MarketSentimentSummary
        {
            OverallSentiment = (decimal)overallSentiment,
            ArticleCount = response.Feed.Count,
            SentimentBySource = sentimentBySource.ToDictionary(kvp => kvp.Key, kvp => (decimal)kvp.Value),
            Timestamp = DateTime.UtcNow
        };
    }

    private void ResetDailyCounterIfNeeded()
    {
        var today = DateTime.UtcNow.Date;
        if (today > _requestCountResetTime)
        {
            _requestsToday = 0;
            _requestCountResetTime = today;
        }
    }

    private async Task<T?> ExecuteRequestAsync<T>(
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        await RateLimitAsync(cancellationToken).ConfigureAwait(false);

        var queryString = string.Join("&",
            parameters.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        var requestUri = $"?{queryString}";

        for (int attempt = 0; attempt <= _options.RetryOptions.MaxRetries; attempt++)
        {
            try
            {
                var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    _requestsToday++;
                    var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                    // Check for API error messages in response
                    if (content.Contains("\"Error Message\"") || content.Contains("\"Note\""))
                    {
                        // API rate limit hit or error
                        if (content.Contains("premium"))
                        {
                            throw new InvalidOperationException(
                                "Alpha Vantage free tier limit reached. Please upgrade to premium.");
                        }
                    }

                    return JsonSerializer.Deserialize<T>(content, _jsonOptions);
                }

                if ((int)response.StatusCode == 429) // Too Many Requests
                {
                    var delay = CalculateBackoff(attempt);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
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
            // Alpha Vantage standard rate limit: 5 calls per minute
            var minInterval = TimeSpan.FromSeconds(12);
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

    private IReadOnlyList<NewsArticleData> ConvertToArticles(
        IReadOnlyList<AlphaVantageArticle> alphaArticles,
        string? filterSymbol = null)
    {
        return alphaArticles.Select(a => ConvertToArticle(a, filterSymbol)).ToList();
    }

    private NewsArticleData ConvertToArticle(AlphaVantageArticle a, string? filterSymbol = null)
    {
        // Get sentiment for specific ticker if filtering
        decimal? sentiment = null;
        if (!string.IsNullOrWhiteSpace(filterSymbol) && a.TickerSentiment?.Count > 0)
        {
            var tickerSent = a.TickerSentiment
                .FirstOrDefault(ts => ts.Ticker?.Equals(filterSymbol, StringComparison.OrdinalIgnoreCase) ?? false);
            if (tickerSent is not null && decimal.TryParse(tickerSent.TickerSentimentScore, out var score))
            {
                sentiment = score;
            }
        }
        else if (a.OverallSentimentScore.HasValue)
        {
            sentiment = (decimal)a.OverallSentimentScore.Value;
        }

        var symbols = a.TickerSentiment?
            .Where(ts => !string.IsNullOrWhiteSpace(ts.Ticker))
            .Select(ts => ts.Ticker!)
            .Distinct()
            .ToList() ?? new List<string>();

        var topics = a.Topics?
            .Where(t => !string.IsNullOrWhiteSpace(t.Topic))
            .Select(t => t.Topic!)
            .ToList() ?? new List<string>();

        return new NewsArticleData
        {
            Id = a.Url ?? Guid.NewGuid().ToString(),
            Title = a.Title ?? string.Empty,
            Content = string.Empty, // Alpha Vantage doesn't provide full content
            Summary = a.Summary ?? string.Empty,
            Source = a.Source ?? "Unknown",
            Url = a.Url ?? string.Empty,
            Author = a.Authors?.FirstOrDefault() ?? string.Empty,
            PublishedAt = ParseAlphaVantageDate(a.TimePublished),
            Symbols = symbols,
            Topics = topics,
            SourceSentiment = sentiment,
            Category = MapTopicsToCategory(topics),
            ImageUrl = a.BannerImage,
            Language = "en",
            IsBreaking = false,
            Provider = ProviderName
        };
    }

    private static DateTime ParseAlphaVantageDate(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return DateTime.UtcNow;

        // Format: "20231215T120000"
        if (DateTime.TryParseExact(dateString, "yyyyMMddTHHmmss",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal,
            out var parsed))
        {
            return parsed;
        }

        return DateTime.UtcNow;
    }

    private static List<string> MapQueryToTopics(string query)
    {
        var topics = new List<string>();
        var lowerQuery = query.ToLowerInvariant();

        if (lowerQuery.Contains("earning") || lowerQuery.Contains("eps"))
            topics.Add("earnings");
        if (lowerQuery.Contains("ipo") || lowerQuery.Contains("initial public"))
            topics.Add("ipo");
        if (lowerQuery.Contains("merger") || lowerQuery.Contains("acquisition") || lowerQuery.Contains("m&a"))
            topics.Add("mergers_and_acquisitions");
        if (lowerQuery.Contains("tech") || lowerQuery.Contains("technology"))
            topics.Add("technology");
        if (lowerQuery.Contains("blockchain") || lowerQuery.Contains("crypto"))
            topics.Add("blockchain");
        if (lowerQuery.Contains("financial") || lowerQuery.Contains("bank"))
            topics.Add("finance");
        if (lowerQuery.Contains("energy") || lowerQuery.Contains("oil"))
            topics.Add("energy_transportation");
        if (lowerQuery.Contains("healthcare") || lowerQuery.Contains("pharma"))
            topics.Add("life_sciences");
        if (lowerQuery.Contains("retail") || lowerQuery.Contains("consumer"))
            topics.Add("retail_wholesale");
        if (lowerQuery.Contains("economy") || lowerQuery.Contains("fed") || lowerQuery.Contains("gdp"))
            topics.Add("economy_macro");

        return topics;
    }

    private static string MapCategoryToTopic(NewsCategory category)
    {
        return category switch
        {
            NewsCategory.Earnings => "earnings",
            NewsCategory.MergersAcquisitions => "mergers_and_acquisitions",
            NewsCategory.IPO => "ipo",
            NewsCategory.Economic => "economy_macro",
            NewsCategory.Crypto => "blockchain",
            NewsCategory.Technology => "technology",
            NewsCategory.Healthcare => "life_sciences",
            NewsCategory.Energy => "energy_transportation",
            NewsCategory.Financial => "finance",
            NewsCategory.Consumer => "retail_wholesale",
            _ => string.Empty
        };
    }

    private static NewsCategory MapTopicsToCategory(IReadOnlyList<string> topics)
    {
        if (topics.Count == 0)
            return NewsCategory.General;

        var firstTopic = topics[0].ToLowerInvariant();

        return firstTopic switch
        {
            "earnings" => NewsCategory.Earnings,
            "mergers_and_acquisitions" => NewsCategory.MergersAcquisitions,
            "ipo" => NewsCategory.IPO,
            "economy_macro" or "economy_monetary" or "economy_fiscal" => NewsCategory.Economic,
            "blockchain" => NewsCategory.Crypto,
            "technology" => NewsCategory.Technology,
            "life_sciences" => NewsCategory.Healthcare,
            "energy_transportation" => NewsCategory.Energy,
            "finance" => NewsCategory.Financial,
            "retail_wholesale" => NewsCategory.Consumer,
            _ => NewsCategory.General
        };
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
/// Market sentiment summary from Alpha Vantage.
/// </summary>
public sealed class MarketSentimentSummary
{
    /// <summary>Gets or sets the overall market sentiment (-1 to 1).</summary>
    public decimal OverallSentiment { get; set; }

    /// <summary>Gets or sets the number of articles analyzed.</summary>
    public int ArticleCount { get; set; }

    /// <summary>Gets or sets sentiment by news source.</summary>
    public IReadOnlyDictionary<string, decimal> SentimentBySource { get; set; } =
        new Dictionary<string, decimal>();

    /// <summary>Gets or sets the timestamp of the analysis.</summary>
    public DateTime Timestamp { get; set; }
}

#region Alpha Vantage API Response Models

internal sealed class AlphaVantageNewsResponse
{
    [JsonPropertyName("feed")]
    public IReadOnlyList<AlphaVantageArticle> Feed { get; set; } = Array.Empty<AlphaVantageArticle>();

    [JsonPropertyName("items")]
    public int? Items { get; set; }

    [JsonPropertyName("sentiment_score_definition")]
    public string? SentimentScoreDefinition { get; set; }

    [JsonPropertyName("relevance_score_definition")]
    public string? RelevanceScoreDefinition { get; set; }
}

internal sealed class AlphaVantageArticle
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("time_published")]
    public string? TimePublished { get; set; }

    [JsonPropertyName("authors")]
    public IReadOnlyList<string>? Authors { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("banner_image")]
    public string? BannerImage { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("category_within_source")]
    public string? CategoryWithinSource { get; set; }

    [JsonPropertyName("source_domain")]
    public string? SourceDomain { get; set; }

    [JsonPropertyName("topics")]
    public IReadOnlyList<AlphaVantageTopic>? Topics { get; set; }

    [JsonPropertyName("overall_sentiment_score")]
    public double? OverallSentimentScore { get; set; }

    [JsonPropertyName("overall_sentiment_label")]
    public string? OverallSentimentLabel { get; set; }

    [JsonPropertyName("ticker_sentiment")]
    public IReadOnlyList<AlphaVantageTickerSentiment>? TickerSentiment { get; set; }
}

internal sealed class AlphaVantageTopic
{
    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    [JsonPropertyName("relevance_score")]
    public string? RelevanceScore { get; set; }
}

internal sealed class AlphaVantageTickerSentiment
{
    [JsonPropertyName("ticker")]
    public string? Ticker { get; set; }

    [JsonPropertyName("relevance_score")]
    public string? RelevanceScore { get; set; }

    [JsonPropertyName("ticker_sentiment_score")]
    public string? TickerSentimentScore { get; set; }

    [JsonPropertyName("ticker_sentiment_label")]
    public string? TickerSentimentLabel { get; set; }
}

#endregion
