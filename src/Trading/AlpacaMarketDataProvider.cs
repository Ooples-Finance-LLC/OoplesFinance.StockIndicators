using System.Runtime.CompilerServices;
#if NET6_0_OR_GREATER
using System.Threading.Channels;
#endif
using Alpaca.Markets;
using OoplesFinance.StockIndicators.Builder.Trading;

namespace OoplesFinance.StockIndicators.Builder.MarketData;

/// <summary>
/// Market data provider implementation using Alpaca Markets API.
/// Provides real-time and historical market data for stocks and crypto.
/// </summary>
public sealed class AlpacaMarketDataProvider : IMarketDataProvider, ICryptoMarketDataProvider
{
    private readonly IAlpacaDataClient _dataClient;
    private readonly IAlpacaCryptoDataClient _cryptoDataClient;
#if NET6_0_OR_GREATER
    private readonly IAlpacaDataStreamingClient? _streamingClient;
    private readonly IAlpacaCryptoStreamingClient? _cryptoStreamingClient;
    private readonly List<AlpacaSubscriptionHandle> _activeSubscriptions = new();
    private readonly object _subscriptionLock = new();
#endif
    private readonly AlpacaOptions _options;
    private readonly MarketDataCache _cache;
    private bool _isConnected;
    private bool _disposed;

    /// <summary>
    /// Creates a new Alpaca market data provider.
    /// </summary>
    /// <param name="options">Alpaca API options.</param>
    /// <param name="enableStreaming">Whether to enable real-time streaming (requires .NET 6+).</param>
    /// <param name="cache">Optional cache instance.</param>
    public AlpacaMarketDataProvider(
        AlpacaOptions? options = null,
        bool enableStreaming = true,
        MarketDataCache? cache = null)
    {
        _options = options ?? new AlpacaOptions();
        _cache = cache ?? new MarketDataCache();

        var apiKey = _options.ApiKey ?? Environment.GetEnvironmentVariable("ALPACA_KEY");
        var apiSecret = _options.ApiSecret ?? Environment.GetEnvironmentVariable("ALPACA_SECRET");

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException(
                "Alpaca API key is required. Set AlpacaOptions.ApiKey or ALPACA_KEY environment variable.");
        }

        if (string.IsNullOrEmpty(apiSecret))
        {
            throw new InvalidOperationException(
                "Alpaca API secret is required. Set AlpacaOptions.ApiSecret or ALPACA_SECRET environment variable.");
        }

        var secretKey = new SecretKey(apiKey, apiSecret);

        // Create data clients
        _dataClient = Environments.Paper.GetAlpacaDataClient(secretKey);
        _cryptoDataClient = Environments.Paper.GetAlpacaCryptoDataClient(secretKey);

#if NET6_0_OR_GREATER
        if (enableStreaming)
        {
            _streamingClient = Environments.Paper.GetAlpacaDataStreamingClient(secretKey);
            _cryptoStreamingClient = Environments.Paper.GetAlpacaCryptoStreamingClient(secretKey);
        }
#endif

        _isConnected = true;
    }

    /// <inheritdoc />
    public string ProviderName => "Alpaca";

    /// <inheritdoc />
    public bool IsConnected => _isConnected && !_disposed;

    /// <inheritdoc />
    public async Task<Quote> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Check cache first
        var cached = _cache.GetQuote(symbol);
        if (cached is not null)
        {
            return cached;
        }

        var alpacaQuote = await _dataClient.GetLatestQuoteAsync(
            new LatestMarketDataRequest(symbol),
            cancellationToken).ConfigureAwait(false);

        var quote = MapQuote(symbol, alpacaQuote);
        _cache.SetQuote(quote);
        return quote;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, Quote>> GetQuotesAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var symbolList = symbols.ToList();
        var result = new Dictionary<string, Quote>();
        var uncached = new List<string>();

        // Check cache first
        foreach (var symbol in symbolList)
        {
            var cached = _cache.GetQuote(symbol);
            if (cached is not null)
            {
                result[symbol] = cached;
            }
            else
            {
                uncached.Add(symbol);
            }
        }

        if (uncached.Count > 0)
        {
            var request = new LatestMarketDataListRequest(uncached);
            var alpacaQuotes = await _dataClient.ListLatestQuotesAsync(request, cancellationToken)
                .ConfigureAwait(false);

            foreach (var kvp in alpacaQuotes)
            {
                var quote = MapQuote(kvp.Key, kvp.Value);
                result[kvp.Key] = quote;
                _cache.SetQuote(quote);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<Trade> GetLatestTradeAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Check cache first
        var cached = _cache.GetTrade(symbol);
        if (cached is not null)
        {
            return cached;
        }

        var alpacaTrade = await _dataClient.GetLatestTradeAsync(
            new LatestMarketDataRequest(symbol),
            cancellationToken).ConfigureAwait(false);

        var trade = MapTrade(symbol, alpacaTrade);
        _cache.SetTrade(trade);
        return trade;
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

        var alpacaTimeframe = MapTimeframe(timeframe);
        var request = new HistoricalBarsRequest(symbol, start, end, alpacaTimeframe);

        var bars = new List<Bar>();

#if NET6_0_OR_GREATER
        // Use async enumerable for efficient memory usage in .NET 6+
        var page = await _dataClient.ListHistoricalBarsAsync(request, cancellationToken).ConfigureAwait(false);
        foreach (var alpacaBar in page.Items)
        {
            bars.Add(MapBar(symbol, alpacaBar));
        }

        // Handle pagination
        while (page.NextPageToken is not null)
        {
            request = request.WithPageToken(page.NextPageToken);
            page = await _dataClient.ListHistoricalBarsAsync(request, cancellationToken).ConfigureAwait(false);
            foreach (var alpacaBar in page.Items)
            {
                bars.Add(MapBar(symbol, alpacaBar));
            }
        }
#else
        // Simple paged fetch for .NET Framework
        var page = await _dataClient.ListHistoricalBarsAsync(request, cancellationToken).ConfigureAwait(false);
        foreach (var alpacaBar in page.Items)
        {
            bars.Add(MapBar(symbol, alpacaBar));
        }

        // Handle pagination
        while (page.NextPageToken is not null)
        {
            request = request.WithPageToken(page.NextPageToken);
            page = await _dataClient.ListHistoricalBarsAsync(request, cancellationToken).ConfigureAwait(false);
            foreach (var alpacaBar in page.Items)
            {
                bars.Add(MapBar(symbol, alpacaBar));
            }
        }
#endif

        return bars;
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

        var symbolList = symbols.ToList();
        var result = new Dictionary<string, IReadOnlyList<Bar>>();

        var alpacaTimeframe = MapTimeframe(timeframe);
        var request = new HistoricalBarsRequest(symbolList, start, end, alpacaTimeframe);

        var barsBySymbol = new Dictionary<string, List<Bar>>();
        foreach (var symbol in symbolList)
        {
            barsBySymbol[symbol] = new List<Bar>();
        }

        var page = await _dataClient.ListHistoricalBarsAsync(request, cancellationToken).ConfigureAwait(false);
        foreach (var alpacaBar in page.Items)
        {
            if (barsBySymbol.TryGetValue(alpacaBar.Symbol, out var list))
            {
                list.Add(MapBar(alpacaBar.Symbol, alpacaBar));
            }
        }

        // Handle pagination
        while (page.NextPageToken is not null)
        {
            request = request.WithPageToken(page.NextPageToken);
            page = await _dataClient.ListHistoricalBarsAsync(request, cancellationToken).ConfigureAwait(false);
            foreach (var alpacaBar in page.Items)
            {
                if (barsBySymbol.TryGetValue(alpacaBar.Symbol, out var list))
                {
                    list.Add(MapBar(alpacaBar.Symbol, alpacaBar));
                }
            }
        }

        foreach (var kvp in barsBySymbol)
        {
            result[kvp.Key] = kvp.Value;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<MarketSnapshot> GetSnapshotAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var alpacaSnapshot = await _dataClient.GetSnapshotAsync(
            new LatestMarketDataRequest(symbol),
            cancellationToken).ConfigureAwait(false);

        return MapSnapshot(symbol, alpacaSnapshot);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, MarketSnapshot>> GetSnapshotsAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var symbolList = symbols.ToList();
        var request = new LatestMarketDataListRequest(symbolList);

        var alpacaSnapshots = await _dataClient.ListSnapshotsAsync(request, cancellationToken)
            .ConfigureAwait(false);

        var result = new Dictionary<string, MarketSnapshot>();
        foreach (var kvp in alpacaSnapshots)
        {
            result[kvp.Key] = MapSnapshot(kvp.Key, kvp.Value);
        }

        return result;
    }

    /// <inheritdoc />
    public Task<Level2OrderBook> GetOrderBookAsync(string symbol, CancellationToken cancellationToken = default)
    {
        // Note: Alpaca SIP data doesn't include full order book.
        // This would require a different data subscription.
        // Returning empty order book as placeholder.
        var emptyBook = new Level2OrderBook(
            symbol,
            Array.Empty<PriceLevel>(),
            Array.Empty<PriceLevel>(),
            DateTime.UtcNow);

        return Task.FromResult(emptyBook);
    }

#if NET6_0_OR_GREATER
    /// <inheritdoc />
    public async IAsyncEnumerable<Quote> StreamQuotesAsync(
        IEnumerable<string> symbols,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_streamingClient is null)
        {
            throw new InvalidOperationException("Streaming is not enabled. Create the provider with enableStreaming: true.");
        }

        var symbolList = symbols.ToList();
        var channel = Channel.CreateUnbounded<Quote>();

        await _streamingClient.ConnectAndAuthenticateAsync(cancellationToken).ConfigureAwait(false);

        var subscription = _streamingClient.GetQuoteSubscription(symbolList[0]);
        for (int i = 1; i < symbolList.Count; i++)
        {
            subscription = _streamingClient.GetQuoteSubscription(symbolList[i]);
        }

        subscription.Received += (quote) =>
        {
            var mapped = MapQuote(quote.Symbol, quote);
            _cache.SetQuote(mapped);
            channel.Writer.TryWrite(mapped);
        };

        await _streamingClient.SubscribeAsync(subscription, cancellationToken).ConfigureAwait(false);

        try
        {
            await foreach (var quote in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return quote;
            }
        }
        finally
        {
            await _streamingClient.UnsubscribeAsync(subscription, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Trade> StreamTradesAsync(
        IEnumerable<string> symbols,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_streamingClient is null)
        {
            throw new InvalidOperationException("Streaming is not enabled. Create the provider with enableStreaming: true.");
        }

        var symbolList = symbols.ToList();
        var channel = Channel.CreateUnbounded<Trade>();

        await _streamingClient.ConnectAndAuthenticateAsync(cancellationToken).ConfigureAwait(false);

        var subscription = _streamingClient.GetTradeSubscription(symbolList[0]);
        for (int i = 1; i < symbolList.Count; i++)
        {
            subscription = _streamingClient.GetTradeSubscription(symbolList[i]);
        }

        subscription.Received += (trade) =>
        {
            var mapped = MapTrade(trade.Symbol, trade);
            _cache.SetTrade(mapped);
            channel.Writer.TryWrite(mapped);
        };

        await _streamingClient.SubscribeAsync(subscription, cancellationToken).ConfigureAwait(false);

        try
        {
            await foreach (var trade in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return trade;
            }
        }
        finally
        {
            await _streamingClient.UnsubscribeAsync(subscription, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Bar> StreamBarsAsync(
        IEnumerable<string> symbols,
        BarTimeframe timeframe,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_streamingClient is null)
        {
            throw new InvalidOperationException("Streaming is not enabled. Create the provider with enableStreaming: true.");
        }

        // Note: Alpaca streaming only supports minute bars
        if (timeframe != BarTimeframe.Minute1)
        {
            throw new ArgumentException("Alpaca streaming only supports minute bars.", nameof(timeframe));
        }

        var symbolList = symbols.ToList();
        var channel = Channel.CreateUnbounded<Bar>();

        await _streamingClient.ConnectAndAuthenticateAsync(cancellationToken).ConfigureAwait(false);

        var subscription = _streamingClient.GetMinuteBarSubscription(symbolList[0]);
        for (int i = 1; i < symbolList.Count; i++)
        {
            subscription = _streamingClient.GetMinuteBarSubscription(symbolList[i]);
        }

        subscription.Received += (bar) =>
        {
            var mapped = MapBar(bar.Symbol, bar);
            channel.Writer.TryWrite(mapped);
        };

        await _streamingClient.SubscribeAsync(subscription, cancellationToken).ConfigureAwait(false);

        try
        {
            await foreach (var bar in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return bar;
            }
        }
        finally
        {
            await _streamingClient.UnsubscribeAsync(subscription, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<IMarketDataSubscriptionHandle> SubscribeAsync(
        MarketDataSubscription subscription,
        Action<MarketDataEvent> handler,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_streamingClient is null)
        {
            throw new InvalidOperationException("Streaming is not enabled. Create the provider with enableStreaming: true.");
        }

        var symbolList = subscription.Symbols.ToList();
        var subscriptionId = Guid.NewGuid().ToString();

        await _streamingClient.ConnectAndAuthenticateAsync(cancellationToken).ConfigureAwait(false);

        IAlpacaDataSubscription<IQuote>? quoteSubscription = null;
        IAlpacaDataSubscription<ITrade>? tradeSubscription = null;
        IAlpacaDataSubscription<IBar>? barSubscription = null;

        if (subscription.SubscribeQuotes && symbolList.Count > 0)
        {
            quoteSubscription = _streamingClient.GetQuoteSubscription(symbolList[0]);
            quoteSubscription.Received += (quote) =>
            {
                var mapped = MapQuote(quote.Symbol, quote);
                _cache.SetQuote(mapped);
                handler(new QuoteEvent(mapped));
            };
            await _streamingClient.SubscribeAsync(quoteSubscription, cancellationToken).ConfigureAwait(false);
        }

        if (subscription.SubscribeTrades && symbolList.Count > 0)
        {
            tradeSubscription = _streamingClient.GetTradeSubscription(symbolList[0]);
            tradeSubscription.Received += (trade) =>
            {
                var mapped = MapTrade(trade.Symbol, trade);
                _cache.SetTrade(mapped);
                handler(new TradeEvent(mapped));
            };
            await _streamingClient.SubscribeAsync(tradeSubscription, cancellationToken).ConfigureAwait(false);
        }

        if (subscription.SubscribeBars && symbolList.Count > 0)
        {
            barSubscription = _streamingClient.GetMinuteBarSubscription(symbolList[0]);
            barSubscription.Received += (bar) =>
            {
                var mapped = MapBar(bar.Symbol, bar);
                handler(new BarEvent(mapped));
            };
            await _streamingClient.SubscribeAsync(barSubscription, cancellationToken).ConfigureAwait(false);
        }

        var handle = new AlpacaSubscriptionHandle(
            subscriptionId,
            symbolList,
            _streamingClient,
            quoteSubscription,
            tradeSubscription,
            barSubscription);

        lock (_subscriptionLock)
        {
            _activeSubscriptions.Add(handle);
        }

        return handle;
    }
#else
    /// <inheritdoc />
    /// <remarks>
    /// Streaming is not supported in .NET Framework. This method throws NotSupportedException.
    /// Use polling with GetQuoteAsync instead.
    /// </remarks>
    public IAsyncEnumerable<Quote> StreamQuotesAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Streaming is not supported in .NET Framework. Use GetQuoteAsync for polling instead, or upgrade to .NET 6+.");
    }

    /// <inheritdoc />
    /// <remarks>
    /// Streaming is not supported in .NET Framework. This method throws NotSupportedException.
    /// Use polling with GetLatestTradeAsync instead.
    /// </remarks>
    public IAsyncEnumerable<Trade> StreamTradesAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Streaming is not supported in .NET Framework. Use GetLatestTradeAsync for polling instead, or upgrade to .NET 6+.");
    }

    /// <inheritdoc />
    /// <remarks>
    /// Streaming is not supported in .NET Framework. This method throws NotSupportedException.
    /// Use polling with GetHistoricalBarsAsync instead.
    /// </remarks>
    public IAsyncEnumerable<Bar> StreamBarsAsync(
        IEnumerable<string> symbols,
        BarTimeframe timeframe,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Streaming is not supported in .NET Framework. Use GetHistoricalBarsAsync for polling instead, or upgrade to .NET 6+.");
    }

    /// <inheritdoc />
    /// <remarks>
    /// Subscriptions are not supported in .NET Framework. This method throws NotSupportedException.
    /// </remarks>
    public Task<IMarketDataSubscriptionHandle> SubscribeAsync(
        MarketDataSubscription subscription,
        Action<MarketDataEvent> handler,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Subscriptions are not supported in .NET Framework. Use polling methods instead, or upgrade to .NET 6+.");
    }
#endif

    /// <inheritdoc />
    public async Task<Level2OrderBook> GetCryptoOrderBookAsync(
        string symbol,
        int depth = 20,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Get latest quote as approximation for order book (Alpaca doesn't have full orderbook API)
        // Use the list-based API for crypto quotes
#pragma warning disable CS0618 // Suppress obsolete warning - API migration in progress
        var request = new LatestDataListRequest(new[] { symbol });
#pragma warning restore CS0618
        var quotes = await _cryptoDataClient.ListLatestQuotesAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!quotes.TryGetValue(symbol, out var quote))
        {
            return new Level2OrderBook(symbol, Array.Empty<PriceLevel>(), Array.Empty<PriceLevel>(), DateTime.UtcNow);
        }

        var bids = new List<PriceLevel>
        {
            new PriceLevel(quote.BidPrice, (long)quote.BidSize)
        };

        var asks = new List<PriceLevel>
        {
            new PriceLevel(quote.AskPrice, (long)quote.AskSize)
        };

        return new Level2OrderBook(symbol, bids, asks, GetTimestamp(quote.TimestampUtc));
    }

    /// <inheritdoc />
    public async Task<Crypto24HourStats> Get24HourStatsAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Get snapshot for 24h data using ListSnapshotsAsync
#pragma warning disable CS0618 // Suppress obsolete warning - API migration in progress
        var request = new SnapshotDataListRequest(new[] { symbol });
#pragma warning restore CS0618
        var snapshots = await _cryptoDataClient.ListSnapshotsAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!snapshots.TryGetValue(symbol, out var snapshot))
        {
            return new Crypto24HourStats
            {
                Symbol = symbol,
                Timestamp = DateTime.UtcNow
            };
        }

        return new Crypto24HourStats
        {
            Symbol = symbol,
            LastPrice = snapshot.Trade?.Price ?? 0m,
            BidPrice = snapshot.Quote?.BidPrice ?? 0m,
            AskPrice = snapshot.Quote?.AskPrice ?? 0m,
            High24h = snapshot.CurrentDailyBar?.High ?? 0m,
            Low24h = snapshot.CurrentDailyBar?.Low ?? 0m,
            Volume24h = snapshot.CurrentDailyBar?.Volume ?? 0m,
            OpenPrice = snapshot.CurrentDailyBar?.Open ?? 0m,
            Timestamp = DateTime.UtcNow
        };
    }

    #region Mapping Methods

    private static DateTime GetTimestamp(DateTime? timestamp)
    {
        return timestamp ?? DateTime.UtcNow;
    }

    private static Quote MapQuote(string symbol, IQuote alpacaQuote)
    {
        return new Quote(
            symbol,
            alpacaQuote.BidPrice,
            alpacaQuote.AskPrice,
            0m, // Last price not in quote
            (long)alpacaQuote.BidSize,
            (long)alpacaQuote.AskSize,
            GetTimestamp(alpacaQuote.TimestampUtc));
    }

    private static Trade MapTrade(string symbol, ITrade alpacaTrade)
    {
        return new Trade(
            symbol,
            alpacaTrade.Price,
            (long)alpacaTrade.Size,
            GetTimestamp(alpacaTrade.TimestampUtc),
            alpacaTrade.Exchange,
            alpacaTrade.TradeId.ToString());
    }

    private static Bar MapBar(string symbol, IBar alpacaBar)
    {
        return new Bar(
            symbol,
            GetTimestamp(alpacaBar.TimeUtc),
            alpacaBar.Open,
            alpacaBar.High,
            alpacaBar.Low,
            alpacaBar.Close,
            (long)alpacaBar.Volume,
            alpacaBar.Vwap,
            (int?)alpacaBar.TradeCount);
    }

    private static MarketSnapshot MapSnapshot(string symbol, ISnapshot alpacaSnapshot)
    {
        return new MarketSnapshot
        {
            Symbol = symbol,
            LatestQuote = alpacaSnapshot.Quote is not null
                ? MapQuote(symbol, alpacaSnapshot.Quote)
                : null,
            LatestTrade = alpacaSnapshot.Trade is not null
                ? MapTrade(symbol, alpacaSnapshot.Trade)
                : null,
            DailyBar = alpacaSnapshot.CurrentDailyBar is not null
                ? MapBar(symbol, alpacaSnapshot.CurrentDailyBar)
                : null,
            PreviousBar = alpacaSnapshot.PreviousDailyBar is not null
                ? MapBar(symbol, alpacaSnapshot.PreviousDailyBar)
                : null,
            MinuteBar = alpacaSnapshot.MinuteBar is not null
                ? MapBar(symbol, alpacaSnapshot.MinuteBar)
                : null
        };
    }

    private static BarTimeFrame MapTimeframe(BarTimeframe timeframe)
    {
        return timeframe switch
        {
            BarTimeframe.Minute1 => BarTimeFrame.Minute,
            BarTimeframe.Minute5 => new BarTimeFrame(5, BarTimeFrameUnit.Minute),
            BarTimeframe.Minute15 => new BarTimeFrame(15, BarTimeFrameUnit.Minute),
            BarTimeframe.Minute30 => new BarTimeFrame(30, BarTimeFrameUnit.Minute),
            BarTimeframe.Hour1 => BarTimeFrame.Hour,
            BarTimeframe.Hour4 => new BarTimeFrame(4, BarTimeFrameUnit.Hour),
            BarTimeframe.Day => BarTimeFrame.Day,
            BarTimeframe.Week => BarTimeFrame.Week,
            BarTimeframe.Month => BarTimeFrame.Month,
            _ => BarTimeFrame.Day
        };
    }

    #endregion

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AlpacaMarketDataProvider));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _isConnected = false;

#if NET6_0_OR_GREATER
            // Dispose all active subscriptions
            lock (_subscriptionLock)
            {
                foreach (var subscription in _activeSubscriptions)
                {
                    subscription.DisposeAsync().AsTask().Wait();
                }
                _activeSubscriptions.Clear();
            }

            _streamingClient?.Dispose();
            _cryptoStreamingClient?.Dispose();
#endif
            _dataClient.Dispose();
            _cryptoDataClient.Dispose();

            _disposed = true;
        }
    }
}

#if NET6_0_OR_GREATER
/// <summary>
/// Subscription handle for Alpaca market data.
/// </summary>
internal sealed class AlpacaSubscriptionHandle : IMarketDataSubscriptionHandle
{
    private readonly IAlpacaDataStreamingClient _client;
    private readonly IAlpacaDataSubscription<IQuote>? _quoteSubscription;
    private readonly IAlpacaDataSubscription<ITrade>? _tradeSubscription;
    private readonly IAlpacaDataSubscription<IBar>? _barSubscription;
    private readonly List<string> _symbols;
    private bool _isActive = true;

    public AlpacaSubscriptionHandle(
        string subscriptionId,
        List<string> symbols,
        IAlpacaDataStreamingClient client,
        IAlpacaDataSubscription<IQuote>? quoteSubscription,
        IAlpacaDataSubscription<ITrade>? tradeSubscription,
        IAlpacaDataSubscription<IBar>? barSubscription)
    {
        SubscriptionId = subscriptionId;
        _symbols = symbols;
        _client = client;
        _quoteSubscription = quoteSubscription;
        _tradeSubscription = tradeSubscription;
        _barSubscription = barSubscription;
    }

    public string SubscriptionId { get; }

    public IReadOnlyList<string> Symbols => _symbols;

    public bool IsActive => _isActive;

    public async Task AddSymbolsAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default)
    {
        var newSymbols = symbols.ToList();

        foreach (var symbol in newSymbols)
        {
            if (_quoteSubscription is not null)
            {
                var newQuoteSub = _client.GetQuoteSubscription(symbol);
                await _client.SubscribeAsync(newQuoteSub, cancellationToken).ConfigureAwait(false);
            }

            if (_tradeSubscription is not null)
            {
                var newTradeSub = _client.GetTradeSubscription(symbol);
                await _client.SubscribeAsync(newTradeSub, cancellationToken).ConfigureAwait(false);
            }

            if (_barSubscription is not null)
            {
                var newBarSub = _client.GetMinuteBarSubscription(symbol);
                await _client.SubscribeAsync(newBarSub, cancellationToken).ConfigureAwait(false);
            }
        }

        _symbols.AddRange(newSymbols);
    }

    public async Task RemoveSymbolsAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default)
    {
        var removeSymbols = symbols.ToHashSet();

        foreach (var symbol in removeSymbols)
        {
            if (_quoteSubscription is not null)
            {
                var removeSub = _client.GetQuoteSubscription(symbol);
                await _client.UnsubscribeAsync(removeSub, cancellationToken).ConfigureAwait(false);
            }

            if (_tradeSubscription is not null)
            {
                var removeSub = _client.GetTradeSubscription(symbol);
                await _client.UnsubscribeAsync(removeSub, cancellationToken).ConfigureAwait(false);
            }

            if (_barSubscription is not null)
            {
                var removeSub = _client.GetMinuteBarSubscription(symbol);
                await _client.UnsubscribeAsync(removeSub, cancellationToken).ConfigureAwait(false);
            }
        }

        _symbols.RemoveAll(s => removeSymbols.Contains(s));
    }

    public async ValueTask DisposeAsync()
    {
        if (_isActive)
        {
            _isActive = false;

            if (_quoteSubscription is not null)
            {
                await _client.UnsubscribeAsync(_quoteSubscription, CancellationToken.None).ConfigureAwait(false);
            }

            if (_tradeSubscription is not null)
            {
                await _client.UnsubscribeAsync(_tradeSubscription, CancellationToken.None).ConfigureAwait(false);
            }

            if (_barSubscription is not null)
            {
                await _client.UnsubscribeAsync(_barSubscription, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
#endif
