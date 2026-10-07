using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Retrospective volatility stop, its active band, and reversal flag.</summary>
public sealed record VolatilityStopValue(
    double? Sar,
    double? UpperBand,
    double? LowerBand,
    bool? IsStop
);

/// <summary>Volatility stop using prior-bar seeded Wilder ATR and favorable close extremes.</summary>
/// <remarks>The initial direction is guessed from the seed window. If a reversal occurs,
/// all rows through that first reversal are removed; otherwise the initial segment remains.
/// Appending data can therefore erase earlier outputs. This is a snapshot in supplied order,
/// not a causal streaming indicator. Rounded wide ATR/width/stop stages preserve exact
/// comparisons; only retained published stops must fit binary64.</remarks>
public static class VolatilityStopSnapshot
{
    /// <summary>Calculates a snapshot with period at least two and a finite positive multiplier.</summary>
    public static IReadOnlyList<VolatilityStopValue> Calculate(
        IReadOnlyList<Bar> bars,
        int period = 7,
        double multiplier = 3
    )
    {
        ArgumentNullException.ThrowIfNull(bars);
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!double.IsFinite(multiplier) || multiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        foreach (var bar in bars)
            if (
                !double.IsFinite(bar.High)
                || !double.IsFinite(bar.Low)
                || !double.IsFinite(bar.Close)
            )
                throw new ArgumentOutOfRangeException(nameof(bars));
        if (bars.Count == 0)
            return Array.Empty<VolatilityStopValue>();
        var stops = new BigInteger?[bars.Count];
        var longSide = new bool[bars.Count];
        var reversals = new bool?[bars.Count];
        var initial = Math.Min(period, bars.Count);
        var bullish = bars[initial - 1].Close > bars[0].Close;
        var extreme = ExactVarianceWindow.Units(bars[0].Close);
        var atr = new SeededAtrWindow(period);
        for (var i = 0; i < initial; i++)
        {
            var price = ExactVarianceWindow.Units(bars[i].Close);
            extreme = bullish ? BigInteger.Max(extreme, price) : BigInteger.Min(extreme, price);
            atr.Add(bars[i]);
        }
        var firstStop = -1;
        for (var i = period; i < bars.Count; i++)
        {
            var price = ExactVarianceWindow.Units(bars[i].Close);
            longSide[i] = bullish;
            if (atr.HasAverage)
            {
                var average =
                    ExactVarianceWindow.Units(atr.Average.Mantissa) << atr.Average.UpperShift;
                var width = RocBankValue.RoundUnits(
                    average * ExactVarianceWindow.Units(multiplier),
                    BigInteger.One << 1074
                );
                stops[i] = RocBankValue.RoundUnits(bullish ? extreme - width : extreme + width, 1);
            }
            var reverse =
                stops[i].HasValue && (bullish ? price < stops[i]!.Value : price > stops[i]!.Value);
            reversals[i] = reverse;
            if (reverse)
            {
                if (firstStop < 0)
                    firstStop = i;
                extreme = price;
                bullish = !bullish;
            }
            else
                extreme = bullish ? BigInteger.Max(extreme, price) : BigInteger.Min(extreme, price);
            atr.Add(bars[i]);
        }
        return Enumerable
            .Range(0, bars.Count)
            .Select(i =>
            {
                if (i <= firstStop)
                    return new VolatilityStopValue(null, null, null, null);
                double? value = stops[i].HasValue
                    ? ExactMeanAccumulator.UnitRatio(stops[i]!.Value, 1)
                    : null;
                if (value.HasValue && !double.IsFinite(value.Value))
                    throw new OverflowException("Volatility stop is not representable.");
                return new VolatilityStopValue(
                    value,
                    longSide[i] ? null : value,
                    longSide[i] ? value : null,
                    reversals[i]
                );
            })
            .ToArray();
    }
}
