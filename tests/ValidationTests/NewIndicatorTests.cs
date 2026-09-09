using System.Globalization;
using OoplesFinance.StockIndicators.Models;
using Xunit;

namespace OoplesFinance.StockIndicators.Tests.ValidationTests;

/// <summary>
/// Behavioural tests for the two indicators requested in issues #45 and #46.
/// </summary>
public class NewIndicatorTests
{
    private const double Tolerance = 1e-9;

    private static List<TickerData> Aapl()
    {
        var list = new List<TickerData>();
        foreach (var line in File.ReadAllLines("TestData/AAPL.csv").Skip(1))
        {
            var p = line.Split(',');
            list.Add(new TickerData
            {
                Date = DateTime.ParseExact(p[0], "yyyyMMdd", CultureInfo.InvariantCulture),
                Open = double.Parse(p[1], CultureInfo.InvariantCulture),
                High = double.Parse(p[2], CultureInfo.InvariantCulture),
                Low = double.Parse(p[3], CultureInfo.InvariantCulture),
                Close = double.Parse(p[4], CultureInfo.InvariantCulture),
                Volume = double.Parse(p[5], CultureInfo.InvariantCulture)
            });
        }
        return list;
    }

    // ---------------------------------------------------------------- issue #46

    [Fact]
    public void SchaffTrendCycleShk_IsBoundedToZeroHundredAndOnePerBar()
    {
        var bars = Aapl();
        var stc = new StockData(bars).CalculateSchaffTrendCycleShk().OutputValues["Stc"];

        Assert.Equal(bars.Count, stc.Count);
        Assert.All(stc, v =>
        {
            Assert.True(double.IsFinite(v), "STC must be finite");
            Assert.InRange(v, 0.0, 100.0);
        });
    }

    [Fact]
    public void SchaffTrendCycleShk_AppliesASecondStochasticPassUnlikeTheSinglePassVersion()
    {
        var bars = Aapl();
        var single = new StockData(bars).CalculateSchaffTrendCycle().OutputValues["Stc"];
        var shk = new StockData(bars).CalculateSchaffTrendCycleShk().OutputValues["Stc"];

        // The whole point of the SHK script is the second stochastic + smoothing pass, so the two
        // series must not be the same. If this ever passes, the extra pass has been lost.
        Assert.Equal(single.Count, shk.Count);
        Assert.Contains(Enumerable.Range(0, shk.Count), i => Math.Abs(single[i] - shk[i]) > 1e-6);
    }

    [Fact]
    public void SchaffTrendCycleShk_PublishesTheUnderlyingMacd()
    {
        var bars = Aapl();
        var result = new StockData(bars).CalculateSchaffTrendCycleShk(
            MovingAvgType.ExponentialMovingAverage, 23, 50, 10, 3, 3);

        var macd = result.OutputValues["Macd"];
        var fast = new StockData(bars).CalculateExponentialMovingAverage(23).CustomValuesList;
        var slow = new StockData(bars).CalculateExponentialMovingAverage(50).CustomValuesList;

        Assert.Equal(bars.Count, macd.Count);
        for (var i = 0; i < macd.Count; i++)
        {
            Assert.Equal(fast[i] - slow[i], macd[i], Tolerance);
        }
    }

    // ---------------------------------------------------------------- issue #45

    [Fact]
    public void UtBotAlerts_ReturnsOneValuePerBarForEveryOutput()
    {
        var bars = Aapl();
        var result = new StockData(bars).CalculateUtBotAlerts();

        foreach (var output in result.OutputValues)
        {
            Assert.Equal(bars.Count, output.Value.Count);
        }
    }

    [Fact]
    public void UtBotAlerts_StopRatchetsAndNeverGivesGroundWhilePriceHoldsItsSide()
    {
        var bars = Aapl();
        var result = new StockData(bars).CalculateUtBotAlerts();
        var stop = result.OutputValues["TrailingStop"];
        var closes = bars.Select(b => b.Close).ToList();

        for (var i = 1; i < stop.Count; i++)
        {
            var prevStop = stop[i - 1];
            if (closes[i] > prevStop && closes[i - 1] > prevStop)
            {
                // Held above: the stop may rise but must never fall.
                Assert.True(stop[i] >= prevStop,
                    $"stop fell from {prevStop} to {stop[i]} at index {i} while price stayed above it");
            }
            else if (closes[i] < prevStop && closes[i - 1] < prevStop)
            {
                // Held below: the stop may fall but must never rise.
                Assert.True(stop[i] <= prevStop,
                    $"stop rose from {prevStop} to {stop[i]} at index {i} while price stayed below it");
            }
        }
    }

    [Fact]
    public void UtBotAlerts_PositionIsAlwaysFlatLongOrShortAndFlipsOnlyWithASignal()
    {
        var bars = Aapl();
        var result = new StockData(bars).CalculateUtBotAlerts();
        var position = result.OutputValues["Position"];
        var buy = result.OutputValues["Buy"];
        var sell = result.OutputValues["Sell"];

        Assert.All(position, p => Assert.Contains(p, new[] { -1.0, 0.0, 1.0 }));
        Assert.All(buy, b => Assert.Contains(b, new[] { 0.0, 1.0 }));
        Assert.All(sell, sl => Assert.Contains(sl, new[] { 0.0, 1.0 }));

        // A bar cannot be both a buy and a sell.
        for (var i = 0; i < buy.Count; i++)
        {
            Assert.False(buy[i] == 1 && sell[i] == 1, $"bar {i} is flagged as both buy and sell");
        }

        // Both directions must actually occur on a year of real data, or the crossing logic is dead.
        Assert.Contains(1.0, buy);
        Assert.Contains(1.0, sell);
        Assert.Contains(1.0, position);
        Assert.Contains(-1.0, position);
    }

    [Fact]
    public void UtBotAlerts_StopSitsOneAtrMultipleFromPriceOnAFlip()
    {
        var bars = Aapl();
        const double keyValue = 2.0;
        var result = new StockData(bars).CalculateUtBotAlerts(
            MovingAvgType.WildersSmoothingMethod, 10, keyValue);
        var stop = result.OutputValues["TrailingStop"];
        var atr = new StockData(bars)
            .CalculateAverageTrueRange(MovingAvgType.WildersSmoothingMethod, 10).CustomValuesList;
        var closes = bars.Select(b => b.Close).ToList();

        var flips = 0;
        for (var i = 1; i < stop.Count; i++)
        {
            var prevStop = stop[i - 1];
            var heldAbove = closes[i] > prevStop && closes[i - 1] > prevStop;
            var heldBelow = closes[i] < prevStop && closes[i - 1] < prevStop;
            if (heldAbove || heldBelow)
            {
                continue;
            }

            // On a flip the stop is placed exactly keyValue * ATR away from price.
            var expected = closes[i] > prevStop
                ? closes[i] - (keyValue * atr[i])
                : closes[i] + (keyValue * atr[i]);
            Assert.Equal(expected, stop[i], 1e-9);
            flips++;
        }

        Assert.True(flips > 0, "the AAPL series should produce at least one stop flip");
    }
}
