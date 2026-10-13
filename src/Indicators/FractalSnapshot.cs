using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Strict local high and low values; either may be absent independently.</summary>
public sealed record FractalValue(double? Bear, double? Bull);

/// <summary>Last confirmed upper and lower fractal prices.</summary>
public sealed record FractalChaosValue(double? Upper, double? Lower);

/// <summary>Retrospective strict Williams fractals and their delayed chaos bands.</summary>
/// <remarks>A fractal is present only when its selected price is strictly beyond
/// every price in both wings. Equal prices invalidate it. Fractals are placed on
/// the center bar using rightSpan future bars; chaos bands publish only after
/// confirmation. Calculations use exact binary64 comparisons and linear time.</remarks>
public static class FractalSnapshot
{
    /// <summary>Returns one row per supplied bar, using high/low or close for both extrema.</summary>
    public static IReadOnlyList<FractalValue> Calculate(
        IReadOnlyList<Bar> bars,
        int leftSpan = 2,
        int rightSpan = 2,
        bool useClose = false
    )
    {
        Validate(bars, leftSpan, rightSpan, useClose);
        var result = Enumerable
            .Range(0, bars.Count)
            .Select(_ => new FractalValue(null, null))
            .ToArray();
        var width = (long)leftSpan + rightSpan + 1;
        if (width > bars.Count)
            return result;
        var highs = new ExtremeDeque((int)width, true);
        var lows = new ExtremeDeque((int)width, false);
        for (var end = 0; end < bars.Count; end++)
        {
            var start = (long)end - width + 1;
            highs.Add(end, useClose ? bars[end].Close : bars[end].High, start);
            lows.Add(end, useClose ? bars[end].Close : bars[end].Low, start);
            if (start < 0)
                continue;
            var center = end - rightSpan;
            result[center] = new(highs.UniqueAt(center), lows.UniqueAt(center));
        }
        return result;
    }

    private static void Validate(IReadOnlyList<Bar> bars, int leftSpan, int rightSpan, bool useClose)
    {
        if (bars is null) throw new ArgumentNullException(nameof(bars));
        if (leftSpan < 2)
            throw new ArgumentOutOfRangeException(nameof(leftSpan));
        if (rightSpan < 2)
            throw new ArgumentOutOfRangeException(nameof(rightSpan));
        foreach (var bar in bars)
            if (
                useClose
                    ? !FrameworkCompatibility.IsFinite(bar.Close)
                    : !FrameworkCompatibility.IsFinite(bar.High) || !FrameworkCompatibility.IsFinite(bar.Low)
            )
                throw new ArgumentOutOfRangeException(nameof(bars));
    }

    /// <summary>Returns symmetric high/low chaos bands, updating only when the right wing is complete.</summary>
    public static IReadOnlyList<FractalChaosValue> ChaosBands(
        IReadOnlyList<Bar> bars,
        int windowSpan = 2
    )
    {
        Validate(bars, windowSpan, windowSpan, false);
        var result = new FractalChaosValue[bars.Count];
        long width = 2L * windowSpan + 1;
        // Confirm directly at the trailing edge, avoiding a second record series.
        var highs = width <= bars.Count ? new ExtremeDeque((int)width, true) : null;
        var lows = width <= bars.Count ? new ExtremeDeque((int)width, false) : null;
        double? upper = null,
            lower = null;
        for (var i = 0; i < bars.Count; i++)
        {
            long start = i - width + 1;
            highs?.Add(i, bars[i].High, start);
            lows?.Add(i, bars[i].Low, start);
            if (start >= 0)
            {
                upper = highs!.UniqueAt(i - windowSpan) ?? upper;
                lower = lows!.UniqueAt(i - windowSpan) ?? lower;
            }
            result[i] = new(upper, lower);
        }
        return result;
    }

}
