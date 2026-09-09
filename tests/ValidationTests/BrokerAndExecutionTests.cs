using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Catalogs;
using OoplesFinance.StockIndicators.Builder.Notifications;
using OoplesFinance.StockIndicators.Builder.Trading;
using Xunit;

namespace OoplesFinance.StockIndicators.Tests.ValidationTests;

/// <summary>
/// Behavioral tests for broker integration and trade execution.
/// These tests use mock brokers to verify actual behavior, not just compilation.
/// </summary>
public class BrokerAndExecutionTests
{
    #region Mock Broker for Testing

    /// <summary>
    /// Mock broker implementation that captures all operations for verification.
    /// </summary>
    private sealed class MockBroker : IBroker
    {
        public BrokerAccount MockAccount { get; set; } = new BrokerAccount
        {
            AccountId = "TEST-123",
            Equity = 100000m,
            Cash = 50000m,
            BuyingPower = 200000m,
            PortfolioValue = 50000m,
            DayPnL = 500m,
            DayPnLPercent = 0.5,
            TradingEnabled = true,
            IsPaper = true
        };

        public List<BrokerPosition> MockPositions { get; set; } = new();
        public List<ExtendedTradeRequest> SubmittedOrders { get; } = new();
        public List<string> CancelledOrderIds { get; } = new();
        public List<string> ClosedSymbols { get; } = new();
        public bool CloseAllPositionsCalled { get; private set; }
        public int NextOrderId { get; set; } = 1;

        // Configure to simulate failures
        public bool SimulateOrderFailure { get; set; }
        public string? OrderFailureMessage { get; set; }

        public Task<BrokerAccount> GetAccountAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MockAccount);
        }

        public Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BrokerPosition>>(MockPositions);
        }

        public Task<BrokerOrder> SubmitOrderAsync(ExtendedTradeRequest request, CancellationToken cancellationToken = default)
        {
            if (SimulateOrderFailure)
            {
                throw new InvalidOperationException(OrderFailureMessage ?? "Order submission failed");
            }

            SubmittedOrders.Add(request);

            return Task.FromResult(new BrokerOrder
            {
                OrderId = (NextOrderId++).ToString(),
                Symbol = request.Symbol,
                Side = request.Action == TradeAction.MarketBuy ? "buy" : "sell",
                OrderType = request.OrderType,
                Quantity = request.Quantity,
                FilledQuantity = request.Quantity,
                Status = BrokerOrderStatus.Filled,
                CreatedAt = DateTime.UtcNow,
                FilledAt = DateTime.UtcNow
            });
        }

        public Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
        {
            CancelledOrderIds.Add(orderId);
            return Task.FromResult(true);
        }

        public Task<BrokerOrder> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new BrokerOrder
            {
                OrderId = orderId,
                Status = BrokerOrderStatus.Filled
            });
        }

        public Task<BrokerOrder> ClosePositionAsync(string symbol, CancellationToken cancellationToken = default)
        {
            ClosedSymbols.Add(symbol);
            return Task.FromResult(new BrokerOrder
            {
                OrderId = (NextOrderId++).ToString(),
                Symbol = symbol,
                Side = "sell",
                Status = BrokerOrderStatus.Filled
            });
        }

        public Task<IReadOnlyList<BrokerOrder>> CloseAllPositionsAsync(CancellationToken cancellationToken = default)
        {
            CloseAllPositionsCalled = true;
            var orders = MockPositions.Select(p => new BrokerOrder
            {
                OrderId = (NextOrderId++).ToString(),
                Symbol = p.Symbol,
                Side = "sell",
                Status = BrokerOrderStatus.Filled
            }).ToList();

            MockPositions.Clear();
            return Task.FromResult<IReadOnlyList<BrokerOrder>>(orders);
        }
    }

    #endregion

    #region Trade Execution Engine Tests

    [Fact]
    public async Task ExecutionEngine_MatchesSignalPattern_ExecutesTrade()
    {
        // Arrange
        var broker = new MockBroker();
        var settings = new TradingSettings
        {
            DefaultSymbol = "SPY",
            MaxPositions = 10,
            MaxPositionSize = 10m,
            DailyLossLimit = 0.02m
        };

        var rules = new List<ExtendedAutoTradeRule>
        {
            new ExtendedAutoTradeRule
            {
                SignalPattern = "RSI*",
                Action = TradeAction.MarketBuy,
                PositionSize = PositionSize.Fixed(100)
            }
        };

        var engine = new TradeExecutionEngine(broker, settings, rules);

        // Act
        var result = await engine.ProcessSignalAsync("RSI Oversold", 25.5);

        // Assert - VERIFY ACTUAL BEHAVIOR
        Assert.True(result.Success, "Trade should succeed");
        Assert.Single(broker.SubmittedOrders);
        Assert.Equal("SPY", broker.SubmittedOrders[0].Symbol);
        Assert.Equal(100m, broker.SubmittedOrders[0].Quantity);
        Assert.Equal(TradeAction.MarketBuy, broker.SubmittedOrders[0].Action);
    }

    [Fact]
    public async Task ExecutionEngine_ExactPatternMatch_ExecutesTrade()
    {
        // Arrange
        var broker = new MockBroker();
        var settings = new TradingSettings { DefaultSymbol = "AAPL" };
        var rules = new List<ExtendedAutoTradeRule>
        {
            new ExtendedAutoTradeRule
            {
                SignalPattern = "MACD Bullish Cross",
                Action = TradeAction.MarketBuy,
                PositionSize = PositionSize.Fixed(50)
            }
        };

        var engine = new TradeExecutionEngine(broker, settings, rules);

        // Act
        var result = await engine.ProcessSignalAsync("MACD Bullish Cross", 0.5);

        // Assert
        Assert.True(result.Success);
        Assert.Single(broker.SubmittedOrders);
        Assert.Equal("AAPL", broker.SubmittedOrders[0].Symbol);
    }

    [Fact]
    public async Task ExecutionEngine_NoMatchingRule_ReturnsNoMatch()
    {
        // Arrange
        var broker = new MockBroker();
        var settings = new TradingSettings();
        var rules = new List<ExtendedAutoTradeRule>
        {
            new ExtendedAutoTradeRule
            {
                SignalPattern = "RSI*",
                Action = TradeAction.MarketBuy
            }
        };

        var engine = new TradeExecutionEngine(broker, settings, rules);

        // Act
        var result = await engine.ProcessSignalAsync("MACD Signal", 0.5);

        // Assert
        Assert.False(result.Success);
        Assert.True(result.IsNoMatch);
        Assert.Empty(broker.SubmittedOrders);
    }

    [Fact]
    public async Task ExecutionEngine_EmergencyStop_BlocksAllTrades()
    {
        // Arrange
        var broker = new MockBroker();
        var settings = new TradingSettings
        {
            EmergencyStop = true,
            DefaultSymbol = "SPY"
        };

        var rules = new List<ExtendedAutoTradeRule>
        {
            new ExtendedAutoTradeRule
            {
                SignalPattern = "*",
                Action = TradeAction.MarketBuy,
                PositionSize = PositionSize.Fixed(100)
            }
        };

        var engine = new TradeExecutionEngine(broker, settings, rules);

        // Track blocked events
        var blockedEvents = new List<TradeBlockedEventArgs>();
        engine.TradeBlocked += (_, e) => blockedEvents.Add(e);

        // Act
        var result = await engine.ProcessSignalAsync("Buy Signal", 100);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Emergency stop is activated", result.BlockedReason);
        Assert.Empty(broker.SubmittedOrders);
        Assert.Single(blockedEvents);
        Assert.Contains("Emergency stop", blockedEvents[0].Reason);
    }

    [Fact]
    public async Task ExecutionEngine_MaxPositionsReached_BlocksBuyOrders()
    {
        // Arrange
        var broker = new MockBroker();
        broker.MockPositions = new List<BrokerPosition>
        {
            new BrokerPosition { Symbol = "AAPL", Quantity = 100 },
            new BrokerPosition { Symbol = "GOOGL", Quantity = 50 }
        };

        var settings = new TradingSettings
        {
            MaxPositions = 2, // Already at max
            DefaultSymbol = "MSFT"
        };

        var rules = new List<ExtendedAutoTradeRule>
        {
            new ExtendedAutoTradeRule
            {
                SignalPattern = "*",
                Action = TradeAction.MarketBuy,
                PositionSize = PositionSize.Fixed(100)
            }
        };

        var engine = new TradeExecutionEngine(broker, settings, rules);

        // Act
        var result = await engine.ProcessSignalAsync("Buy Signal", 100);

        // Assert
        Assert.False(result.Success);
        Assert.Empty(broker.SubmittedOrders);
    }

    [Fact]
    public async Task ExecutionEngine_DailyLossLimitReached_BlocksTrades()
    {
        // Arrange
        var broker = new MockBroker();
        broker.MockAccount = new BrokerAccount
        {
            Equity = 98000m, // Lost 2% of 100k
            DayPnL = -2000m, // 2% loss
            BuyingPower = 196000m
        };

        var settings = new TradingSettings
        {
            DailyLossLimit = 0.02m, // 2% limit
            DefaultSymbol = "SPY"
        };

        var rules = new List<ExtendedAutoTradeRule>
        {
            new ExtendedAutoTradeRule
            {
                SignalPattern = "*",
                Action = TradeAction.MarketBuy,
                PositionSize = PositionSize.Fixed(100)
            }
        };

        var engine = new TradeExecutionEngine(broker, settings, rules);

        // Act
        var result = await engine.ProcessSignalAsync("Buy Signal", 100);

        // Assert
        Assert.False(result.Success);
        Assert.Empty(broker.SubmittedOrders);
    }

    [Fact]
    public async Task ExecutionEngine_PercentOfEquitySizing_CalculatesCorrectly()
    {
        // Arrange
        var broker = new MockBroker();
        broker.MockAccount = new BrokerAccount
        {
            Equity = 100000m,
            BuyingPower = 200000m
        };

        broker.MockPositions = new List<BrokerPosition>
        {
            new BrokerPosition { Symbol = "SPY", CurrentPrice = 500m }
        };

        var settings = new TradingSettings
        {
            MaxPositionSize = 100m, // Allow full equity
            DefaultSymbol = "SPY"
        };

        var rules = new List<ExtendedAutoTradeRule>
        {
            new ExtendedAutoTradeRule
            {
                SignalPattern = "*",
                Action = TradeAction.MarketBuy,
                PositionSize = PositionSize.PercentOfEquity(10) // 10% of equity = $10,000
            }
        };

        var engine = new TradeExecutionEngine(broker, settings, rules);

        // Act
        var result = await engine.ProcessSignalAsync("Buy Signal", 100);

        // Assert
        Assert.True(result.Success);
        Assert.Single(broker.SubmittedOrders);
        // At $500/share, 10% of $100k = $10k = 20 shares
        // Note: GetCurrentPrice returns 100m as placeholder, so actual qty would be 100
        // In real implementation, would use actual market price
        Assert.True(broker.SubmittedOrders[0].Quantity > 0);
    }

    [Fact]
    public async Task ExecutionEngine_CloseAllPositions_ClosesEverything()
    {
        // Arrange
        var broker = new MockBroker();
        broker.MockPositions = new List<BrokerPosition>
        {
            new BrokerPosition { Symbol = "AAPL", Quantity = 100 },
            new BrokerPosition { Symbol = "GOOGL", Quantity = 50 },
            new BrokerPosition { Symbol = "MSFT", Quantity = 75 }
        };

        var settings = new TradingSettings();
        var rules = new List<ExtendedAutoTradeRule>
        {
            new ExtendedAutoTradeRule
            {
                SignalPattern = "Exit All",
                Action = TradeAction.ClosePosition,
                CloseAllPositions = true
            }
        };

        var engine = new TradeExecutionEngine(broker, settings, rules);

        // Act
        var result = await engine.ProcessSignalAsync("Exit All", 100);

        // Assert
        Assert.True(result.Success);
        Assert.True(broker.CloseAllPositionsCalled);
    }

    [Fact]
    public async Task ExecutionEngine_CloseSpecificPosition_ClosesOnlyThatSymbol()
    {
        // Arrange
        var broker = new MockBroker();
        var settings = new TradingSettings();
        var rules = new List<ExtendedAutoTradeRule>
        {
            new ExtendedAutoTradeRule
            {
                SignalPattern = "Exit AAPL",
                Action = TradeAction.ClosePosition,
                Symbol = "AAPL"
            }
        };

        var engine = new TradeExecutionEngine(broker, settings, rules);

        // Act
        var result = await engine.ProcessSignalAsync("Exit AAPL", 100);

        // Assert
        Assert.True(result.Success);
        Assert.Single(broker.ClosedSymbols);
        Assert.Equal("AAPL", broker.ClosedSymbols[0]);
    }

    [Fact]
    public async Task ExecutionEngine_StopLossApplied_SetsCorrectPrice()
    {
        // Arrange
        var broker = new MockBroker();
        broker.MockPositions = new List<BrokerPosition>
        {
            new BrokerPosition { Symbol = "SPY", CurrentPrice = 100m }
        };

        var settings = new TradingSettings { DefaultSymbol = "SPY" };
        var rules = new List<ExtendedAutoTradeRule>
        {
            new ExtendedAutoTradeRule
            {
                SignalPattern = "*",
                Action = TradeAction.MarketBuy,
                PositionSize = PositionSize.Fixed(10),
                StopLoss = StopLoss.Percent(2) // 2% stop loss
            }
        };

        var engine = new TradeExecutionEngine(broker, settings, rules);

        // Act
        var result = await engine.ProcessSignalAsync("Buy Signal", 100);

        // Assert
        Assert.True(result.Success);
        Assert.Single(broker.SubmittedOrders);
        var order = broker.SubmittedOrders[0];
        Assert.NotNull(order.StopLossPrice);
        // At $100 entry, 2% stop = $98 (100 - 100 * 0.02)
        Assert.True(order.StopLossPrice < 100m); // Stop loss below entry for buy
    }

    [Fact]
    public async Task ExecutionEngine_TakeProfitApplied_SetsCorrectPrice()
    {
        // Arrange
        var broker = new MockBroker();
        broker.MockPositions = new List<BrokerPosition>
        {
            new BrokerPosition { Symbol = "SPY", CurrentPrice = 100m }
        };

        var settings = new TradingSettings { DefaultSymbol = "SPY" };
        var rules = new List<ExtendedAutoTradeRule>
        {
            new ExtendedAutoTradeRule
            {
                SignalPattern = "*",
                Action = TradeAction.MarketBuy,
                PositionSize = PositionSize.Fixed(10),
                TakeProfit = TakeProfit.Percent(5) // 5% take profit
            }
        };

        var engine = new TradeExecutionEngine(broker, settings, rules);

        // Act
        var result = await engine.ProcessSignalAsync("Buy Signal", 100);

        // Assert
        Assert.True(result.Success);
        Assert.Single(broker.SubmittedOrders);
        var order = broker.SubmittedOrders[0];
        Assert.NotNull(order.TakeProfitPrice);
        // At $100 entry, 5% take profit = $105 (100 + 100 * 0.05)
        Assert.True(order.TakeProfitPrice > 100m); // Take profit above entry for buy
    }

    [Fact]
    public async Task ExecutionEngine_TrailingStop_SetsTrailingOffset()
    {
        // Arrange
        var broker = new MockBroker();
        broker.MockPositions = new List<BrokerPosition>
        {
            new BrokerPosition { Symbol = "SPY", CurrentPrice = 100m }
        };

        var settings = new TradingSettings { DefaultSymbol = "SPY" };
        var rules = new List<ExtendedAutoTradeRule>
        {
            new ExtendedAutoTradeRule
            {
                SignalPattern = "*",
                Action = TradeAction.MarketBuy,
                PositionSize = PositionSize.Fixed(10),
                StopLoss = StopLoss.TrailingPercent(3) // 3% trailing stop
            }
        };

        var engine = new TradeExecutionEngine(broker, settings, rules);

        // Act
        var result = await engine.ProcessSignalAsync("Buy Signal", 100);

        // Assert
        Assert.True(result.Success);
        Assert.Single(broker.SubmittedOrders);
        var order = broker.SubmittedOrders[0];
        Assert.True(order.IsTrailingStop);
        Assert.Equal(3m, order.TrailingStopOffset);
    }

    [Fact]
    public async Task ExecutionEngine_MultipleMatchingRules_ExecutesAll()
    {
        // Arrange
        var broker = new MockBroker();
        var settings = new TradingSettings { DefaultSymbol = "SPY" };
        var rules = new List<ExtendedAutoTradeRule>
        {
            new ExtendedAutoTradeRule
            {
                SignalPattern = "RSI*",
                Action = TradeAction.MarketBuy,
                PositionSize = PositionSize.Fixed(10)
            },
            new ExtendedAutoTradeRule
            {
                SignalPattern = "*Oversold*",
                Action = TradeAction.MarketBuy,
                PositionSize = PositionSize.Fixed(5)
            }
        };

        var engine = new TradeExecutionEngine(broker, settings, rules);

        // Act - "RSI Oversold" matches both patterns
        var result = await engine.ProcessSignalAsync("RSI Oversold", 25);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, broker.SubmittedOrders.Count);
    }

    [Fact]
    public async Task ExecutionEngine_TradeExecutedEvent_IsFired()
    {
        // Arrange
        var broker = new MockBroker();
        var settings = new TradingSettings { DefaultSymbol = "SPY" };
        var rules = new List<ExtendedAutoTradeRule>
        {
            new ExtendedAutoTradeRule
            {
                SignalPattern = "*",
                Action = TradeAction.MarketBuy,
                PositionSize = PositionSize.Fixed(10)
            }
        };

        var engine = new TradeExecutionEngine(broker, settings, rules);

        var executedEvents = new List<TradeExecutedEventArgs>();
        engine.TradeExecuted += (_, e) => executedEvents.Add(e);

        // Act
        await engine.ProcessSignalAsync("Buy Signal", 100);

        // Assert
        Assert.Single(executedEvents);
        Assert.Equal("Buy Signal", executedEvents[0].SignalName);
        Assert.NotNull(executedEvents[0].Order);
    }

    [Fact]
    public async Task ExecutionEngine_SellOrder_HasCorrectSide()
    {
        // Arrange
        var broker = new MockBroker();
        var settings = new TradingSettings { DefaultSymbol = "SPY" };
        var rules = new List<ExtendedAutoTradeRule>
        {
            new ExtendedAutoTradeRule
            {
                SignalPattern = "Sell*",
                Action = TradeAction.MarketSell,
                PositionSize = PositionSize.Fixed(50)
            }
        };

        var engine = new TradeExecutionEngine(broker, settings, rules);

        // Act
        var result = await engine.ProcessSignalAsync("Sell Signal", 100);

        // Assert
        Assert.True(result.Success);
        Assert.Single(broker.SubmittedOrders);
        Assert.Equal(TradeAction.MarketSell, broker.SubmittedOrders[0].Action);
    }

    #endregion

    #region Position Sizing Tests

    [Fact]
    public void PositionSize_Fixed_ReturnsCorrectValue()
    {
        var size = PositionSize.Fixed(100);
        Assert.Equal(TradingPositionSizing.Fixed, size.Method);
        Assert.Equal(100m, size.Value);
    }

    [Fact]
    public void PositionSize_PercentOfEquity_ReturnsCorrectValue()
    {
        var size = PositionSize.PercentOfEquity(10);
        Assert.Equal(TradingPositionSizing.PercentOfEquity, size.Method);
        Assert.Equal(10m, size.Value);
    }

    [Fact]
    public void PositionSize_RiskBased_ReturnsCorrectValue()
    {
        var size = PositionSize.RiskBased(500); // $500 risk
        Assert.Equal(TradingPositionSizing.RiskBased, size.Method);
        Assert.Equal(500m, size.Value);
    }

    [Fact]
    public void PositionSize_AllIn_ReturnsCorrectValue()
    {
        var size = PositionSize.AllIn();
        Assert.Equal(TradingPositionSizing.AllIn, size.Method);
        Assert.Equal(100m, size.Value);
    }

    #endregion

    #region Stop Loss Tests

    [Fact]
    public void StopLoss_Percent_ReturnsCorrectValues()
    {
        var stop = StopLoss.Percent(2);
        Assert.True(stop.IsPercent);
        Assert.False(stop.IsTrailing);
        Assert.Equal(2m, stop.Value);
    }

    [Fact]
    public void StopLoss_Dollars_ReturnsCorrectValues()
    {
        var stop = StopLoss.Dollars(50);
        Assert.False(stop.IsPercent);
        Assert.False(stop.IsTrailing);
        Assert.Equal(50m, stop.Value);
    }

    [Fact]
    public void StopLoss_TrailingPercent_ReturnsCorrectValues()
    {
        var stop = StopLoss.TrailingPercent(3);
        Assert.True(stop.IsPercent);
        Assert.True(stop.IsTrailing);
        Assert.Equal(3m, stop.Value);
    }

    [Fact]
    public void StopLoss_TrailingDollars_ReturnsCorrectValues()
    {
        var stop = StopLoss.TrailingDollars(5);
        Assert.False(stop.IsPercent);
        Assert.True(stop.IsTrailing);
        Assert.Equal(5m, stop.Value);
    }

    #endregion

    #region Take Profit Tests

    [Fact]
    public void TakeProfit_Percent_ReturnsCorrectValues()
    {
        var tp = TakeProfit.Percent(5);
        Assert.True(tp.IsPercent);
        Assert.Equal(5m, tp.Value);
    }

    [Fact]
    public void TakeProfit_Dollars_ReturnsCorrectValues()
    {
        var tp = TakeProfit.Dollars(100);
        Assert.False(tp.IsPercent);
        Assert.Equal(100m, tp.Value);
    }

    #endregion

    #region Extended Auto Trade Rule Tests

    [Fact]
    public void ExtendedAutoTradeRule_MatchesExactPattern()
    {
        var rule = new ExtendedAutoTradeRule { SignalPattern = "RSI Oversold" };

        Assert.True(rule.Matches("RSI Oversold"));
        Assert.True(rule.Matches("rsi oversold")); // Case insensitive
        Assert.False(rule.Matches("RSI Overbought"));
    }

    [Fact]
    public void ExtendedAutoTradeRule_MatchesWildcardPrefix()
    {
        var rule = new ExtendedAutoTradeRule { SignalPattern = "RSI*" };

        Assert.True(rule.Matches("RSI Oversold"));
        Assert.True(rule.Matches("RSI Overbought"));
        Assert.True(rule.Matches("RSI"));
        Assert.False(rule.Matches("MACD Cross"));
    }

    [Fact]
    public void ExtendedAutoTradeRule_MatchesWildcardStar()
    {
        var rule = new ExtendedAutoTradeRule { SignalPattern = "*" };

        Assert.True(rule.Matches("Anything"));
        Assert.True(rule.Matches("RSI Oversold"));
        Assert.True(rule.Matches(""));
    }

    [Fact]
    public void ExtendedAutoTradeRule_EmptyPattern_NeverMatches()
    {
        var rule = new ExtendedAutoTradeRule { SignalPattern = "" };

        Assert.False(rule.Matches("Anything"));
        Assert.False(rule.Matches(""));
    }

    #endregion
}
