namespace OoplesFinance.StockIndicators.Builder.MarketData;

/// <summary>
/// Interface for market data providers.
/// Provides real-time and historical market data for trading and analysis.
/// </summary>
public interface IMarketDataProvider : IDisposable
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
    /// Gets the latest quote for a symbol.
    /// </summary>
    /// <param name="symbol">The symbol to get a quote for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest quote.</returns>
    Task<Quote> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest quotes for multiple symbols.
    /// </summary>
    /// <param name="symbols">The symbols to get quotes for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of symbol to quote.</returns>
    Task<IReadOnlyDictionary<string, Quote>> GetQuotesAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest trade for a symbol.
    /// </summary>
    /// <param name="symbol">The symbol to get a trade for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest trade.</returns>
    Task<Trade> GetLatestTradeAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical bar data for a symbol.
    /// </summary>
    /// <param name="symbol">The symbol to get bars for.</param>
    /// <param name="start">Start date/time.</param>
    /// <param name="end">End date/time.</param>
    /// <param name="timeframe">Bar timeframe.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of historical bars.</returns>
    Task<IReadOnlyList<Bar>> GetHistoricalBarsAsync(
        string symbol,
        DateTime start,
        DateTime end,
        BarTimeframe timeframe,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical bar data for multiple symbols.
    /// </summary>
    /// <param name="symbols">The symbols to get bars for.</param>
    /// <param name="start">Start date/time.</param>
    /// <param name="end">End date/time.</param>
    /// <param name="timeframe">Bar timeframe.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of symbol to list of bars.</returns>
    Task<IReadOnlyDictionary<string, IReadOnlyList<Bar>>> GetHistoricalBarsAsync(
        IEnumerable<string> symbols,
        DateTime start,
        DateTime end,
        BarTimeframe timeframe,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a market snapshot for a symbol.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The market snapshot.</returns>
    Task<MarketSnapshot> GetSnapshotAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets market snapshots for multiple symbols.
    /// </summary>
    /// <param name="symbols">The symbols.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of symbol to snapshot.</returns>
    Task<IReadOnlyDictionary<string, MarketSnapshot>> GetSnapshotsAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the Level 2 order book for a symbol.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The order book.</returns>
    Task<Level2OrderBook> GetOrderBookAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams real-time quotes for the specified symbols.
    /// </summary>
    /// <param name="symbols">The symbols to stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of quotes.</returns>
    IAsyncEnumerable<Quote> StreamQuotesAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams real-time trades for the specified symbols.
    /// </summary>
    /// <param name="symbols">The symbols to stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of trades.</returns>
    IAsyncEnumerable<Trade> StreamTradesAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams real-time bars for the specified symbols.
    /// </summary>
    /// <param name="symbols">The symbols to stream.</param>
    /// <param name="timeframe">The bar timeframe.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of bars.</returns>
    IAsyncEnumerable<Bar> StreamBarsAsync(
        IEnumerable<string> symbols,
        BarTimeframe timeframe,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to market data events with a callback handler.
    /// </summary>
    /// <param name="subscription">The subscription configuration.</param>
    /// <param name="handler">The event handler.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A subscription handle that can be disposed to unsubscribe.</returns>
    Task<IMarketDataSubscriptionHandle> SubscribeAsync(
        MarketDataSubscription subscription,
        Action<MarketDataEvent> handler,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Handle for managing a market data subscription.
/// </summary>
public interface IMarketDataSubscriptionHandle : IAsyncDisposable
{
    /// <summary>
    /// Gets the subscription ID.
    /// </summary>
    string SubscriptionId { get; }

    /// <summary>
    /// Gets the subscribed symbols.
    /// </summary>
    IReadOnlyList<string> Symbols { get; }

    /// <summary>
    /// Gets whether the subscription is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Adds symbols to the subscription.
    /// </summary>
    Task AddSymbolsAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes symbols from the subscription.
    /// </summary>
    Task RemoveSymbolsAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default);
}

/// <summary>
/// Extended market data provider interface with options data support.
/// </summary>
public interface IOptionsMarketDataProvider : IMarketDataProvider
{
    /// <summary>
    /// Gets the options chain for an underlying symbol.
    /// </summary>
    /// <param name="underlyingSymbol">The underlying stock/ETF symbol.</param>
    /// <param name="expirationDate">Optional specific expiration date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The options chain.</returns>
    Task<OptionsChain> GetOptionsChainAsync(
        string underlyingSymbol,
        DateTime? expirationDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available expiration dates for options on a symbol.
    /// </summary>
    /// <param name="underlyingSymbol">The underlying symbol.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available expiration dates.</returns>
    Task<IReadOnlyList<DateTime>> GetOptionsExpirationsAsync(
        string underlyingSymbol,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Placeholder for options chain - will be implemented in Options module.
/// </summary>
public sealed class OptionsChain
{
    /// <summary>Gets or sets the underlying symbol.</summary>
    public string UnderlyingSymbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the underlying price.</summary>
    public decimal UnderlyingPrice { get; set; }

    /// <summary>Gets or sets the expiration date.</summary>
    public DateTime ExpirationDate { get; set; }

    /// <summary>Gets or sets the timestamp.</summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Extended market data provider interface with crypto-specific features.
/// </summary>
public interface ICryptoMarketDataProvider : IMarketDataProvider
{
    /// <summary>
    /// Gets the order book with full depth for crypto trading.
    /// </summary>
    /// <param name="symbol">The crypto pair symbol.</param>
    /// <param name="depth">Number of levels to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The order book.</returns>
    Task<Level2OrderBook> GetCryptoOrderBookAsync(
        string symbol,
        int depth = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the 24-hour trading statistics for a crypto pair.
    /// </summary>
    /// <param name="symbol">The crypto pair symbol.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The 24-hour stats.</returns>
    Task<Crypto24HourStats> Get24HourStatsAsync(
        string symbol,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 24-hour trading statistics for crypto.
/// </summary>
public sealed class Crypto24HourStats
{
    /// <summary>Gets or sets the symbol.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the 24h high price.</summary>
    public decimal High24h { get; set; }

    /// <summary>Gets or sets the 24h low price.</summary>
    public decimal Low24h { get; set; }

    /// <summary>Gets or sets the 24h volume in base currency.</summary>
    public decimal Volume24h { get; set; }

    /// <summary>Gets or sets the 24h volume in quote currency.</summary>
    public decimal QuoteVolume24h { get; set; }

    /// <summary>Gets or sets the price change in 24h.</summary>
    public decimal PriceChange24h { get; set; }

    /// <summary>Gets or sets the price change percentage in 24h.</summary>
    public decimal PriceChangePercent24h { get; set; }

    /// <summary>Gets or sets the last price.</summary>
    public decimal LastPrice { get; set; }

    /// <summary>Gets or sets the bid price.</summary>
    public decimal BidPrice { get; set; }

    /// <summary>Gets or sets the ask price.</summary>
    public decimal AskPrice { get; set; }

    /// <summary>Gets or sets the open price.</summary>
    public decimal OpenPrice { get; set; }

    /// <summary>Gets or sets the timestamp.</summary>
    public DateTime Timestamp { get; set; }
}
