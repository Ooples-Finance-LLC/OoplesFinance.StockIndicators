using FluentAssertions;
using OoplesFinance.StockIndicators.Builder.Trading;
using OoplesFinance.StockIndicators.Builder.Trading.Brokers;
using Xunit;

namespace TradingApp.Tests.Integration;

/// <summary>
/// Integration tests for broker APIs.
/// These tests require valid API credentials and run against real (paper) accounts.
/// Set environment variables before running:
/// - ALPACA_API_KEY
/// - ALPACA_API_SECRET
/// </summary>
[Trait("Category", "Integration")]
public class AlpacaBrokerIntegrationTests : IAsyncLifetime
{
    private AlpacaBroker? _broker;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly bool _canRun;

    public AlpacaBrokerIntegrationTests()
    {
        _apiKey = Environment.GetEnvironmentVariable("ALPACA_API_KEY") ?? string.Empty;
        _apiSecret = Environment.GetEnvironmentVariable("ALPACA_API_SECRET") ?? string.Empty;
        _canRun = !string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_apiSecret);
    }

    public Task InitializeAsync()
    {
        if (_canRun)
        {
            _broker = new AlpacaBroker(new AlpacaOptions
            {
                ApiKey = _apiKey,
                ApiSecret = _apiSecret,
                UsePaper = true // Always use paper trading for tests
            });
        }
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _broker?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAccountAsync_ReturnsValidAccount()
    {
        Skip.If(!_canRun, "Alpaca credentials not configured");

        // Act
        var account = await _broker!.GetAccountAsync();

        // Assert
        account.Should().NotBeNull();
        account.AccountId.Should().NotBeNullOrEmpty();
        account.Equity.Should().BeGreaterOrEqualTo(0);
        account.Cash.Should().BeGreaterOrEqualTo(0);
        account.BuyingPower.Should().BeGreaterOrEqualTo(0);
        account.IsPaper.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetPositionsAsync_ReturnsPositionsList()
    {
        Skip.If(!_canRun, "Alpaca credentials not configured");

        // Act
        var positions = await _broker!.GetPositionsAsync();

        // Assert
        positions.Should().NotBeNull();
        // Positions list may be empty, but should not be null
        foreach (var position in positions)
        {
            position.Symbol.Should().NotBeNullOrEmpty();
            position.Quantity.Should().NotBe(0);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SubmitAndCancelOrder_WorksCorrectly()
    {
        Skip.If(!_canRun, "Alpaca credentials not configured");

        // Arrange - Use a limit order far from market price to avoid fills
        var request = new ExtendedTradeRequest
        {
            Symbol = "AAPL",
            Action = TradeAction.MarketBuy,
            Quantity = 1,
            OrderType = OrderType.Limit,
            LimitPrice = 1.00m, // Very low price, won't fill
            TimeInForce = TimeInForce.Day
        };

        // Act - Submit order
        var order = await _broker!.SubmitOrderAsync(request);

        // Assert - Order submitted
        order.Should().NotBeNull();
        order.OrderId.Should().NotBeNullOrEmpty();
        order.Symbol.Should().Be("AAPL");
        order.Status.Should().BeOneOf(BrokerOrderStatus.New, BrokerOrderStatus.Accepted, BrokerOrderStatus.PendingNew);

        // Act - Cancel order
        await Task.Delay(500); // Give broker time to process
        var cancelled = await _broker.CancelOrderAsync(order.OrderId);

        // Assert - Order cancelled
        cancelled.Should().BeTrue();

        // Verify cancellation
        await Task.Delay(500);
        var cancelledOrder = await _broker.GetOrderAsync(order.OrderId);
        cancelledOrder.Status.Should().BeOneOf(BrokerOrderStatus.Cancelled, BrokerOrderStatus.PendingCancel);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetOrderAsync_ReturnsOrderDetails()
    {
        Skip.If(!_canRun, "Alpaca credentials not configured");

        // Arrange - Submit an order first
        var request = new ExtendedTradeRequest
        {
            Symbol = "MSFT",
            Action = TradeAction.MarketBuy,
            Quantity = 1,
            OrderType = OrderType.Limit,
            LimitPrice = 1.00m,
            TimeInForce = TimeInForce.Day
        };

        var submittedOrder = await _broker!.SubmitOrderAsync(request);

        try
        {
            // Act
            await Task.Delay(500);
            var order = await _broker.GetOrderAsync(submittedOrder.OrderId);

            // Assert
            order.Should().NotBeNull();
            order.OrderId.Should().Be(submittedOrder.OrderId);
            order.Symbol.Should().Be("MSFT");
        }
        finally
        {
            // Cleanup
            await _broker.CancelOrderAsync(submittedOrder.OrderId);
        }
    }
}

/// <summary>
/// Integration tests for Binance broker.
/// </summary>
[Trait("Category", "Integration")]
public class BinanceBrokerIntegrationTests : IAsyncLifetime
{
    private Binance.BinanceBroker? _broker;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly bool _canRun;

    public BinanceBrokerIntegrationTests()
    {
        _apiKey = Environment.GetEnvironmentVariable("BINANCE_API_KEY") ?? string.Empty;
        _apiSecret = Environment.GetEnvironmentVariable("BINANCE_API_SECRET") ?? string.Empty;
        _canRun = !string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_apiSecret);
    }

    public Task InitializeAsync()
    {
        if (_canRun)
        {
            _broker = new Binance.BinanceBroker(new Binance.BinanceOptions
            {
                ApiKey = _apiKey,
                ApiSecret = _apiSecret,
                UseTestnet = true // Always use testnet for tests
            });
        }
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _broker?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAccountAsync_ReturnsValidAccount()
    {
        Skip.If(!_canRun, "Binance credentials not configured");

        // Act
        var account = await _broker!.GetAccountAsync();

        // Assert
        account.Should().NotBeNull();
        account.AccountId.Should().NotBeNullOrEmpty();
        account.IsPaper.Should().BeTrue(); // Should be testnet
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetPositionsAsync_ReturnsPositionsList()
    {
        Skip.If(!_canRun, "Binance credentials not configured");

        // Act
        var positions = await _broker!.GetPositionsAsync();

        // Assert
        positions.Should().NotBeNull();
    }
}

/// <summary>
/// Market data integration tests.
/// </summary>
[Trait("Category", "Integration")]
public class MarketDataIntegrationTests : IAsyncLifetime
{
    private AlpacaMarketDataProvider? _provider;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly bool _canRun;

    public MarketDataIntegrationTests()
    {
        _apiKey = Environment.GetEnvironmentVariable("ALPACA_API_KEY") ?? string.Empty;
        _apiSecret = Environment.GetEnvironmentVariable("ALPACA_API_SECRET") ?? string.Empty;
        _canRun = !string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_apiSecret);
    }

    public Task InitializeAsync()
    {
        if (_canRun)
        {
            _provider = new AlpacaMarketDataProvider(new AlpacaMarketDataOptions
            {
                ApiKey = _apiKey,
                ApiSecret = _apiSecret,
                UsePaper = true
            });
        }
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _provider?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetQuoteAsync_ReturnsValidQuote()
    {
        Skip.If(!_canRun, "Alpaca credentials not configured");

        // Act
        var quote = await _provider!.GetQuoteAsync("AAPL");

        // Assert
        quote.Should().NotBeNull();
        quote!.LastPrice.Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetHistoricalBarsAsync_ReturnsBars()
    {
        Skip.If(!_canRun, "Alpaca credentials not configured");

        // Arrange
        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-30);

        // Act
        var bars = await _provider!.GetHistoricalBarsAsync("AAPL", startDate, endDate, MarketDataInterval.Day);

        // Assert
        bars.Should().NotBeNull();
        bars.Should().NotBeEmpty();

        foreach (var bar in bars)
        {
            bar.Open.Should().BeGreaterThan(0);
            bar.High.Should().BeGreaterOrEqualTo(bar.Low);
            bar.Close.Should().BeGreaterThan(0);
            bar.Volume.Should().BeGreaterOrEqualTo(0);
        }
    }
}

/// <summary>
/// Helper class for skipping tests when conditions aren't met.
/// </summary>
public static class Skip
{
    public static void If(bool condition, string reason)
    {
        if (condition)
        {
            throw new Xunit.SkipException(reason);
        }
    }
}
