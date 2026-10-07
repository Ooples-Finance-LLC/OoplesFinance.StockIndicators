using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>One row of a retrospective regression channel.</summary>
public sealed record RegressionChannelValue(
    double? Centerline,
    double? UpperChannel,
    double? LowerChannel,
    bool BreakPoint
);

/// <summary>Nonoverlapping regression channels aligned backward from the final observation.</summary>
/// <remarks>A null period fits the complete input. Otherwise, the leading incomplete block
/// is absent. Appending input changes block boundaries and can repaint every channel.
/// Each center is an exact fitted position rounded once; width uses rounded population
/// deviation. Bands round once after adding/subtracting the wide width. Published overflow
/// is rejected. Input order is preserved.</remarks>
public static class RegressionChannelSnapshot
{
    /// <summary>Fits finite closes in blocks of at least two with a finite positive deviation multiplier.</summary>
    public static IReadOnlyList<RegressionChannelValue> Calculate(
        IReadOnlyList<Bar> bars,
        int? period = null,
        double deviations = 2
    )
    {
        if (bars is null) throw new ArgumentNullException(nameof(bars));
        var length = period ?? bars.Count;
        if (length < 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!FrameworkCompatibility.IsFinite(deviations) || deviations <= 0)
            throw new ArgumentOutOfRangeException(nameof(deviations));
        foreach (var bar in bars)
            if (!FrameworkCompatibility.IsFinite(bar.Close))
                throw new ArgumentOutOfRangeException(nameof(bars));
        var result = Enumerable
            .Range(0, bars.Count)
            .Select(_ => new RegressionChannelValue(null, null, null, false))
            .ToArray();
        if (bars.Count < length)
            return result;
        using var window = new WindowRegressionStatistics.State(length);
        Span<double> values = stackalloc double[10];
        for (var start = bars.Count - length; start >= 0; start -= length)
        {
            window.Reset();
            for (var i = start; i < start + length; i++)
                window.Update(bars[i], values);
            var width = RocBankValue.RoundUnits(
                ExactVarianceWindow.Units(values[2]) * ExactVarianceWindow.Units(deviations),
                System.Numerics.BigInteger.One << 1074
            );
            for (var j = 0; j < length; j++)
            {
                var center = window.LineAt(j);
                if (!FrameworkCompatibility.IsFinite(center))
                    throw new OverflowException("Regression center is not representable.");
                var units = ExactVarianceWindow.Units(center);
                var upper = ExactMeanAccumulator.UnitRatio(units + width, 1);
                var lower = ExactMeanAccumulator.UnitRatio(units - width, 1);
                if (!FrameworkCompatibility.IsFinite(upper) || !FrameworkCompatibility.IsFinite(lower))
                    throw new OverflowException("Regression channel is not representable.");
                result[start + j] = new(center, upper, lower, j == 0);
            }
        }
        return result;
    }
}
