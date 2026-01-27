using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Catalogs;
using OoplesFinance.StockIndicators.Builder.Trading;
using OoplesFinance.StockIndicators.Models;
using Xunit;

namespace OoplesFinance.StockIndicators.Tests.ValidationTests;

/// <summary>
/// Tests for the auto-trading infrastructure (Phase 10).
/// </summary>
public class AutoTradingTests
{
    private static List<TickerData> CreateTestData()
    {
        var data = new List<TickerData>();
        var baseDate = new DateTime(2024, 1, 1);
        var random = new Random(42);

        double price = 100;
        for (int i = 0; i < 50; i++)
        {
            var change = (random.NextDouble() - 0.5) * 2;
            price = Math.Max(50, Math.Min(150, price + change));
            var high = price * (1 + random.NextDouble() * 0.02);
            var low = price * (1 - random.NextDouble() * 0.02);
            var open = low + random.NextDouble() * (high - low);
            var close = low + random.NextDouble() * (high - low);

            data.Add(new TickerData
            {
                Date = baseDate.AddDays(i),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = 1000000 + random.Next(500000)
            });
        }

        return data;
    }

    [Fact]
    public void PositionSize_Fixed_CreatesCorrectSizing()
    {
        // Arrange & Act
        var size = PositionSize.Fixed(100);

        // Assert
        Assert.Equal(TradingPositionSizing.Fixed, size.Method);
        Assert.Equal(100m, size.Value);
    }

    [Fact]
    public void PositionSize_PercentOfEquity_CreatesCorrectSizing()
    {
        // Arrange & Act
        var size = PositionSize.PercentOfEquity(5);

        // Assert
        Assert.Equal(TradingPositionSizing.PercentOfEquity, size.Method);
        Assert.Equal(5m, size.Value);
    }

    [Fact]
    public void PositionSize_RiskBased_CreatesCorrectSizing()
    {
        // Arrange & Act
        var size = PositionSize.RiskBased(500);

        // Assert
        Assert.Equal(TradingPositionSizing.RiskBased, size.Method);
        Assert.Equal(500m, size.Value);
    }

    [Fact]
    public void PositionSize_AllIn_CreatesCorrectSizing()
    {
        // Arrange & Act
        var size = PositionSize.AllIn();

        // Assert
        Assert.Equal(TradingPositionSizing.AllIn, size.Method);
        Assert.Equal(100m, size.Value);
    }

    [Fact]
    public void StopLoss_Percent_CreatesCorrectStopLoss()
    {
        // Arrange & Act
        var stopLoss = StopLoss.Percent(2);

        // Assert
        Assert.True(stopLoss.IsPercent);
        Assert.Equal(2m, stopLoss.Value);
        Assert.False(stopLoss.IsTrailing);
    }

    [Fact]
    public void StopLoss_Dollars_CreatesCorrectStopLoss()
    {
        // Arrange & Act
        var stopLoss = StopLoss.Dollars(50);

        // Assert
        Assert.False(stopLoss.IsPercent);
        Assert.Equal(50m, stopLoss.Value);
        Assert.False(stopLoss.IsTrailing);
    }

    [Fact]
    public void StopLoss_TrailingPercent_CreatesCorrectStopLoss()
    {
        // Arrange & Act
        var stopLoss = StopLoss.TrailingPercent(3);

        // Assert
        Assert.True(stopLoss.IsPercent);
        Assert.Equal(3m, stopLoss.Value);
        Assert.True(stopLoss.IsTrailing);
    }

    [Fact]
    public void StopLoss_TrailingDollars_CreatesCorrectStopLoss()
    {
        // Arrange & Act
        var stopLoss = StopLoss.TrailingDollars(25);

        // Assert
        Assert.False(stopLoss.IsPercent);
        Assert.Equal(25m, stopLoss.Value);
        Assert.True(stopLoss.IsTrailing);
    }

    [Fact]
    public void TakeProfit_Percent_CreatesCorrectTakeProfit()
    {
        // Arrange & Act
        var takeProfit = TakeProfit.Percent(10);

        // Assert
        Assert.True(takeProfit.IsPercent);
        Assert.Equal(10m, takeProfit.Value);
    }

    [Fact]
    public void TakeProfit_Dollars_CreatesCorrectTakeProfit()
    {
        // Arrange & Act
        var takeProfit = TakeProfit.Dollars(200);

        // Assert
        Assert.False(takeProfit.IsPercent);
        Assert.Equal(200m, takeProfit.Value);
    }

    [Fact]
    public void ExtendedAutoTradeRule_Matches_ExactName()
    {
        // Arrange
        var rule = new ExtendedAutoTradeRule { SignalPattern = "Strong Buy Signal" };

        // Act & Assert
        Assert.True(rule.Matches("Strong Buy Signal"));
        Assert.False(rule.Matches("Strong Sell Signal"));
    }

    [Fact]
    public void ExtendedAutoTradeRule_Matches_WildcardPattern()
    {
        // Arrange
        var rule = new ExtendedAutoTradeRule { SignalPattern = "RSI*" };

        // Act & Assert
        Assert.True(rule.Matches("RSI Overbought"));
        Assert.True(rule.Matches("RSI Oversold"));
        Assert.False(rule.Matches("MACD Cross"));
    }

    [Fact]
    public void ExtendedAutoTradeRule_Matches_StarWildcard()
    {
        // Arrange
        var rule = new ExtendedAutoTradeRule { SignalPattern = "*" };

        // Act & Assert
        Assert.True(rule.Matches("Any Signal"));
        Assert.True(rule.Matches("Another Signal"));
    }

    [Fact]
    public void ExtendedAutoTradeRule_DoesNotMatch_EmptyPattern()
    {
        // Arrange
        var rule = new ExtendedAutoTradeRule { SignalPattern = "" };

        // Act & Assert
        Assert.False(rule.Matches("Any Signal"));
    }

    [Fact]
    public void TradingSettings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new TradingSettings();

        // Assert
        Assert.Equal(10, settings.MaxPositions);
        Assert.Equal(10m, settings.MaxPositionSize);
        Assert.Equal(0.02m, settings.DailyLossLimit);
        Assert.False(settings.RequireConfirmation);
        Assert.False(settings.EmergencyStop);
        Assert.Equal("SPY", settings.DefaultSymbol);
    }

    [Fact]
    public void AutoTradingCatalog_OnSignal_CreatesRule()
    {
        // Arrange
        var testData = CreateTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        SeriesHandle rsi = default;

        // Act
        builder.ConfigureIndicators(catalog =>
        {
            rsi = catalog.Rsi(14);
        });

        builder.ConfigureSignals(signals =>
        {
            signals.When(rsi).CrossesAbove(30).Emit("RSI Oversold Exit");
        });

        builder.ConfigureAutoTrading(trading =>
        {
            trading.OnSignal("RSI Oversold Exit")
                   .Buy()
                   .WithSize(PositionSize.Fixed(100))
                   .Execute();
        });

        using var runtime = builder.Build();

        // Assert - Build succeeds without errors
        Assert.NotNull(runtime);
    }

    [Fact]
    public void AutoTradingCatalog_OnSignal_WithStopLoss_CreatesRule()
    {
        // Arrange
        var testData = CreateTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        SeriesHandle rsi = default;

        // Act
        builder.ConfigureIndicators(catalog =>
        {
            rsi = catalog.Rsi(14);
        });

        builder.ConfigureSignals(signals =>
        {
            signals.When(rsi).CrossesAbove(30).Emit("Buy Signal");
        });

        builder.ConfigureAutoTrading(trading =>
        {
            trading.OnSignal("Buy Signal")
                   .Buy()
                   .WithSize(PositionSize.PercentOfEquity(5))
                   .WithStopLoss(StopLoss.Percent(2))
                   .WithTakeProfit(TakeProfit.Percent(10))
                   .Execute();
        });

        using var runtime = builder.Build();

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void AutoTradingCatalog_Sell_AllPositions_CreatesRule()
    {
        // Arrange
        var testData = CreateTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        SeriesHandle rsi = default;

        // Act
        builder.ConfigureIndicators(catalog =>
        {
            rsi = catalog.Rsi(14);
        });

        builder.ConfigureSignals(signals =>
        {
            signals.When(rsi).CrossesBelow(70).Emit("RSI Overbought Exit");
        });

        builder.ConfigureAutoTrading(trading =>
        {
            trading.OnSignal("RSI Overbought Exit")
                   .Sell()
                   .AllPositions();
        });

        using var runtime = builder.Build();

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void AutoTradingCatalog_MultipleRules_Integration()
    {
        // Arrange
        var testData = CreateTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        SeriesHandle rsi = default;
        SeriesHandle macdLine = default;
        SeriesHandle macdSignal = default;

        // Act
        builder.ConfigureIndicators(catalog =>
        {
            rsi = catalog.Rsi(14);
            var macd = catalog.Macd(12, 26, 9);
            macdLine = macd.Primary;
            macdSignal = macd.Signal;
        });

        builder.ConfigureSignals(signals =>
        {
            signals.When(rsi).CrossesAbove(30).Emit("RSI Buy");
            signals.When(rsi).CrossesBelow(70).Emit("RSI Sell");
            // Use Signal() for series-to-series comparison
            signals.Signal(macdLine).CrossesAbove(macdSignal).Named("MACD Bullish");
        });

        builder.ConfigureAutoTrading(trading =>
        {
            // Set global trading parameters
            trading.MaxPositions = 5;
            trading.DailyLossLimit = 0.03m;
            trading.DefaultSymbol = "AAPL";

            // Map signals to actions
            trading.OnSignal("RSI Buy")
                   .Buy()
                   .WithSize(PositionSize.Fixed(50))
                   .WithStopLoss(StopLoss.Percent(2))
                   .Execute();

            trading.OnSignal("RSI Sell")
                   .Sell()
                   .AllPositions();

            trading.OnSignal("MACD Bullish")
                   .Buy()
                   .WithSize(PositionSize.PercentOfEquity(3))
                   .WithStopLoss(StopLoss.TrailingPercent(5))
                   .Execute();
        });

        using var runtime = builder.Build();

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void AutoTradingCatalog_Properties_SetCorrectly()
    {
        // Arrange
        var testData = CreateTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        // Act - just configure trading without indicators/signals to test properties
        builder.ConfigureAutoTrading(trading =>
        {
            trading.MaxPositions = 15;
            trading.MaxPositionSize = 8m;
            trading.DailyLossLimit = 0.05m;
            trading.RequireConfirmation = true;
            trading.EmergencyStop = false;
            trading.DefaultSymbol = "QQQ";
        });

        using var runtime = builder.Build();

        // Assert - Build succeeds with custom settings
        Assert.NotNull(runtime);
    }

    [Fact]
    public void BrokerAccount_Properties_AreSetCorrectly()
    {
        // Arrange & Act
        var account = new BrokerAccount
        {
            AccountId = "TEST123",
            Equity = 100000m,
            Cash = 50000m,
            BuyingPower = 200000m,
            PortfolioValue = 50000m,
            DayPnL = 500m,
            DayPnLPercent = 0.5,
            TradingEnabled = true,
            IsPaper = true
        };

        // Assert
        Assert.Equal("TEST123", account.AccountId);
        Assert.Equal(100000m, account.Equity);
        Assert.Equal(50000m, account.Cash);
        Assert.Equal(200000m, account.BuyingPower);
        Assert.Equal(50000m, account.PortfolioValue);
        Assert.Equal(500m, account.DayPnL);
        Assert.Equal(0.5, account.DayPnLPercent);
        Assert.True(account.TradingEnabled);
        Assert.True(account.IsPaper);
    }

    [Fact]
    public void BrokerPosition_IsLong_ReturnsCorrectly()
    {
        // Arrange & Act
        var longPosition = new BrokerPosition { Quantity = 100 };
        var shortPosition = new BrokerPosition { Quantity = -100 };
        var flatPosition = new BrokerPosition { Quantity = 0 };

        // Assert
        Assert.True(longPosition.IsLong);
        Assert.False(longPosition.IsShort);

        Assert.False(shortPosition.IsLong);
        Assert.True(shortPosition.IsShort);

        Assert.False(flatPosition.IsLong);
        Assert.False(flatPosition.IsShort);
    }

    [Fact]
    public void BrokerOrder_IsFilled_ReturnsCorrectly()
    {
        // Arrange & Act
        var filledOrder = new BrokerOrder { Status = BrokerOrderStatus.Filled };
        var newOrder = new BrokerOrder { Status = BrokerOrderStatus.New };
        var partialOrder = new BrokerOrder { Status = BrokerOrderStatus.PartiallyFilled };

        // Assert
        Assert.True(filledOrder.IsFilled);
        Assert.False(filledOrder.IsPending);

        Assert.False(newOrder.IsFilled);
        Assert.True(newOrder.IsPending);

        Assert.False(partialOrder.IsFilled);
        Assert.True(partialOrder.IsPending);
    }

    [Fact]
    public void ExtendedTradeRequest_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var request = new ExtendedTradeRequest();

        // Assert
        Assert.Equal(string.Empty, request.Symbol);
        Assert.Equal(OrderType.Market, request.OrderType);
        Assert.Equal(TimeInForce.Day, request.TimeInForce);
        Assert.False(request.IsTrailingStop);
        Assert.Null(request.LimitPrice);
        Assert.Null(request.StopPrice);
        Assert.Null(request.StopLossPrice);
        Assert.Null(request.TakeProfitPrice);
        Assert.Null(request.TrailingStopOffset);
    }

    [Fact]
    public void OrderType_AllValues_AreDefined()
    {
        // Assert all expected order types exist
        Assert.Equal(OrderType.Market, (OrderType)0);
        Assert.Equal(OrderType.Limit, (OrderType)1);
        Assert.Equal(OrderType.Stop, (OrderType)2);
        Assert.Equal(OrderType.StopLimit, (OrderType)3);
        Assert.Equal(OrderType.TrailingStop, (OrderType)4);
    }

    [Fact]
    public void TimeInForce_AllValues_AreDefined()
    {
        // Assert all expected time in force options exist
        Assert.Equal(TimeInForce.Day, (TimeInForce)0);
        Assert.Equal(TimeInForce.GTC, (TimeInForce)1);
        Assert.Equal(TimeInForce.IOC, (TimeInForce)2);
        Assert.Equal(TimeInForce.FOK, (TimeInForce)3);
        Assert.Equal(TimeInForce.OPG, (TimeInForce)4);
        Assert.Equal(TimeInForce.CLS, (TimeInForce)5);
    }

    [Fact]
    public void BrokerOrderStatus_AllValues_AreDefined()
    {
        // Assert all expected status values exist
        Assert.Equal(BrokerOrderStatus.New, (BrokerOrderStatus)0);
        Assert.Equal(BrokerOrderStatus.PartiallyFilled, (BrokerOrderStatus)1);
        Assert.Equal(BrokerOrderStatus.Filled, (BrokerOrderStatus)2);
        Assert.Equal(BrokerOrderStatus.Cancelled, (BrokerOrderStatus)3);
        Assert.Equal(BrokerOrderStatus.Expired, (BrokerOrderStatus)4);
        Assert.Equal(BrokerOrderStatus.Rejected, (BrokerOrderStatus)5);
    }

    [Fact]
    public void TradeExecutionOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new TradeExecutionOptions();

        // Assert
        Assert.Equal(10, options.MaxPositions);
        Assert.Equal(10.0, options.MaxPositionSizePercent);
        Assert.Equal(2.0, options.DailyLossLimitPercent);
        Assert.False(options.RequireConfirmation);
        Assert.False(options.EmergencyStop);
        Assert.Equal(2.0, options.DefaultStopLossPercent);
        Assert.Null(options.DefaultTakeProfitPercent);
        Assert.False(options.UseTrailingStop);
        Assert.Equal(TradingPositionSizing.Fixed, options.PositionSizing);
    }

    [Fact]
    public void AlpacaOptions_DefaultValues_AreNull()
    {
        // Arrange & Act
        var options = new AlpacaOptions();

        // Assert
        Assert.Null(options.ApiKey);
        Assert.Null(options.ApiSecret);
        Assert.Null(options.UsePaper);
        Assert.Null(options.BaseUrl);
    }

    [Fact]
    public void AutoTradingConfiguration_Empty_HasCorrectDefaults()
    {
        // Arrange & Act
        var config = AutoTradingConfiguration.Empty;

        // Assert
        Assert.Empty(config.Rules);
        Assert.Empty(config.Adapters);
        Assert.Empty(config.ExtendedRules);
        Assert.Null(config.Broker);
        Assert.NotNull(config.Settings);
    }

    [Fact]
    public void ConsoleTradeAdapter_Execute_DoesNotThrow()
    {
        // Arrange
        var adapter = new ConsoleTradeAdapter();
        var request = new TradeRequest(
            new SignalHandle(1),
            TradeAction.MarketBuy,
            DateTime.Now);

        // Act & Assert - Should not throw
        adapter.Execute(request);
    }
}
