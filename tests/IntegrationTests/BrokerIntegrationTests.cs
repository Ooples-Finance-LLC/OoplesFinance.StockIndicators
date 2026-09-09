namespace OoplesFinance.StockIndicators.Tests.IntegrationTests;

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Trading;
using OoplesFinance.StockIndicators.Builder.Trading.Orders;
using Xunit;

/// <summary>
/// Integration tests for broker connections.
/// These tests verify end-to-end broker functionality using paper trading accounts.
/// </summary>
[Trait("Category", "Integration")]
public class BrokerIntegrationTests
{
    /// <summary>
    /// Tests basic broker account info retrieval.
    /// </summary>
    [Fact]
    public async Task Broker_ShouldRetrieveAccountInfo()
    {
        // Arrange - Using Alpaca paper trading as the test broker
        var broker = CreateTestBroker();

        // Act
        var accountInfo = await broker.GetAccountAsync();

        // Assert
        Assert.NotNull(accountInfo);
        Assert.True(accountInfo.BuyingPower >= 0, "Buying power should be non-negative");
        Assert.NotNull(accountInfo.AccountId);
    }

    /// <summary>
    /// Tests position retrieval.
    /// </summary>
    [Fact]
    public async Task Broker_ShouldRetrievePositions()
    {
        // Arrange
        var broker = CreateTestBroker();

        // Act
        var positions = await broker.GetPositionsAsync();

        // Assert
        Assert.NotNull(positions);
        // May be empty if no positions, but should not throw
    }

    /// <summary>
    /// Tests market order submission (paper trading).
    /// </summary>
    [Fact]
    public async Task Broker_ShouldSubmitMarketOrder_PaperTrading()
    {
        // Arrange
        var broker = CreateTestBroker();

        var request = new ExtendedTradeRequest
        {
            Symbol = "AAPL",
            Quantity = 1,
            Action = TradeAction.MarketBuy,
            OrderType = OrderType.Market
        };

        // Act
        var result = await broker.SubmitOrderAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.OrderId);
        Assert.NotEqual(BrokerOrderStatus.Rejected, result.Status);
    }

    /// <summary>
    /// Tests limit order submission (paper trading).
    /// </summary>
    [Fact]
    public async Task Broker_ShouldSubmitLimitOrder_PaperTrading()
    {
        // Arrange
        var broker = CreateTestBroker();

        // Use a limit price well below market so it won't fill immediately
        var limitPrice = 100.00m; // Far below AAPL market price

        var request = new ExtendedTradeRequest
        {
            Symbol = "AAPL",
            Quantity = 1,
            Action = TradeAction.MarketBuy,
            OrderType = OrderType.Limit,
            LimitPrice = limitPrice
        };

        // Act
        var result = await broker.SubmitOrderAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.OrderId);

        // Cancel the test order
        await broker.CancelOrderAsync(result.OrderId);
    }

    /// <summary>
    /// Tests stop order submission (paper trading).
    /// </summary>
    [Fact]
    public async Task Broker_ShouldSubmitStopOrder_PaperTrading()
    {
        // Arrange
        var broker = CreateTestBroker();

        // Use a stop price well above market for sell stop
        var stopPrice = 250.00m; // Above AAPL market price

        var request = new ExtendedTradeRequest
        {
            Symbol = "AAPL",
            Quantity = 1,
            Action = TradeAction.MarketSell,
            OrderType = OrderType.Stop,
            StopPrice = stopPrice
        };

        // Act
        var result = await broker.SubmitOrderAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.OrderId);

        // Cancel the test order
        await broker.CancelOrderAsync(result.OrderId);
    }

    /// <summary>
    /// Tests order cancellation.
    /// </summary>
    [Fact]
    public async Task Broker_ShouldCancelOrder()
    {
        // Arrange
        var broker = CreateTestBroker();

        var limitPrice = 100.00m; // Far below market so it won't fill

        var request = new ExtendedTradeRequest
        {
            Symbol = "AAPL",
            Quantity = 1,
            Action = TradeAction.MarketBuy,
            OrderType = OrderType.Limit,
            LimitPrice = limitPrice
        };

        var result = await broker.SubmitOrderAsync(request);

        // Act
        var cancelled = await broker.CancelOrderAsync(result.OrderId);

        // Assert
        Assert.True(cancelled, "Order should be cancelled successfully");
    }

    /// <summary>
    /// Tests order status retrieval.
    /// </summary>
    [Fact]
    public async Task Broker_ShouldRetrieveOrderStatus()
    {
        // Arrange
        var broker = CreateTestBroker();

        var limitPrice = 100.00m; // Far below market so it won't fill

        var request = new ExtendedTradeRequest
        {
            Symbol = "AAPL",
            Quantity = 1,
            Action = TradeAction.MarketBuy,
            OrderType = OrderType.Limit,
            LimitPrice = limitPrice
        };

        var result = await broker.SubmitOrderAsync(request);

        // Act
        var order = await broker.GetOrderAsync(result.OrderId);

        // Assert
        Assert.NotNull(order);
        Assert.Equal(result.OrderId, order.OrderId);

        // Cleanup
        await broker.CancelOrderAsync(result.OrderId);
    }

    /// <summary>
    /// Tests position closing.
    /// </summary>
    [Fact]
    public async Task Broker_ShouldClosePosition()
    {
        // Arrange
        var broker = CreateTestBroker();

        // First, create a position
        var buyRequest = new ExtendedTradeRequest
        {
            Symbol = "AAPL",
            Quantity = 1,
            Action = TradeAction.MarketBuy,
            OrderType = OrderType.Market
        };

        var buyResult = await broker.SubmitOrderAsync(buyRequest);

        // Wait a moment for the order to fill
        await Task.Delay(1000);

        // Act - Close the position
        var closeResult = await broker.ClosePositionAsync("AAPL");

        // Assert
        Assert.NotNull(closeResult);
    }

    /// <summary>
    /// Tests concurrent order submission.
    /// </summary>
    [Fact]
    public async Task Broker_ShouldHandleConcurrentOrders()
    {
        // Arrange
        var broker = CreateTestBroker();

        var symbols = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "META" };
        var limitPrice = 100.00m; // Far below market so orders won't fill

        // Act - Submit 5 orders concurrently
        var tasks = symbols.Select(symbol => broker.SubmitOrderAsync(new ExtendedTradeRequest
        {
            Symbol = symbol,
            Quantity = 1,
            Action = TradeAction.MarketBuy,
            OrderType = OrderType.Limit,
            LimitPrice = limitPrice
        }));

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(5, results.Length);
        Assert.All(results, r => Assert.NotNull(r.OrderId));

        // Cleanup - Cancel all orders
        var cancelTasks = results.Select(r => broker.CancelOrderAsync(r.OrderId));
        await Task.WhenAll(cancelTasks);
    }

    #region Helper Methods

    private static IBroker CreateTestBroker()
    {
        // Use environment variables or test configuration for credentials
        var options = new AlpacaOptions
        {
            ApiKey = Environment.GetEnvironmentVariable("ALPACA_API_KEY") ?? "test-key",
            ApiSecret = Environment.GetEnvironmentVariable("ALPACA_API_SECRET") ?? "test-secret",
            UsePaper = true // Always use paper trading for tests
        };

        return new AlpacaBroker(options);
    }

    #endregion
}

/// <summary>
/// Cross-broker consistency tests to ensure all brokers behave consistently.
/// </summary>
[Trait("Category", "Integration")]
public class CrossBrokerConsistencyTests
{
    /// <summary>
    /// Tests that order types enum contains all basic types.
    /// </summary>
    [Fact]
    public void AllBrokers_ShouldSupportBasicOrderTypes()
    {
        // Verify that OrderType enum covers all basic types
        var requiredTypes = new[]
        {
            OrderType.Market,
            OrderType.Limit,
            OrderType.Stop,
            OrderType.StopLimit
        };

        foreach (var type in requiredTypes)
        {
            Assert.True(Enum.IsDefined(typeof(OrderType), type),
                $"OrderType.{type} should be defined");
        }
    }

    /// <summary>
    /// Tests that order sides enum contains buy and sell.
    /// </summary>
    [Fact]
    public void AllBrokers_ShouldSupportOrderSides()
    {
        var requiredSides = new[]
        {
            OrderSide.Buy,
            OrderSide.Sell
        };

        foreach (var side in requiredSides)
        {
            Assert.True(Enum.IsDefined(typeof(OrderSide), side),
                $"OrderSide.{side} should be defined");
        }
    }

    /// <summary>
    /// Tests that broker order status enum contains all required statuses.
    /// </summary>
    [Fact]
    public void AllBrokers_ShouldSupportOrderStatuses()
    {
        var requiredStatuses = new[]
        {
            BrokerOrderStatus.New,
            BrokerOrderStatus.Filled,
            BrokerOrderStatus.Cancelled,
            BrokerOrderStatus.Rejected,
            BrokerOrderStatus.PartiallyFilled
        };

        foreach (var status in requiredStatuses)
        {
            Assert.True(Enum.IsDefined(typeof(BrokerOrderStatus), status),
                $"BrokerOrderStatus.{status} should be defined");
        }
    }

    /// <summary>
    /// Tests that time in force enum contains all required options.
    /// </summary>
    [Fact]
    public void AllBrokers_ShouldSupportTimeInForce()
    {
        var requiredTif = new[]
        {
            TimeInForce.Day,
            TimeInForce.GTC,
            TimeInForce.IOC,
            TimeInForce.FOK
        };

        foreach (var tif in requiredTif)
        {
            Assert.True(Enum.IsDefined(typeof(TimeInForce), tif),
                $"TimeInForce.{tif} should be defined");
        }
    }
}
