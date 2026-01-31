using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OoplesFinance.StockIndicators.Builder.DataProviders.Providers;

/// <summary>
/// News provider implementation for Benzinga API.
/// Benzinga provides real-time financial news, analyst ratings, and SEC filings.
/// </summary>
public sealed class BenzingaNewsProvider : INewsProvider
{
    private readonly HttpClient _httpClient;
    private readonly NewsProviderOptions _options;
    private readonly SemaphoreSlim _rateLimiter;
    private readonly JsonSerializerOptions _jsonOptions;
    private DateTime _lastRequestTime = DateTime.MinValue;
    private bool _disposed;

    private const string BaseUrl = "https://api.benzinga.com/api/v2";

    /// <summary>
    /// Creates a new Benzinga news provider.
    /// </summary>
    /// <param name="options">Provider options including API key.</param>
    /// <param name="httpClient">Optional HTTP client (for testing).</param>
    public BenzingaNewsProvider(NewsProviderOptions options, HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new ArgumentException("API key is required for Benzinga", nameof(options));
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
    public string ProviderName => "Benzinga";

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
            ["token"] = _options.ApiKey,
            ["tickers"] = symbol.ToUpperInvariant(),
            ["dateFrom"] = startDate.ToString("yyyy-MM-dd"),
            ["dateTo"] = endDate.ToString("yyyy-MM-dd"),
            ["pageSize"] = Math.Min(limit, 100).ToString(),
            ["displayOutput"] = "full"
        };

        var response = await ExecuteRequestAsync<BenzingaNewsResponse>(
            "/news", parameters, cancellationToken).ConfigureAwait(false);

        return ConvertToArticles(response?.Articles ?? Array.Empty<BenzingaArticle>());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NewsArticleData>> GetLatestNewsAsync(
        string symbol,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["token"] = _options.ApiKey,
            ["tickers"] = symbol.ToUpperInvariant(),
            ["pageSize"] = Math.Min(limit, 100).ToString(),
            ["displayOutput"] = "full"
        };

        var response = await ExecuteRequestAsync<BenzingaNewsResponse>(
            "/news", parameters, cancellationToken).ConfigureAwait(false);

        return ConvertToArticles(response?.Articles ?? Array.Empty<BenzingaArticle>());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<NewsArticleData>>> GetLatestNewsAsync(
        IEnumerable<string> symbols,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var symbolList = symbols.ToList();

        // Benzinga supports multiple tickers in one request
        var parameters = new Dictionary<string, string>
        {
            ["token"] = _options.ApiKey,
            ["tickers"] = string.Join(",", symbolList.Select(s => s.ToUpperInvariant())),
            ["pageSize"] = Math.Min(limit * symbolList.Count, 100).ToString(),
            ["displayOutput"] = "full"
        };

        var response = await ExecuteRequestAsync<BenzingaNewsResponse>(
            "/news", parameters, cancellationToken).ConfigureAwait(false);

        var articles = ConvertToArticles(response?.Articles ?? Array.Empty<BenzingaArticle>());

        // Group by symbol
        var result = new Dictionary<string, IReadOnlyList<NewsArticleData>>();
        foreach (var symbol in symbolList)
        {
            var symbolArticles = articles
                .Where(a => a.Symbols.Contains(symbol, StringComparer.OrdinalIgnoreCase))
                .Take(limit)
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
        var parameters = new Dictionary<string, string>
        {
            ["token"] = _options.ApiKey,
            ["q"] = query,
            ["pageSize"] = Math.Min(limit, 100).ToString(),
            ["displayOutput"] = "full"
        };

        if (startDate.HasValue)
        {
            parameters["dateFrom"] = startDate.Value.ToString("yyyy-MM-dd");
        }

        if (endDate.HasValue)
        {
            parameters["dateTo"] = endDate.Value.ToString("yyyy-MM-dd");
        }

        var response = await ExecuteRequestAsync<BenzingaNewsResponse>(
            "/news", parameters, cancellationToken).ConfigureAwait(false);

        return ConvertToArticles(response?.Articles ?? Array.Empty<BenzingaArticle>());
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<NewsArticleData> StreamNewsAsync(
        IEnumerable<string> symbols,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var symbolList = symbols.ToList();
        var lastSeenId = string.Empty;

        // Benzinga doesn't have true streaming, so we poll
        while (!cancellationToken.IsCancellationRequested)
        {
            var articles = await GetLatestNewsAsync(
                string.Join(",", symbolList),
                limit: 20,
                cancellationToken).ConfigureAwait(false);

            foreach (var article in articles.OrderBy(a => a.PublishedAt))
            {
                if (string.Compare(article.Id, lastSeenId, StringComparison.Ordinal) > 0)
                {
                    lastSeenId = article.Id;
                    yield return article;
                }
            }

            // Poll every 30 seconds
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
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
            ["token"] = _options.ApiKey,
            ["pageSize"] = Math.Min(limit, 100).ToString(),
            ["displayOutput"] = "full"
        };

        if (category.HasValue)
        {
            parameters["channels"] = MapCategoryToChannel(category.Value);
        }

        var response = await ExecuteRequestAsync<BenzingaNewsResponse>(
            "/news", parameters, cancellationToken).ConfigureAwait(false);

        return ConvertToArticles(response?.Articles ?? Array.Empty<BenzingaArticle>());
    }

    /// <summary>
    /// Gets analyst ratings for a symbol.
    /// </summary>
    public async Task<IReadOnlyList<AnalystRating>> GetAnalystRatingsAsync(
        string symbol,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["token"] = _options.ApiKey,
            ["tickers"] = symbol.ToUpperInvariant(),
            ["pageSize"] = Math.Min(limit, 100).ToString()
        };

        var response = await ExecuteRequestAsync<BenzingaRatingsResponse>(
            "/calendar/ratings", parameters, cancellationToken).ConfigureAwait(false);

        if (response?.Ratings is null)
            return new List<AnalystRating>();

        return response.Ratings.Select(r => new AnalystRating
        {
            Symbol = r.Ticker ?? string.Empty,
            Analyst = r.AnalystName ?? string.Empty,
            Firm = r.Firm ?? string.Empty,
            Action = MapRatingAction(r.Action),
            CurrentRating = r.RatingCurrent ?? string.Empty,
            PreviousRating = r.RatingPrior ?? string.Empty,
            CurrentPriceTarget = r.PtCurrent,
            PreviousPriceTarget = r.PtPrior,
            Date = r.Date,
            Provider = ProviderName
        }).ToList();
    }

    private async Task<T?> ExecuteRequestAsync<T>(
        string endpoint,
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        await RateLimitAsync(cancellationToken).ConfigureAwait(false);

        var queryString = string.Join("&",
            parameters.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        var requestUri = $"{endpoint}?{queryString}";

        for (int attempt = 0; attempt <= _options.RetryOptions.MaxRetries; attempt++)
        {
            try
            {
                var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
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
            var minInterval = TimeSpan.FromMinutes(1) / _options.MaxRequestsPerMinute;
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

    private IReadOnlyList<NewsArticleData> ConvertToArticles(IReadOnlyList<BenzingaArticle> benzingaArticles)
    {
        return benzingaArticles.Select(a => new NewsArticleData
        {
            Id = a.Id.ToString(),
            Title = a.Title ?? string.Empty,
            Content = a.Body ?? string.Empty,
            Summary = a.Teaser ?? string.Empty,
            Source = a.Author ?? "Benzinga",
            Url = a.Url ?? string.Empty,
            Author = a.Author ?? string.Empty,
            PublishedAt = a.Created,
            Symbols = a.Stocks?.Select(s => s.Name).ToList() ?? new List<string>(),
            Topics = a.Channels?.Select(c => c.Name).ToList() ?? new List<string>(),
            Category = MapChannelToCategory(a.Channels?.FirstOrDefault()?.Name),
            ImageUrl = a.Image?.FirstOrDefault()?.Url,
            Language = "en",
            IsBreaking = a.Title?.Contains("Breaking:", StringComparison.OrdinalIgnoreCase) ?? false,
            Provider = ProviderName
        }).ToList();
    }

    private static string MapCategoryToChannel(NewsCategory category)
    {
        return category switch
        {
            NewsCategory.Earnings => "Earnings",
            NewsCategory.MergersAcquisitions => "M&A",
            NewsCategory.IPO => "IPOs",
            NewsCategory.AnalystRatings => "Analyst Ratings",
            NewsCategory.Regulatory => "SEC",
            NewsCategory.Economic => "Economics",
            NewsCategory.Crypto => "Cryptocurrency",
            NewsCategory.Technology => "Tech",
            NewsCategory.Healthcare => "Healthcare",
            NewsCategory.Energy => "Energy",
            NewsCategory.Financial => "Financials",
            NewsCategory.InsiderTrading => "Insider Trades",
            NewsCategory.OptionsActivity => "Options",
            NewsCategory.ShortInterest => "Short Selling",
            _ => "News"
        };
    }

    private static NewsCategory MapChannelToCategory(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
            return NewsCategory.General;

        return channel.ToLowerInvariant() switch
        {
            "earnings" => NewsCategory.Earnings,
            "m&a" or "mergers" => NewsCategory.MergersAcquisitions,
            "ipos" => NewsCategory.IPO,
            "analyst ratings" or "ratings" => NewsCategory.AnalystRatings,
            "sec" or "regulatory" => NewsCategory.Regulatory,
            "economics" or "economic" => NewsCategory.Economic,
            "cryptocurrency" or "crypto" => NewsCategory.Crypto,
            "tech" or "technology" => NewsCategory.Technology,
            "healthcare" => NewsCategory.Healthcare,
            "energy" => NewsCategory.Energy,
            "financials" or "financial" => NewsCategory.Financial,
            "insider trades" => NewsCategory.InsiderTrading,
            "options" => NewsCategory.OptionsActivity,
            "short selling" => NewsCategory.ShortInterest,
            _ => NewsCategory.General
        };
    }

    private static RatingAction MapRatingAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return RatingAction.Maintains;

        return action.ToLowerInvariant() switch
        {
            "upgrades" or "upgrade" => RatingAction.Upgrades,
            "downgrades" or "downgrade" => RatingAction.Downgrades,
            "initiates" or "initiate" or "initiates coverage on" => RatingAction.Initiates,
            "reiterates" or "reiterate" => RatingAction.Reiterates,
            "maintains" or "maintain" => RatingAction.Maintains,
            _ => RatingAction.Maintains
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
/// Analyst rating information.
/// </summary>
public sealed class AnalystRating
{
    /// <summary>Gets or sets the stock symbol.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the analyst name.</summary>
    public string Analyst { get; set; } = string.Empty;

    /// <summary>Gets or sets the firm name.</summary>
    public string Firm { get; set; } = string.Empty;

    /// <summary>Gets or sets the rating action.</summary>
    public RatingAction Action { get; set; }

    /// <summary>Gets or sets the current rating.</summary>
    public string CurrentRating { get; set; } = string.Empty;

    /// <summary>Gets or sets the previous rating.</summary>
    public string? PreviousRating { get; set; }

    /// <summary>Gets or sets the current price target.</summary>
    public decimal? CurrentPriceTarget { get; set; }

    /// <summary>Gets or sets the previous price target.</summary>
    public decimal? PreviousPriceTarget { get; set; }

    /// <summary>Gets or sets the rating date.</summary>
    public DateTime Date { get; set; }

    /// <summary>Gets or sets the data provider.</summary>
    public string Provider { get; set; } = string.Empty;
}

/// <summary>
/// Rating action types.
/// </summary>
public enum RatingAction
{
    /// <summary>Analyst upgrades the stock.</summary>
    Upgrades,

    /// <summary>Analyst downgrades the stock.</summary>
    Downgrades,

    /// <summary>Analyst initiates coverage.</summary>
    Initiates,

    /// <summary>Analyst reiterates rating.</summary>
    Reiterates,

    /// <summary>Analyst maintains rating.</summary>
    Maintains
}

#region Benzinga API Response Models

internal sealed class BenzingaNewsResponse
{
    [JsonPropertyName("articles")]
    public IReadOnlyList<BenzingaArticle> Articles { get; set; } = Array.Empty<BenzingaArticle>();
}

internal sealed class BenzingaArticle
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("teaser")]
    public string? Teaser { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    [JsonPropertyName("updated")]
    public DateTime? Updated { get; set; }

    [JsonPropertyName("stocks")]
    public IReadOnlyList<BenzingaStock>? Stocks { get; set; }

    [JsonPropertyName("channels")]
    public IReadOnlyList<BenzingaChannel>? Channels { get; set; }

    [JsonPropertyName("image")]
    public IReadOnlyList<BenzingaImage>? Image { get; set; }
}

internal sealed class BenzingaStock
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

internal sealed class BenzingaChannel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

internal sealed class BenzingaImage
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

internal sealed class BenzingaRatingsResponse
{
    [JsonPropertyName("ratings")]
    public IReadOnlyList<BenzingaRating> Ratings { get; set; } = Array.Empty<BenzingaRating>();
}

internal sealed class BenzingaRating
{
    [JsonPropertyName("ticker")]
    public string Ticker { get; set; } = string.Empty;

    [JsonPropertyName("analyst")]
    public string? AnalystName { get; set; }

    [JsonPropertyName("analyst_name")]
    public string? AnalystNameAlt { get; set; }

    [JsonPropertyName("firm")]
    public string Firm { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("rating_current")]
    public string RatingCurrent { get; set; } = string.Empty;

    [JsonPropertyName("rating_prior")]
    public string? RatingPrior { get; set; }

    [JsonPropertyName("pt_current")]
    public decimal? PtCurrent { get; set; }

    [JsonPropertyName("pt_prior")]
    public decimal? PtPrior { get; set; }

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
}

#endregion
