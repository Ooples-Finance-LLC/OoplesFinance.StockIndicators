using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Backtest;
using OoplesFinance.StockIndicators.Models;
using Xunit;

namespace OoplesFinance.StockIndicators.Tests.ValidationTests;

/// <summary>
/// Tests for the backtesting engine.
/// </summary>
public class BacktestTests
{
    private static List<TickerData> CreateTestData()
    {
        var data = new List<TickerData>();
        var baseDate = new DateTime(2024, 1, 1);
        var random = new Random(42);

        double price = 100;
        for (int i = 0; i < 100; i++)
        {
            var change = (random.NextDouble() - 0.5) * 2; // -1 to +1
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
    public void Backtest_WithDefaultOptions_ReturnsResults()
    {
        // Arrange
        var testData = CreateTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        builder.ConfigureIndicators(catalog =>
        {
            catalog.Rsi(14);
        });

        // Act
        var results = builder.Backtest();

        // Assert
        Assert.NotNull(results);
        Assert.Equal(100_000, results.InitialCapital);
        Assert.True(results.StartDate <= results.EndDate);
        Assert.NotNull(results.EquityCurve);
        Assert.NotNull(results.Trades);
    }

    [Fact]
    public void Backtest_WithCustomOptions_ReturnsResults()
    {
        // Arrange
        var testData = CreateTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        builder.ConfigureIndicators(catalog =>
        {
            catalog.Sma(20);
        });

        // Act
        var results = builder.Backtest(options =>
        {
            options.InitialCapital = 50_000;
            options.FeeModel = FeeModel.Percent;
            options.PercentFee = 0.1;
            options.SlippageModel = SlippageModel.Percent;
            options.SlippagePercent = 0.05;
            options.PositionSizing = new PositionSizingOptions
            {
                Method = PositionSizingMethod.FixedUnits,
                FixedUnits = 50
            };
            options.RiskManagement = new RiskManagementOptions
            {
                StopLossType = StopLossType.FixedPercent,
                StopLossValue = 5,
                TakeProfitType = TakeProfitType.FixedPercent,
                TakeProfitValue = 10
            };
        });

        // Assert
        Assert.NotNull(results);
        Assert.Equal(50_000, results.InitialCapital);
    }

    [Fact]
    public void BacktestResults_CalculatedProperties_AreCorrect()
    {
        // Arrange
        var results = new BacktestResults
        {
            InitialCapital = 100_000,
            FinalCapital = 120_000,
            TotalTrades = 100,
            WinningTrades = 55,
            LosingTrades = 40
        };

        // Assert
        Assert.Equal(20_000, results.NetProfit);
        Assert.Equal(20, results.TotalReturnPercent, 6); // 6 decimal precision
        Assert.Equal(55, results.WinRatePercent, 6); // 6 decimal precision
        Assert.Equal(5, results.BreakEvenTrades);
    }

    [Fact]
    public void EquityPoint_Constructor_SetsProperties()
    {
        // Arrange & Act
        var point = new EquityPoint(
            new DateTime(2024, 1, 15),
            105000,
            500,
            0.48);

        // Assert
        Assert.Equal(new DateTime(2024, 1, 15), point.Date);
        Assert.Equal(105000, point.Equity);
        Assert.Equal(500, point.Drawdown);
        Assert.Equal(0.48, point.DrawdownPercent);
    }

    [Fact]
    public void TradeRecord_NetPnL_CalculatesCorrectly()
    {
        // Arrange
        var trade = new TradeRecord
        {
            GrossPnL = 1000,
            Fees = 10,
            Slippage = 5
        };

        // Assert
        Assert.Equal(985, trade.NetPnL);
    }

    [Fact]
    public void PositionSizingOptions_StaticCreators_Work()
    {
        // Test CreateFixedUnits
        var fixedUnits = PositionSizingOptions.CreateFixedUnits(200);
        Assert.Equal(PositionSizingMethod.FixedUnits, fixedUnits.Method);
        Assert.Equal(200, fixedUnits.FixedUnits);

        // Test FixedDollar
        var fixedDollar = PositionSizingOptions.FixedDollar(5000);
        Assert.Equal(PositionSizingMethod.FixedDollar, fixedDollar.Method);
        Assert.Equal(5000, fixedDollar.FixedDollarAmount);

        // Test PercentOfEquity
        var percentEquity = PositionSizingOptions.PercentOfEquity(15);
        Assert.Equal(PositionSizingMethod.PercentOfEquity, percentEquity.Method);
        Assert.Equal(15, percentEquity.EquityPercent);

        // Test RiskPercent
        var riskPercent = PositionSizingOptions.RiskPercent(2);
        Assert.Equal(PositionSizingMethod.RiskPercent, riskPercent.Method);
        Assert.Equal(2, riskPercent.EquityPercent);
    }

    [Fact]
    public void RiskManagementOptions_StaticCreators_Work()
    {
        // Test FixedStopLoss
        var fixedStop = RiskManagementOptions.FixedStopLoss(3);
        Assert.Equal(StopLossType.FixedPercent, fixedStop.StopLossType);
        Assert.Equal(3, fixedStop.StopLossValue);

        // Test TrailingStop
        var trailingStop = RiskManagementOptions.TrailingStop(5);
        Assert.Equal(StopLossType.TrailingPercent, trailingStop.StopLossType);
        Assert.Equal(5, trailingStop.StopLossValue);

        // Test RiskReward
        var riskReward = RiskManagementOptions.RiskReward(2, 3);
        Assert.Equal(StopLossType.FixedPercent, riskReward.StopLossType);
        Assert.Equal(2, riskReward.StopLossValue);
        Assert.Equal(TakeProfitType.RiskRewardRatio, riskReward.TakeProfitType);
        Assert.Equal(3, riskReward.TakeProfitValue);
    }

    [Fact]
    public void BacktestResults_ToString_FormatsCorrectly()
    {
        // Arrange
        var results = new BacktestResults
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            InitialCapital = 100_000,
            FinalCapital = 125_000,
            TotalTrades = 50,
            WinningTrades = 30,
            LosingTrades = 20,
            ProfitFactor = 1.8,
            MaxDrawdownPercent = 12.5,
            SharpeRatio = 1.5,
            SortinoRatio = 2.1
        };

        // Act
        var str = results.ToString();

        // Assert
        Assert.Contains("Backtest Results:", str);
        Assert.Contains("Net Profit:", str);
        Assert.Contains("Total Trades: 50", str);
        Assert.Contains("Win Rate: 60.0%", str);
        Assert.Contains("Profit Factor: 1.80", str);
        Assert.Contains("Max Drawdown: 12.50%", str);
        Assert.Contains("Sharpe Ratio: 1.50", str);
        Assert.Contains("Sortino Ratio: 2.10", str);
    }
}
