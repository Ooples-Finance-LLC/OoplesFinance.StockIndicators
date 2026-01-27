using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Catalogs;
using OoplesFinance.StockIndicators.Builder.Notifications;
using OoplesFinance.StockIndicators.Builder.Signals;
using OoplesFinance.StockIndicators.Builder.Trading;
using OoplesFinance.StockIndicators.Models;
using Xunit;

namespace OoplesFinance.StockIndicators.Tests.ValidationTests;

/// <summary>
/// End-to-end integration tests demonstrating all V2 features working together (Phase 11).
/// </summary>
public class EndToEndIntegrationTests
{
    private static List<TickerData> CreateRealisticTestData(int bars = 252)
    {
        var data = new List<TickerData>();
        var baseDate = new DateTime(2024, 1, 1);
        var random = new Random(42); // Deterministic for reproducibility

        // Simulate realistic price movement with trend and volatility
        double price = 100;
        double trend = 0.0002; // Slight upward trend
        double volatility = 0.02; // 2% daily volatility

        for (int i = 0; i < bars; i++)
        {
            var dailyReturn = trend + (random.NextDouble() - 0.5) * 2 * volatility;
            price *= (1 + dailyReturn);
            price = Math.Max(50, Math.Min(200, price)); // Keep within bounds

            var dayVolatility = 0.5 + random.NextDouble() * 1.5; // 0.5-2% daily range
            var high = price * (1 + dayVolatility / 100);
            var low = price * (1 - dayVolatility / 100);
            var open = low + random.NextDouble() * (high - low);
            var close = low + random.NextDouble() * (high - low);

            data.Add(new TickerData
            {
                Date = baseDate.AddDays(i),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = 1000000 + random.Next(2000000)
            });
        }

        return data;
    }

    [Fact]
    public void FullWorkflow_IndicatorsOnly_BuildsSuccessfully()
    {
        // Arrange
        var testData = CreateRealisticTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        // Act - Configure only indicators
        SeriesHandle rsi = default;
        SeriesHandle macdLine = default;
        SeriesHandle macdSignal = default;
        SeriesHandle atr = default;
        SeriesHandle sma = default;
        SeriesHandle ema = default;

        builder.ConfigureIndicators(catalog =>
        {
            rsi = catalog.Rsi(14);
            var macd = catalog.Macd(12, 26, 9);
            macdLine = macd.Primary;
            macdSignal = macd.Signal;
            atr = catalog.Atr(14);
            sma = catalog.Sma(20);
            ema = catalog.Ema(9);
        });

        using var runtime = builder.Build();

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void FullWorkflow_IndicatorsAndSignals_BuildsSuccessfully()
    {
        // Arrange
        var testData = CreateRealisticTestData();
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
            // Simple threshold signals
            signals.When(rsi).CrossesAbove(30).Emit("RSI Oversold Exit");
            signals.When(rsi).CrossesBelow(70).Emit("RSI Overbought Exit");

            // Compound signal with series-to-series comparison
            signals.Signal(rsi).IsBelow(30)
                   .And(macdLine).CrossesAbove(macdSignal)
                   .Named("Strong Buy Signal");
        });

        using var runtime = builder.Build();

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void FullWorkflow_SignalsAndNotifications_BuildsSuccessfully()
    {
        // Arrange
        var testData = CreateRealisticTestData();
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
            signals.When(rsi).CrossesBelow(70).Emit("Sell Signal");
        });

        builder.ConfigureNotifications(notify =>
        {
            // Console output for all signals
            notify.Console();

            // Route specific signals
            notify.OnSignal("Buy Signal")
                  .SendConsole()
                  .SendWebhook("https://example.com/webhook/buy");

            notify.OnSignal("Sell Signal")
                  .SendConsole()
                  .SendWebhook("https://example.com/webhook/sell");

            // Wildcard pattern for all signals
            notify.OnSignal("*")
                  .SendWebSocket("ws://localhost:8080/signals");
        });

        using var runtime = builder.Build();

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void FullWorkflow_SignalsAndAutoTrading_BuildsSuccessfully()
    {
        // Arrange
        var testData = CreateRealisticTestData();
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
            signals.Signal(macdLine).CrossesAbove(macdSignal).Named("MACD Bullish");
        });

        builder.ConfigureAutoTrading(trading =>
        {
            // Global settings
            trading.MaxPositions = 5;
            trading.MaxPositionSize = 10m;
            trading.DailyLossLimit = 0.02m;
            trading.DefaultSymbol = "AAPL";

            // Map signals to trading actions
            trading.OnSignal("RSI Buy")
                   .Buy()
                   .WithSize(PositionSize.Fixed(100))
                   .WithStopLoss(StopLoss.Percent(2))
                   .Execute();

            trading.OnSignal("MACD Bullish")
                   .Buy()
                   .WithSize(PositionSize.PercentOfEquity(5))
                   .WithStopLoss(StopLoss.TrailingPercent(3))
                   .WithTakeProfit(TakeProfit.Percent(10))
                   .Execute();

            trading.OnSignal("RSI Sell")
                   .Sell()
                   .AllPositions();
        });

        using var runtime = builder.Build();

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void FullWorkflow_CompleteIntegration_AllFeatures()
    {
        // Arrange - This is the full workflow from the plan
        var testData = CreateRealisticTestData(365); // One year of data
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        SeriesHandle rsi = default;
        SeriesHandle macdLine = default;
        SeriesHandle macdSignal = default;
        SeriesHandle atr = default;
        SeriesHandle sma = default;

        // Act - Step 1: Configure Indicators
        builder.ConfigureIndicators(catalog =>
        {
            rsi = catalog.Rsi(14);
            var macd = catalog.Macd(12, 26, 9);
            macdLine = macd.Primary;
            macdSignal = macd.Signal;
            atr = catalog.Atr(14);
            sma = catalog.Sma(50);
        });

        // Step 2: Configure Signals
        builder.ConfigureSignals(signals =>
        {
            // Simple signals
            signals.When(rsi).CrossesAbove(30).Emit("RSI Oversold Exit");
            signals.When(rsi).CrossesBelow(70).Emit("RSI Overbought Exit");

            // Compound signals using extended builder
            signals.Signal(rsi).IsBelow(30)
                   .And(macdLine).CrossesAbove(macdSignal)
                   .Named("Strong Buy Signal");

            // MACD crossover
            signals.Signal(macdLine).CrossesAbove(macdSignal).Named("MACD Buy");
            signals.Signal(macdLine).CrossesBelow(macdSignal).Named("MACD Sell");
        });

        // Step 3: Configure Notifications
        builder.ConfigureNotifications(notify =>
        {
            notify.Console();

            // Route buy signals
            notify.OnSignal("Strong Buy Signal")
                  .SendConsole()
                  .SendWebhook("https://example.com/strong-buy");

            notify.OnSignal("MACD*")
                  .SendWebSocket("ws://localhost:8080/macd-signals");
        });

        // Step 4: Configure Backtesting
        builder.ConfigureBacktesting(new BacktestOptions
        {
            InitialCapital = 100000,
            SlippageModel = SlippageModel.Percent,
            SlippagePercent = 0.05
        });

        // Step 5: Configure Auto Trading
        builder.ConfigureAutoTrading(trading =>
        {
            trading.MaxPositions = 10;
            trading.MaxPositionSize = 5m;
            trading.DailyLossLimit = 0.02m;
            trading.RequireConfirmation = false;

            // Buy on strong signals
            trading.OnSignal("Strong Buy Signal")
                   .Buy()
                   .WithSize(PositionSize.Fixed(100))
                   .WithStopLoss(StopLoss.Percent(2))
                   .WithTakeProfit(TakeProfit.Percent(6))
                   .Execute();

            // Sell on overbought
            trading.OnSignal("RSI Overbought Exit")
                   .Sell()
                   .AllPositions();
        });

        // Step 6: Build Runtime
        using var runtime = builder.Build();

        // Assert - Full integration builds successfully
        Assert.NotNull(runtime);
    }

    [Fact]
    public void FullWorkflow_MultipleIndicatorTypes_BuildsSuccessfully()
    {
        // Test that many different indicator types work together
        var testData = CreateRealisticTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        // Act - Configure a variety of indicators
        builder.ConfigureIndicators(catalog =>
        {
            // Moving averages
            catalog.Sma(20);
            catalog.Ema(12);
            catalog.Wma(14);
            catalog.Hma(9);

            // Oscillators
            catalog.Rsi(14);
            catalog.Stochastic(14, 3);
            catalog.Cci(20);
            catalog.WilliamsR(14);

            // Trend indicators
            catalog.Macd(12, 26, 9);
            catalog.Adx(14);

            // Volatility
            catalog.Atr(14);
            catalog.BollingerBands(20, 2);

            // Volume
            catalog.Obv();
        });

        using var runtime = builder.Build();

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void FullWorkflow_CompoundSignals_AllOperators()
    {
        // Test all compound signal operators
        var testData = CreateRealisticTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        SeriesHandle rsi = default;
        SeriesHandle sma = default;
        SeriesHandle ema = default;

        builder.ConfigureIndicators(catalog =>
        {
            rsi = catalog.Rsi(14);
            sma = catalog.Sma(20);
            ema = catalog.Ema(9);
        });

        builder.ConfigureSignals(signals =>
        {
            // IsAbove + And + Above (chained compound signals)
            signals.Signal(rsi).IsAbove(50)
                   .And(sma).Above(100)
                   .Named("Bull Market");

            // IsBelow + And
            signals.Signal(rsi).IsBelow(50)
                   .AndAbove(sma, 100)
                   .Named("Bearish RSI Bullish Price");

            // CrossesAbove with threshold
            signals.Signal(rsi).CrossesAbove(30).Named("RSI Recovery");

            // CrossesAbove with series
            signals.Signal(ema).CrossesAbove(sma).Named("Golden Cross");

            // CrossesBelow with threshold
            signals.Signal(rsi).CrossesBelow(70).Named("RSI Peak");

            // OrCrossesAbove
            signals.Signal(rsi).IsBelow(30)
                   .OrCrossesAbove(sma, 100)
                   .Named("Entry Conditions");

            // Group with All()
            signals.Group(
                SignalCondition.Above(rsi, 50),
                SignalCondition.CrossesAbove(sma, 100)
            ).All().Emit("Bullish Confluence");

            // Group with Any()
            signals.Group(
                SignalCondition.Below(rsi, 30),
                SignalCondition.Below(sma, 90)
            ).Any().Emit("Potential Bottom");
        });

        using var runtime = builder.Build();

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void FullWorkflow_TradingRules_AllPositionSizes()
    {
        // Test all position sizing methods
        var testData = CreateRealisticTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        SeriesHandle rsi = default;

        builder.ConfigureIndicators(catalog =>
        {
            rsi = catalog.Rsi(14);
        });

        builder.ConfigureSignals(signals =>
        {
            signals.When(rsi).CrossesAbove(30).Emit("Signal1");
            signals.When(rsi).CrossesBelow(70).Emit("Signal2");
            signals.When(rsi).Above(50).Emit("Signal3");
            signals.When(rsi).Below(50).Emit("Signal4");
        });

        builder.ConfigureAutoTrading(trading =>
        {
            // Fixed size
            trading.OnSignal("Signal1")
                   .Buy()
                   .WithSize(PositionSize.Fixed(100))
                   .Execute();

            // Percent of equity
            trading.OnSignal("Signal2")
                   .Buy()
                   .WithSize(PositionSize.PercentOfEquity(5))
                   .Execute();

            // Risk-based
            trading.OnSignal("Signal3")
                   .Buy()
                   .WithSize(PositionSize.RiskBased(500))
                   .WithStopLoss(StopLoss.Percent(2))
                   .Execute();

            // All positions close
            trading.OnSignal("Signal4")
                   .Sell()
                   .AllPositions();
        });

        using var runtime = builder.Build();

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void FullWorkflow_TradingRules_AllStopLossTypes()
    {
        // Test all stop loss types
        var testData = CreateRealisticTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        SeriesHandle rsi = default;

        builder.ConfigureIndicators(catalog =>
        {
            rsi = catalog.Rsi(14);
        });

        builder.ConfigureSignals(signals =>
        {
            signals.When(rsi).CrossesAbove(20).Emit("StopLossPercent");
            signals.When(rsi).CrossesAbove(25).Emit("StopLossDollars");
            signals.When(rsi).CrossesAbove(30).Emit("TrailingPercent");
            signals.When(rsi).CrossesAbove(35).Emit("TrailingDollars");
        });

        builder.ConfigureAutoTrading(trading =>
        {
            // Percent stop loss
            trading.OnSignal("StopLossPercent")
                   .Buy()
                   .WithSize(PositionSize.Fixed(100))
                   .WithStopLoss(StopLoss.Percent(2))
                   .Execute();

            // Dollar stop loss
            trading.OnSignal("StopLossDollars")
                   .Buy()
                   .WithSize(PositionSize.Fixed(100))
                   .WithStopLoss(StopLoss.Dollars(50))
                   .Execute();

            // Trailing percent
            trading.OnSignal("TrailingPercent")
                   .Buy()
                   .WithSize(PositionSize.Fixed(100))
                   .WithStopLoss(StopLoss.TrailingPercent(3))
                   .Execute();

            // Trailing dollars
            trading.OnSignal("TrailingDollars")
                   .Buy()
                   .WithSize(PositionSize.Fixed(100))
                   .WithStopLoss(StopLoss.TrailingDollars(25))
                   .Execute();
        });

        using var runtime = builder.Build();

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void FullWorkflow_NotificationRouting_AllPatterns()
    {
        // Test all notification routing patterns
        var testData = CreateRealisticTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        SeriesHandle rsi = default;

        builder.ConfigureIndicators(catalog =>
        {
            rsi = catalog.Rsi(14);
        });

        builder.ConfigureSignals(signals =>
        {
            signals.When(rsi).CrossesAbove(30).Emit("RSI Buy Signal");
            signals.When(rsi).CrossesBelow(70).Emit("RSI Sell Signal");
            signals.When(rsi).Above(80).Emit("Extreme Overbought");
        });

        builder.ConfigureNotifications(notify =>
        {
            // Global console
            notify.Console();

            // Exact match
            notify.OnSignal("RSI Buy Signal")
                  .SendConsole()
                  .SendWebhook("https://example.com/buy");

            // Wildcard prefix
            notify.OnSignal("RSI*")
                  .SendWebSocket("ws://localhost:8080/rsi");

            // Star wildcard (all signals)
            notify.OnSignal("*")
                  .SendWebhook("https://example.com/all-signals");
        });

        using var runtime = builder.Build();

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void FullWorkflow_BacktestOptions_AllSettings()
    {
        // Test all backtest configuration options
        var testData = CreateRealisticTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        builder.ConfigureIndicators(catalog =>
        {
            catalog.Rsi(14);
        });

        builder.ConfigureBacktesting(new BacktestOptions
        {
            InitialCapital = 50000,
            SlippageModel = SlippageModel.Percent,
            SlippagePercent = 0.05,
            WarmupBars = 30
        });

        using var runtime = builder.Build();

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void FullWorkflow_EmptyConfiguration_BuildsWithDefaults()
    {
        // Test that empty configuration still works
        var testData = CreateRealisticTestData(50);
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        // Act - No configuration at all
        using var runtime = builder.Build();

        // Assert - Should build with defaults
        Assert.NotNull(runtime);
    }

    [Fact]
    public void FullWorkflow_MinimalData_HandlesEdgeCases()
    {
        // Test with minimal data
        var testData = CreateRealisticTestData(30); // Just enough for warmup
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        SeriesHandle rsi = default;

        builder.ConfigureIndicators(catalog =>
        {
            rsi = catalog.Rsi(14);
        });

        builder.ConfigureSignals(signals =>
        {
            signals.When(rsi).CrossesAbove(30).Emit("Buy");
        });

        using var runtime = builder.Build();

        // Assert
        Assert.NotNull(runtime);
    }
}
