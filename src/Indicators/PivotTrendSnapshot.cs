using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Direction between successive unequal confirmed pivots.</summary>
public enum PivotTrendDirection
{
    /// <summary>Higher high.</summary>
    HigherHigh,

    /// <summary>Lower high.</summary>
    LowerHigh,

    /// <summary>Higher low.</summary>
    HigherLow,

    /// <summary>Lower low.</summary>
    LowerLow,
}

/// <summary>Confirmed pivots and retrospective lines/trends; every field has independent presence.</summary>
public sealed record PivotTrendValue(
    double? HighPoint,
    double? LowPoint,
    double? HighLine,
    double? LowLine,
    PivotTrendDirection? HighTrend,
    PivotTrendDirection? LowTrend
);

/// <summary>Connects unequal Williams fractals within the maximum trend distance.</summary>
/// <remarks>Reuses strict fractals, preserving future-wing confirmation. Lines repaint
/// between successive qualifying pivots, including endpoints; trends begin one bar
/// after the earlier pivot. Equal pivots reset the anchor without painting a line.
/// Complete interpolation rounds once and cannot overflow between finite endpoints.</remarks>
public static class PivotTrendSnapshot
{
    /// <summary>Calculates retrospective pivot trends with spans at least two and a maximum distance greater than the left span.</summary>
    public static IReadOnlyList<PivotTrendValue> Calculate(
        IReadOnlyList<Bar> bars,
        int leftSpan = 2,
        int rightSpan = 2,
        int maxTrendPeriods = 20,
        bool useClose = false
    )
    {
        if (maxTrendPeriods <= leftSpan)
            throw new ArgumentOutOfRangeException(nameof(maxTrendPeriods));
        var points = FractalSnapshot.Calculate(bars, leftSpan, rightSpan, useClose);
        var result = points
            .Select(p => new PivotTrendValue(p.Bear, p.Bull, null, null, null, null))
            .ToArray();
        for (var side = 0; side < 2; side++)
        {
            int? previous = null;
            for (var i = 0; i < result.Length; i++)
            {
                var current = side == 0 ? points[i].Bear : points[i].Bull;
                if (previous.HasValue && (long)i - previous.Value > maxTrendPeriods)
                    previous = null;
                if (!current.HasValue)
                    continue;
                if (previous.HasValue)
                {
                    var start = previous.Value;
                    var old = (side == 0 ? points[start].Bear : points[start].Bull)!.Value;
                    if (!current.Value.Equals(old)) // NOSONAR: S1244 - Only exactly equal pivots reset the anchor without a trend.
                    {
                        var trend =
                            side == 0
                                ? current.Value > old
                                    ? PivotTrendDirection.HigherHigh
                                    : PivotTrendDirection.LowerHigh
                                : current.Value > old
                                    ? PivotTrendDirection.HigherLow
                                    : PivotTrendDirection.LowerLow;
                        for (var j = start; j <= i; j++)
                        {
                            var sum = new ExactMeanAccumulator();
                            sum.Add(old, i - j);
                            sum.Add(current.Value, j - start);
                            var value = sum.Mean(i - start);
                            result[j] =
                                side == 0
                                    ? result[j] with
                                    {
                                        HighLine = value,
                                        HighTrend = j == start ? result[j].HighTrend : trend,
                                    }
                                    : result[j] with
                                    {
                                        LowLine = value,
                                        LowTrend = j == start ? result[j].LowTrend : trend,
                                    };
                        }
                    }
                }
                previous = i;
            }
        }
        return result;
    }
}
