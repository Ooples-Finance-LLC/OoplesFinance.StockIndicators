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
        var result = Enumerable
            .Range(0, bars.Count)
            .Select(_ => new FractalValue(null, null))
            .ToArray();
        var width = (long)leftSpan + rightSpan + 1;
        if (width > bars.Count)
            return result;
        var highs = new Extreme(true);
        var lows = new Extreme(false);
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

    /// <summary>Returns symmetric high/low chaos bands, updating only when the right wing is complete.</summary>
    public static IReadOnlyList<FractalChaosValue> ChaosBands(
        IReadOnlyList<Bar> bars,
        int windowSpan = 2
    )
    {
        var fractals = Calculate(bars, windowSpan, windowSpan);
        var result = new FractalChaosValue[bars.Count];
        double? upper = null,
            lower = null;
        for (var i = 0; i < bars.Count; i++)
        {
            if ((long)i >= 2L * windowSpan)
            {
                upper = fractals[i - windowSpan].Bear ?? upper;
                lower = fractals[i - windowSpan].Bull ?? lower;
            }
            result[i] = new(upper, lower);
        }
        return result;
    }

    private sealed class Extreme(bool maximum)
    {
        private readonly LinkedList<(int Index, double Value)> _candidates = new();

        internal void Add(int index, double value, long first)
        {
            while (_candidates.First is { } head && head.Value.Index < first)
                _candidates.RemoveFirst();
            while (
                _candidates.Last is { } tail
                && (maximum ? tail.Value.Value < value : tail.Value.Value > value)
            )
                _candidates.RemoveLast();
            _candidates.AddLast((index, value));
        }

        internal double? UniqueAt(int index)
        {
            var head = _candidates.First;
            if (head is null || head.Value.Index != index)
                return null;
            return head.Next is { } next && next.Value.Value == head.Value.Value // NOSONAR: Exact ties invalidate strict extrema; adjacent prices must remain distinct.
                ? null
                : head.Value.Value;
        }
    }
}
