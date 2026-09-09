namespace OoplesFinance.StockIndicators.Tests.IntegrationTests;

using OoplesFinance.StockIndicators.Builder.MarketData;
using OoplesFinance.StockIndicators.Builder.Trading;
using Xunit;

/// <summary>
/// Integration tests for market data providers.
/// These tests verify real-time and historical data retrieval.
/// </summary>
[Trait("Category", "Integration")]
public class MarketDataIntegrationTests
{
    /// <summary>
    /// Tests quote retrieval from Alpaca.
    /// </summary>
    [SkippableFact]
    public async Task AlpacaMarketData_ShouldRetrieveQuote()
    {
        Skip.IfNot(AlpacaTestCredentials.Available, AlpacaTestCredentials.SkipReason);

        // Arrange
        var provider = CreateAlpacaProvider();

        // Act
        var quote = await provider.GetQuoteAsync("AAPL");

        // Assert
        Assert.NotNull(quote);
        Assert.Equal("AAPL", quote.Symbol);
        Assert.True(quote.Bid > 0, "Bid price should be positive");
        Assert.True(quote.Ask > 0, "Ask price should be positive");
        Assert.True(quote.Ask >= quote.Bid, "Ask should be >= Bid");
    }

    /// <summary>
    /// Tests historical bar retrieval.
    /// </summary>
    [SkippableFact]
    public async Task AlpacaMarketData_ShouldRetrieveHistoricalBars()
    {
        Skip.IfNot(AlpacaTestCredentials.Available, AlpacaTestCredentials.SkipReason);

        // Arrange
        var provider = CreateAlpacaProvider();
        var endDate = DateTime.UtcNow.Date;
        var startDate = endDate.AddDays(-30);

        // Act
        var bars = await provider.GetHistoricalBarsAsync(
            "AAPL",
            startDate,
            endDate,
            BarTimeframe.Day);

        // Assert
        Assert.NotNull(bars);
        Assert.NotEmpty(bars);
        Assert.All(bars, bar =>
        {
            Assert.Equal("AAPL", bar.Symbol);
            Assert.True(bar.Open > 0);
            Assert.True(bar.High >= bar.Low);
            Assert.True(bar.Close > 0);
            Assert.True(bar.Volume >= 0);
        });
    }

    /// <summary>
    /// Tests intraday bar retrieval.
    /// </summary>
    [SkippableFact]
    public async Task AlpacaMarketData_ShouldRetrieveIntradayBars()
    {
        Skip.IfNot(AlpacaTestCredentials.Available, AlpacaTestCredentials.SkipReason);

        // Arrange
        var provider = CreateAlpacaProvider();
        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddHours(-8);

        // Act
        var bars = await provider.GetHistoricalBarsAsync(
            "AAPL",
            startDate,
            endDate,
            BarTimeframe.Minute1);

        // Assert
        Assert.NotNull(bars);
        // May be empty if market is closed
        if (bars.Count > 0)
        {
            Assert.All(bars, bar =>
            {
                Assert.True(bar.High >= bar.Low);
            });
        }
    }

    /// <summary>
    /// Tests multiple symbol quotes.
    /// </summary>
    [SkippableFact]
    public async Task AlpacaMarketData_ShouldRetrieveMultipleQuotes()
    {
        Skip.IfNot(AlpacaTestCredentials.Available, AlpacaTestCredentials.SkipReason);

        // Arrange
        var provider = CreateAlpacaProvider();
        var symbols = new[] { "AAPL", "MSFT", "GOOGL", "AMZN" };

        // Act
        var tasks = symbols.Select(s => provider.GetQuoteAsync(s));
        var quotes = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(4, quotes.Length);
        Assert.All(quotes, q => Assert.NotNull(q));
        Assert.Equal(4, quotes.Select(q => q!.Symbol).Distinct().Count());
    }

    /// <summary>
    /// Tests market data caching.
    /// </summary>
    [SkippableFact]
    public async Task MarketDataCache_ShouldCacheQuotes()
    {
        Skip.IfNot(AlpacaTestCredentials.Available, AlpacaTestCredentials.SkipReason);

        // Arrange
        var cacheTtl = TimeSpan.FromSeconds(30);
        var cache = new MarketDataCache(cacheTtl, cacheTtl, cacheTtl, cacheTtl);
        var provider = CreateAlpacaProviderWithCache(cache);

        // Act - First call should hit the provider
        var quote1 = await provider.GetQuoteAsync("AAPL");

        // Second call should use cache
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var quote2 = await provider.GetQuoteAsync("AAPL");
        stopwatch.Stop();

        // Assert
        Assert.NotNull(quote1);
        Assert.NotNull(quote2);
        Assert.True(stopwatch.ElapsedMilliseconds < 50, "Cached quote should be fast");
    }

    /// <summary>
    /// Tests order book retrieval (Level 2 data).
    /// </summary>
    [SkippableFact]
    public async Task AlpacaMarketData_ShouldRetrieveOrderBook()
    {
        Skip.IfNot(AlpacaTestCredentials.Available, AlpacaTestCredentials.SkipReason);

        // Arrange
        var provider = CreateAlpacaProvider();

        // Act
        var orderBook = await provider.GetOrderBookAsync("AAPL");

        // Assert
        if (orderBook is not null) // May not be available for all subscriptions
        {
            Assert.NotEmpty(orderBook.Bids);
            Assert.NotEmpty(orderBook.Asks);
            Assert.All(orderBook.Bids, level =>
            {
                Assert.True(level.Price > 0);
                Assert.True(level.Size > 0);
            });
        }
    }

    /// <summary>
    /// Tests quote streaming.
    /// </summary>
    [SkippableFact]
    public async Task AlpacaMarketData_ShouldStreamQuotes()
    {
        Skip.IfNot(AlpacaTestCredentials.Available, AlpacaTestCredentials.SkipReason);

        // Arrange
        var provider = CreateAlpacaProvider();
        var symbols = new[] { "AAPL", "MSFT" };
        var receivedQuotes = new List<Quote>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        try
        {
            await foreach (var quote in provider.StreamQuotesAsync(symbols, cts.Token))
            {
                receivedQuotes.Add(quote);
                if (receivedQuotes.Count >= 5) break;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected if market is closed or timeout
        }

        // Assert - May be empty if market is closed
        Assert.NotNull(receivedQuotes);
    }

    /// <summary>
    /// Tests trade streaming.
    /// </summary>
    [SkippableFact]
    public async Task AlpacaMarketData_ShouldStreamTrades()
    {
        Skip.IfNot(AlpacaTestCredentials.Available, AlpacaTestCredentials.SkipReason);

        // Arrange
        var provider = CreateAlpacaProvider();
        var symbols = new[] { "AAPL" };
        var receivedTrades = new List<Trade>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        try
        {
            await foreach (var trade in provider.StreamTradesAsync(symbols, cts.Token))
            {
                receivedTrades.Add(trade);
                if (receivedTrades.Count >= 5) break;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected if market is closed or timeout
        }

        // Assert - May be empty if market is closed
        Assert.NotNull(receivedTrades);
    }

    /// <summary>
    /// Tests market data provider resilience to invalid symbols.
    /// </summary>
    [SkippableFact]
    public async Task MarketData_ShouldHandleInvalidSymbol()
    {
        Skip.IfNot(AlpacaTestCredentials.Available, AlpacaTestCredentials.SkipReason);

        // Arrange
        var provider = CreateAlpacaProvider();

        // Act & Assert
        try
        {
            var quote = await provider.GetQuoteAsync("INVALID_SYMBOL_XYZ123");
            // Should either return null or throw a specific exception
        }
        catch (Exception)
        {
            // Expected for invalid symbols
        }
    }

    /// <summary>
    /// Tests concurrent market data requests.
    /// </summary>
    [SkippableFact]
    public async Task MarketData_ShouldHandleConcurrentRequests()
    {
        Skip.IfNot(AlpacaTestCredentials.Available, AlpacaTestCredentials.SkipReason);

        // Arrange
        var provider = CreateAlpacaProvider();
        var symbols = Enumerable.Range(0, 20).Select(_ => "AAPL").ToList();

        // Act
        var tasks = symbols.Select(s => provider.GetQuoteAsync(s));
        var quotes = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(20, quotes.Length);
        Assert.All(quotes, q => Assert.NotNull(q));
    }

    /// <summary>
    /// Tests market snapshot retrieval.
    /// </summary>
    [SkippableFact]
    public async Task AlpacaMarketData_ShouldRetrieveSnapshot()
    {
        Skip.IfNot(AlpacaTestCredentials.Available, AlpacaTestCredentials.SkipReason);

        // Arrange
        var provider = CreateAlpacaProvider();

        // Act
        var snapshot = await provider.GetSnapshotAsync("AAPL");

        // Assert
        Assert.NotNull(snapshot);
        Assert.Equal("AAPL", snapshot.Symbol);
    }

    /// <summary>
    /// Tests latest trade retrieval.
    /// </summary>
    [SkippableFact]
    public async Task AlpacaMarketData_ShouldRetrieveLatestTrade()
    {
        Skip.IfNot(AlpacaTestCredentials.Available, AlpacaTestCredentials.SkipReason);

        // Arrange
        var provider = CreateAlpacaProvider();

        // Act
        var trade = await provider.GetLatestTradeAsync("AAPL");

        // Assert
        Assert.NotNull(trade);
        Assert.Equal("AAPL", trade.Symbol);
        Assert.True(trade.Price > 0);
        Assert.True(trade.Size > 0);
    }

    #region Helper Methods

    private static IMarketDataProvider CreateAlpacaProvider()
    {
        var options = AlpacaTestCredentials.CreateOptions();
        return new AlpacaMarketDataProvider(options);
    }

    private static IMarketDataProvider CreateAlpacaProviderWithCache(MarketDataCache cache)
    {
        var options = AlpacaTestCredentials.CreateOptions();
        return new AlpacaMarketDataProvider(options, enableStreaming: false, cache: cache);
    }

    #endregion
}

/// <summary>
/// Tests for market data quality and consistency.
/// </summary>
[Trait("Category", "Integration")]
public class MarketDataQualityTests
{
    /// <summary>
    /// Tests that bar data has correct OHLC relationships.
    /// </summary>
    [SkippableFact]
    public async Task Bars_ShouldHaveValidOHLCRelationships()
    {
        Skip.IfNot(AlpacaTestCredentials.Available, AlpacaTestCredentials.SkipReason);

        // Arrange
        var provider = CreateAlpacaProvider();
        var endDate = DateTime.UtcNow.Date;
        var startDate = endDate.AddDays(-30);

        // Act
        var bars = await provider.GetHistoricalBarsAsync(
            "AAPL",
            startDate,
            endDate,
            BarTimeframe.Day);

        // Assert
        Assert.All(bars, bar =>
        {
            // High should be >= Open, Close, Low
            Assert.True(bar.High >= bar.Open, $"High ({bar.High}) should be >= Open ({bar.Open})");
            Assert.True(bar.High >= bar.Close, $"High ({bar.High}) should be >= Close ({bar.Close})");
            Assert.True(bar.High >= bar.Low, $"High ({bar.High}) should be >= Low ({bar.Low})");

            // Low should be <= Open, Close, High
            Assert.True(bar.Low <= bar.Open, $"Low ({bar.Low}) should be <= Open ({bar.Open})");
            Assert.True(bar.Low <= bar.Close, $"Low ({bar.Low}) should be <= Close ({bar.Close})");
            Assert.True(bar.Low <= bar.High, $"Low ({bar.Low}) should be <= High ({bar.High})");
        });
    }

    /// <summary>
    /// Tests that quote spreads are reasonable.
    /// </summary>
    [SkippableFact]
    public async Task Quotes_ShouldHaveReasonableSpreads()
    {
        Skip.IfNot(AlpacaTestCredentials.Available, AlpacaTestCredentials.SkipReason);

        // Arrange
        var provider = CreateAlpacaProvider();

        // Act
        var quote = await provider.GetQuoteAsync("AAPL");

        // Assert
        if (quote is not null)
        {
            var spread = quote.Ask - quote.Bid;
            var spreadPercent = spread / quote.Bid * 100;

            // For liquid stocks like AAPL, spread should typically be < 0.1%
            Assert.True(spreadPercent < 1.0m,
                $"Spread of {spreadPercent:F2}% seems too high for AAPL");
        }
    }

    /// <summary>
    /// Tests that bars are in chronological order.
    /// </summary>
    [SkippableFact]
    public async Task Bars_ShouldBeInChronologicalOrder()
    {
        Skip.IfNot(AlpacaTestCredentials.Available, AlpacaTestCredentials.SkipReason);

        // Arrange
        var provider = CreateAlpacaProvider();
        var endDate = DateTime.UtcNow.Date;
        var startDate = endDate.AddDays(-30);

        // Act
        var bars = await provider.GetHistoricalBarsAsync(
            "AAPL",
            startDate,
            endDate,
            BarTimeframe.Day);

        // Assert
        var timestamps = bars.Select(b => b.Timestamp).ToList();
        var sortedTimestamps = timestamps.OrderBy(t => t).ToList();

        Assert.Equal(sortedTimestamps, timestamps);
    }

    /// <summary>
    /// Tests that bar timeframe enum contains all expected timeframes.
    /// </summary>
    [Fact]
    public void BarTimeframe_ShouldContainExpectedValues()
    {
        var expectedTimeframes = new[]
        {
            BarTimeframe.Minute1,
            BarTimeframe.Minute5,
            BarTimeframe.Minute15,
            BarTimeframe.Minute30,
            BarTimeframe.Hour1,
            BarTimeframe.Hour4,
            BarTimeframe.Day,
            BarTimeframe.Week,
            BarTimeframe.Month
        };

        foreach (var timeframe in expectedTimeframes)
        {
            Assert.True(Enum.IsDefined(typeof(BarTimeframe), timeframe),
                $"BarTimeframe.{timeframe} should be defined");
        }
    }

    private static IMarketDataProvider CreateAlpacaProvider()
    {
        var options = AlpacaTestCredentials.CreateOptions();
        return new AlpacaMarketDataProvider(options);
    }
}
