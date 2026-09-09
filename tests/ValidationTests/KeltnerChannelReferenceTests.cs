using System.Globalization;
using OoplesFinance.StockIndicators.Models;
using Xunit;

namespace OoplesFinance.StockIndicators.Tests.ValidationTests;

/// <summary>
/// Pins Keltner Channels to the standard definition, as charting platforms compute it:
/// an EMA basis with bands at a multiple of Wilder-smoothed ATR.
/// </summary>
/// <remarks>
/// <para>
/// Issue #30 reported the upper band sitting roughly eight points above TradingView's for the same
/// input. Two things were wrong. The ATR was smoothed with the caller's <c>maType</c> (an EMA) rather
/// than Wilder's method, and - far larger - the moving average was computed <em>before</em> the ATR.
/// <c>GetMovingAverageList</c> stores its result in <c>StockData.CustomValuesList</c>, which the
/// true-range helper treats as the close series, so the ATR ended up measuring each bar's high and low
/// against the previous MOVING AVERAGE instead of the previous close.
/// </para>
/// <para>
/// The expected values below are computed independently from the definition
/// (<c>basis = EMA(close, 20)</c>, <c>atr = RMA(TrueRange, 10)</c>, <c>upper = basis + 2*atr</c>)
/// over the checked-in AAPL series, so a regression in either the ordering or the smoothing fails here.
/// </para>
/// </remarks>
public class KeltnerChannelReferenceTests
{
    private const double Tolerance = 1e-4;

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

    [Fact]
    public void KeltnerChannels_MatchTheStandardDefinitionOnTheLastBar()
    {
        var result = new StockData(Aapl()).CalculateKeltnerChannels();
        var last = result.OutputValues["MiddleBand"].Count - 1;

        // Independently computed: EMA(close,20) = 135.8312, RMA(TrueRange,10) = 4.1662.
        Assert.Equal(135.8312, result.OutputValues["MiddleBand"][last], Tolerance);
        Assert.Equal(144.1636, result.OutputValues["UpperBand"][last], Tolerance);
        Assert.Equal(127.4988, result.OutputValues["LowerBand"][last], Tolerance);
    }

    [Fact]
    public void KeltnerChannels_AtrIsMeasuredAgainstCloseNotTheMovingAverage()
    {
        var keltner = new StockData(Aapl()).CalculateKeltnerChannels();
        var atr = new StockData(Aapl())
            .CalculateAverageTrueRange(MovingAvgType.WildersSmoothingMethod, 10).CustomValuesList;
        var last = atr.Count - 1;

        // Recovering the ATR from the band width must give back a genuine ATR of the price series.
        // Before the fix this implied 9.7261 against a true ATR(10) of 4.1662, because the true range
        // was being measured against the 20-period average instead of the close.
        var impliedAtr =
            (keltner.OutputValues["UpperBand"][last] - keltner.OutputValues["MiddleBand"][last]) / 2.0;

        Assert.Equal(atr[last], impliedAtr, Tolerance);
    }

    [Fact]
    public void KeltnerChannels_AtrSmoothingIsCallerOverridable()
    {
        // The Wilder default is the standard, but the ATR smoothing stays selectable.
        var wilder = new StockData(Aapl()).CalculateKeltnerChannels();
        var ema = new StockData(Aapl()).CalculateKeltnerChannels(
            MovingAvgType.ExponentialMovingAverage, 20, 10, 2, MovingAvgType.ExponentialMovingAverage);
        var last = wilder.OutputValues["UpperBand"].Count - 1;

        // Same basis, different band width: EMA-smoothed ATR(10) is 3.9553 rather than 4.1662.
        Assert.Equal(
            wilder.OutputValues["MiddleBand"][last], ema.OutputValues["MiddleBand"][last], Tolerance);
        Assert.Equal(143.7419, ema.OutputValues["UpperBand"][last], Tolerance);
    }
}
