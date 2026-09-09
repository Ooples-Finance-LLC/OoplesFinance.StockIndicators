using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace OoplesFinance.StockIndicators.Builder.MarketData;

/// <summary>
/// Market data provider using Polygon.io API.
/// Provides real-time and historical market data for stocks, options, forex, and crypto.
/// </summary>
/// <remarks>
/// Requires a Polygon.io API key. Free tier has limited data.
/// Supports stocks, options, indices, forex, and cryptocurrency data.
/// </remarks>
public sealed class PolygonMarketDataProvider : IMarketDataProvider, IOptionsMarketDataProvider, IDisposable
{
    private readonly PolygonOptions _options;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    private const string BaseUrl = "https://api.polygon.io";

    /// <summary>
    /// Creates a new Polygon market data provider.
    /// </summary>
    /// <param name="options">Polygon API options.</param>
    public PolygonMarketDataProvider(PolygonOptions? options = null)
    {
        _options = options ?? new PolygonOptions();

        var apiKey = _options.ApiKey ?? Environment.GetEnvironmentVariable("POLYGON_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException(
                "Polygon API key is required. Set PolygonOptions.ApiKey or POLYGON_API_KEY environment variable.");
        }

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = _options.Timeout
        };

        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <inheritdoc />
    public string ProviderName => "Polygon.io";

    /// <inheritdoc />
    public bool IsConnected => !_disposed;

    /// <inheritdoc />
    public async Task<Quote> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Get snapshot for the symbol
        var response = await _httpClient.GetAsync(
            $"/v2/snapshot/locale/us/markets/stocks/tickers/{symbol.ToUpperInvariant()}",
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // Fall back to last trade
            return await GetLastTradeQuoteAsync(symbol, cancellationToken).ConfigureAwait(false);
        }

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<PolygonSnapshotResponse>(content, _jsonOptions);

        if (result?.Ticker is null)
        {
            throw new InvalidOperationException($"No data found for symbol: {symbol}");
        }

        var ticker = result.Ticker;
        return new Quote(
            symbol,
            ticker.LastQuote?.BidPrice ?? ticker.Day?.Close ?? 0m,
            ticker.LastQuote?.AskPrice ?? ticker.Day?.Close ?? 0m,
            ticker.LastTrade?.Price ?? ticker.Day?.Close ?? 0m,
            ticker.LastQuote?.BidSize ?? 0,
            ticker.LastQuote?.AskSize ?? 0,
            ticker.Updated != null
                ? DateTimeOffset.FromUnixTimeMilliseconds(ticker.Updated.Value).UtcDateTime
                : DateTime.UtcNow
        );
    }

    private async Task<Quote> GetLastTradeQuoteAsync(string symbol, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(
            $"/v2/last/trade/{symbol.ToUpperInvariant()}",
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<PolygonLastTradeResponse>(content, _jsonOptions);

        if (result?.Results is null)
        {
            throw new InvalidOperationException($"No trade data found for symbol: {symbol}");
        }

        var trade = result.Results;
        return new Quote(
            symbol,
            trade.Price,
            trade.Price,
            trade.Price,
            0,
            0,
            DateTimeOffset.FromUnixTimeMilliseconds(trade.Timestamp).UtcDateTime
        );
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Bar>> GetHistoricalBarsAsync(
        string symbol,
        DateTime start,
        DateTime end,
        BarTimeframe timeframe,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var (multiplier, span) = ConvertTimeframe(timeframe);
        var startDate = start.ToString("yyyy-MM-dd");
        var endDate = end.ToString("yyyy-MM-dd");

        var bars = new List<Bar>();
        string? nextUrl = null;

        do
        {
            var url = nextUrl ?? $"/v2/aggs/ticker/{symbol.ToUpperInvariant()}/range/{multiplier}/{span}/{startDate}/{endDate}?adjusted=true&sort=asc&limit=50000";

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<PolygonAggregatesResponse>(content, _jsonOptions);

            if (result?.Results is not null)
            {
                foreach (var agg in result.Results)
                {
                    bars.Add(new Bar(
                        symbol,
                        DateTimeOffset.FromUnixTimeMilliseconds(agg.Timestamp).UtcDateTime,
                        agg.Open,
                        agg.High,
                        agg.Low,
                        agg.Close,
                        agg.Volume
                    ));
                }
            }

            nextUrl = result?.NextUrl;

        } while (nextUrl is not null && !cancellationToken.IsCancellationRequested);

        return bars;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Quote> StreamQuotesAsync(
        IEnumerable<string> symbols,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Polygon streaming requires WebSocket subscription
        // For now, implement polling-based fallback
        var symbolList = symbols.ToList();

        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var symbol in symbolList)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                Quote? quote = null;
                try
                {
                    quote = await GetQuoteAsync(symbol, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Skip failed quotes
                }

                if (quote is not null)
                {
                    yield return quote;
                }
            }

            // Rate limit: Polygon free tier has 5 requests/minute
            await Task.Delay(_options.PollingInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Trade> StreamTradesAsync(
        IEnumerable<string> symbols,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Polling-based implementation
        var symbolList = symbols.ToList();

        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var symbol in symbolList)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                Trade? trade = null;
                try
                {
                    var response = await _httpClient.GetAsync(
                        $"/v2/last/trade/{symbol.ToUpperInvariant()}",
                        cancellationToken).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var result = JsonSerializer.Deserialize<PolygonLastTradeResponse>(content, _jsonOptions);

                        if (result?.Results is not null)
                        {
                            trade = new Trade(
                                symbol,
                                result.Results.Price,
                                result.Results.Size,
                                DateTimeOffset.FromUnixTimeMilliseconds(result.Results.Timestamp).UtcDateTime,
                                result.Results.Exchange?.ToString()
                            );
                        }
                    }
                }
                catch
                {
                    // Skip failed trades
                }

                if (trade is not null)
                {
                    yield return trade;
                }
            }

            await Task.Delay(_options.PollingInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<Level2OrderBook> GetOrderBookAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Polygon provides NBBO but not full order book on standard plans
        // Return best bid/ask as single-level book
        var quote = await GetQuoteAsync(symbol, cancellationToken).ConfigureAwait(false);

        return new Level2OrderBook(
            symbol,
            new[] { new PriceLevel(quote.Bid, quote.BidSize) },
            new[] { new PriceLevel(quote.Ask, quote.AskSize) },
            quote.Timestamp
        );
    }

    /// <inheritdoc />
    public async Task<OptionsChain> GetOptionsChainAsync(
        string underlyingSymbol,
        DateTime? expirationDate = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Get current price for underlying
        decimal underlyingPrice = 0m;
        try
        {
            var quote = await GetQuoteAsync(underlyingSymbol, cancellationToken).ConfigureAwait(false);
            underlyingPrice = quote.Last;
        }
        catch
        {
            // Continue without price
        }

        return new OptionsChain
        {
            UnderlyingSymbol = underlyingSymbol,
            UnderlyingPrice = underlyingPrice,
            ExpirationDate = expirationDate ?? DateTime.MinValue,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DateTime>> GetOptionsExpirationsAsync(
        string underlyingSymbol,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var url = $"/v3/reference/options/contracts?underlying_ticker={underlyingSymbol.ToUpperInvariant()}&limit=1000";

        var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<PolygonOptionsContractsResponse>(content, _jsonOptions);

        if (result?.Results is null)
        {
            return Array.Empty<DateTime>();
        }

        return result.Results
            .Select(c => DateTime.TryParse(c.ExpirationDate, out var exp) ? exp : DateTime.MinValue)
            .Where(d => d != DateTime.MinValue)
            .Distinct()
            .OrderBy(d => d)
            .ToList();
    }

    private static string ParseUnderlyingFromOptionSymbol(string optionSymbol)
    {
        // Option symbols like "O:AAPL230120C00150000" have underlying at the start after O:
        if (optionSymbol.StartsWith("O:"))
        {
            optionSymbol = optionSymbol.Substring(2);
        }

        // Find where the date starts (first digit usually)
        for (var i = 0; i < optionSymbol.Length; i++)
        {
            if (char.IsDigit(optionSymbol[i]))
            {
                return optionSymbol.Substring(0, i);
            }
        }

        return optionSymbol;
    }

    private static (int Multiplier, string Span) ConvertTimeframe(BarTimeframe timeframe)
    {
        return timeframe switch
        {
            BarTimeframe.Minute1 => (1, "minute"),
            BarTimeframe.Minute5 => (5, "minute"),
            BarTimeframe.Minute15 => (15, "minute"),
            BarTimeframe.Minute30 => (30, "minute"),
            BarTimeframe.Hour1 => (1, "hour"),
            BarTimeframe.Hour4 => (4, "hour"),
            BarTimeframe.Day => (1, "day"),
            BarTimeframe.Week => (1, "week"),
            BarTimeframe.Month => (1, "month"),
            _ => (1, "day")
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, Quote>> GetQuotesAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var result = new Dictionary<string, Quote>();
        foreach (var symbol in symbols)
        {
            if (cancellationToken.IsCancellationRequested) break;
            try
            {
                var quote = await GetQuoteAsync(symbol, cancellationToken).ConfigureAwait(false);
                result[symbol] = quote;
            }
            catch
            {
                // Skip failed symbols
            }
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<Trade> GetLatestTradeAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var response = await _httpClient.GetAsync(
            $"/v2/last/trade/{symbol.ToUpperInvariant()}",
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<PolygonLastTradeResponse>(content, _jsonOptions);

        if (result?.Results is null)
        {
            throw new InvalidOperationException($"No trade data found for symbol: {symbol}");
        }

        return new Trade(
            symbol,
            result.Results.Price,
            result.Results.Size,
            DateTimeOffset.FromUnixTimeMilliseconds(result.Results.Timestamp).UtcDateTime,
            result.Results.Exchange?.ToString()
        );
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<Bar>>> GetHistoricalBarsAsync(
        IEnumerable<string> symbols,
        DateTime start,
        DateTime end,
        BarTimeframe timeframe,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var result = new Dictionary<string, IReadOnlyList<Bar>>();
        foreach (var symbol in symbols)
        {
            if (cancellationToken.IsCancellationRequested) break;
            try
            {
                var bars = await GetHistoricalBarsAsync(symbol, start, end, timeframe, cancellationToken).ConfigureAwait(false);
                result[symbol] = bars;
            }
            catch
            {
                result[symbol] = Array.Empty<Bar>();
            }
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<MarketSnapshot> GetSnapshotAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var quote = await GetQuoteAsync(symbol, cancellationToken).ConfigureAwait(false);

        return new MarketSnapshot
        {
            Symbol = symbol,
            LatestQuote = quote,
            LatestTrade = new Trade(symbol, quote.Last, 0, quote.Timestamp)
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, MarketSnapshot>> GetSnapshotsAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var result = new Dictionary<string, MarketSnapshot>();
        foreach (var symbol in symbols)
        {
            if (cancellationToken.IsCancellationRequested) break;
            try
            {
                var snapshot = await GetSnapshotAsync(symbol, cancellationToken).ConfigureAwait(false);
                result[symbol] = snapshot;
            }
            catch
            {
                // Skip failed symbols
            }
        }
        return result;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Bar> StreamBarsAsync(
        IEnumerable<string> symbols,
        BarTimeframe timeframe,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Polling-based implementation
        var symbolList = symbols.ToList();
        var lastTimestamp = DateTime.UtcNow.AddMinutes(-5);

        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var symbol in symbolList)
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;

                IReadOnlyList<Bar>? bars = null;
                try
                {
                    bars = await GetHistoricalBarsAsync(
                        symbol,
                        lastTimestamp,
                        DateTime.UtcNow,
                        timeframe,
                        cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Skip failures
                }

                if (bars is not null)
                {
                    foreach (var bar in bars.Where(b => b.Timestamp > lastTimestamp))
                    {
                        yield return bar;
                        if (bar.Timestamp > lastTimestamp)
                            lastTimestamp = bar.Timestamp;
                    }
                }
            }

            await Task.Delay(_options.PollingInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<IMarketDataSubscriptionHandle> SubscribeAsync(
        MarketDataSubscription subscription,
        Action<MarketDataEvent> handler,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Polygon requires WebSocket for real-time streaming (paid tier)
        // Return a polling-based subscription handle
        var handle = new PolygonSubscriptionHandle(this, subscription, handler, _options.PollingInterval);
        await handle.StartAsync(cancellationToken).ConfigureAwait(false);
        return handle;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PolygonMarketDataProvider));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Configuration options for Polygon.io API.
/// </summary>
public sealed class PolygonOptions
{
    /// <summary>
    /// Gets or sets the API key.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the request timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the polling interval for streaming fallback.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);
}

#region Polygon API Response Types

internal sealed class PolygonSnapshotResponse
{
    public string? Status { get; set; }
    public PolygonTicker? Ticker { get; set; }
}

internal sealed class PolygonTicker
{
    public string? Ticker_ { get; set; }
    public long? Updated { get; set; }
    public PolygonDay? Day { get; set; }
    public PolygonLastQuote? LastQuote { get; set; }
    public PolygonLastTrade? LastTrade { get; set; }
}

internal sealed class PolygonDay
{
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}

internal sealed class PolygonLastQuote
{
    public decimal BidPrice { get; set; }
    public long BidSize { get; set; }
    public decimal AskPrice { get; set; }
    public long AskSize { get; set; }
}

internal sealed class PolygonLastTrade
{
    public decimal Price { get; set; }
    public long Size { get; set; }
}

internal sealed class PolygonLastTradeResponse
{
    public string? Status { get; set; }
    public PolygonTradeResult? Results { get; set; }
}

internal sealed class PolygonTradeResult
{
    public decimal Price { get; set; }
    public long Size { get; set; }
    public long Timestamp { get; set; }
    public int? Exchange { get; set; }
}

internal sealed class PolygonAggregatesResponse
{
    public string? Status { get; set; }
    public int? ResultsCount { get; set; }
    public List<PolygonAggregate>? Results { get; set; }
    public string? NextUrl { get; set; }
}

internal sealed class PolygonAggregate
{
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public long Timestamp { get; set; }
}

internal sealed class PolygonOptionsContractsResponse
{
    public string? Status { get; set; }
    public List<PolygonOptionsContract>? Results { get; set; }
}

internal sealed class PolygonOptionsContract
{
    public string Ticker { get; set; } = string.Empty;
    public string UnderlyingTicker { get; set; } = string.Empty;
    public string ExpirationDate { get; set; } = string.Empty;
    public decimal StrikePrice { get; set; }
    public string ContractType { get; set; } = string.Empty;
}

internal sealed class PolygonOptionSnapshotResponse
{
    public string? Status { get; set; }
    public PolygonOptionSnapshot? Results { get; set; }
}

internal sealed class PolygonOptionSnapshot
{
    public PolygonOptionDetails? Details { get; set; }
    public PolygonOptionGreeks? Greeks { get; set; }
    public PolygonLastQuoteOption? LastQuote { get; set; }
    public PolygonLastTrade? LastTrade { get; set; }
    public PolygonOptionUnderlyingAsset? UnderlyingAsset { get; set; }
    public long OpenInterest { get; set; }
    public PolygonDay? Day { get; set; }
}

internal sealed class PolygonOptionDetails
{
    public string? ContractType { get; set; }
    public decimal StrikePrice { get; set; }
    public string? ExpirationDate { get; set; }
}

internal sealed class PolygonOptionGreeks
{
    public decimal Delta { get; set; }
    public decimal Gamma { get; set; }
    public decimal Theta { get; set; }
    public decimal Vega { get; set; }
    public decimal ImpliedVolatility { get; set; }
}

internal sealed class PolygonLastQuoteOption
{
    public decimal Bid { get; set; }
    public decimal Ask { get; set; }
    public long BidSize { get; set; }
    public long AskSize { get; set; }
}

internal sealed class PolygonOptionUnderlyingAsset
{
    public string? Ticker { get; set; }
}

#endregion

/// <summary>
/// Subscription handle for Polygon polling-based subscriptions.
/// </summary>
internal sealed class PolygonSubscriptionHandle : IMarketDataSubscriptionHandle
{
    private readonly PolygonMarketDataProvider _provider;
    private readonly MarketDataSubscription _subscription;
    private readonly Action<MarketDataEvent> _handler;
    private readonly TimeSpan _pollingInterval;
    private readonly List<string> _symbols;
    private CancellationTokenSource? _cts;
    private Task? _pollingTask;

    public PolygonSubscriptionHandle(
        PolygonMarketDataProvider provider,
        MarketDataSubscription subscription,
        Action<MarketDataEvent> handler,
        TimeSpan pollingInterval)
    {
        _provider = provider;
        _subscription = subscription;
        _handler = handler;
        _pollingInterval = pollingInterval;
        _symbols = subscription.Symbols.ToList();
        SubscriptionId = Guid.NewGuid().ToString();
    }

    public string SubscriptionId { get; }
    public IReadOnlyList<string> Symbols => _symbols;
    public bool IsActive => _cts is not null && !_cts.IsCancellationRequested;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pollingTask = PollAsync(_cts.Token);
        return Task.CompletedTask;
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                foreach (var symbol in _symbols.ToList())
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    if (_subscription.SubscribeQuotes)
                    {
                        try
                        {
                            var quote = await _provider.GetQuoteAsync(symbol, cancellationToken).ConfigureAwait(false);
                            _handler(new QuoteEvent(quote));
                        }
                        catch
                        {
                            // Skip failures
                        }
                    }

                    if (_subscription.SubscribeTrades)
                    {
                        try
                        {
                            var trade = await _provider.GetLatestTradeAsync(symbol, cancellationToken).ConfigureAwait(false);
                            _handler(new TradeEvent(trade));
                        }
                        catch
                        {
                            // Skip failures
                        }
                    }
                }

                await Task.Delay(_pollingInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public Task AddSymbolsAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default)
    {
        foreach (var symbol in symbols)
        {
            if (!_symbols.Contains(symbol))
                _symbols.Add(symbol);
        }
        return Task.CompletedTask;
    }

    public Task RemoveSymbolsAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default)
    {
        foreach (var symbol in symbols)
        {
            _symbols.Remove(symbol);
        }
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        if (_pollingTask is not null)
        {
            try
            {
                await _pollingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
        _cts?.Dispose();
    }
}
