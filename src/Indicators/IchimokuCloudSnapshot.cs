using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Five independently nullable lines of a shifted midpoint cloud.</summary>
public sealed record IchimokuCloudValue(
    double? Conversion,
    double? Base,
    double? LeadingA,
    double? LeadingB,
    double? Lagging
);

/// <summary>A cloud value at a possibly negative or projected bar index.</summary>
public sealed record IndexedIchimokuCloudValue(long Index, IchimokuCloudValue Value);

/// <summary>Retrospective Ichimoku cloud with independently configurable forward and backward shifts.</summary>
/// <remarks>Window midpoints use true high/low extrema, including negative prices.
/// Each midpoint and the mean of the two rounded midpoints rounds once.
/// Leading A additionally waits until index max(2*forwardOffset,conversionPeriod,basePeriod)-1.
/// Leading B uses the full span-B window ending forwardOffset bars earlier.
/// Lagging close uses backwardOffset future bars, so appending input can fill
/// previously absent trailing values. One output row is returned per input bar;
/// projected values outside that range are omitted. Input order is retained.</remarks>
public static class IchimokuCloudSnapshot
{
    /// <summary>Enumerates the extended cloud with forward shift basePeriod and backward shift basePeriod-1.</summary>
    /// <remarks>Default output indexes are 1-basePeriod through bars.Count-1+basePeriod,
    /// including absent rows. Explicit startIndex/endIndex select output coordinates
    /// directly. There is no extra leading-A gate beyond complete source windows.
    /// Values are captured at the call; output rows are generated lazily, allowing
    /// very large shifts without allocating arrays proportional to the shift.</remarks>
    public static IEnumerable<IndexedIchimokuCloudValue> Extended(
        IReadOnlyList<Bar> bars,
        int conversionPeriod = 9,
        int basePeriod = 26,
        int spanBPeriod = 52,
        long? startIndex = null,
        long? endIndex = null
    )
    {
        Validate(bars, conversionPeriod, basePeriod, spanBPeriod, 0, 0);
        var (conversion, basis, spanB) = CaptureMidpoints(bars, conversionPeriod, basePeriod, spanBPeriod);
        var closes = new double[bars.Count];
        for (int i = 0; i < closes.Length; i++) closes[i] = bars[i].Close;
        var first = startIndex ?? 1L - basePeriod;
        var last = endIndex ?? (long)bars.Count - 1 + basePeriod;
        if (first > last)
            throw new ArgumentOutOfRangeException(nameof(endIndex));
        return Rows();

        IEnumerable<IndexedIchimokuCloudValue> Rows()
        {
            for (var index = first; ; index++)
            {
                double? c = index >= conversionPeriod - 1L && index < closes.Length ? conversion[(int)index] : null;
                double? k = index >= basePeriod - 1L && index < closes.Length ? basis[(int)index] : null;
                double? a = null, b = null;
                // Bounds are tested before shifting, avoiding overflow even for Int64 endpoints.
                if (index >= basePeriod && index - basePeriod < closes.Length)
                {
                    int source = (int)(index - basePeriod);
                    if (source >= conversionPeriod - 1 && source >= basePeriod - 1)
                        a = Midpoint(conversion[source], basis[source]);
                    if (source >= spanBPeriod - 1) b = spanB[source];
                }
                var lag =
                    index >= 1L - basePeriod && index <= (long)closes.Length - basePeriod
                        ? (double?)closes[(int)(index + basePeriod - 1)]
                        : null;
                yield return new(
                    index,
                    new(c, k, a, b, lag)
                );
                if (index == last)
                    yield break;
            }
        }
    }

    /// <summary>Computes a cloud from finite high, low and close values. Null offsets default to basePeriod.</summary>
    public static IReadOnlyList<IchimokuCloudValue> Calculate(
        IReadOnlyList<Bar> bars,
        int conversionPeriod = 9,
        int basePeriod = 26,
        int spanBPeriod = 52,
        int? forwardOffset = null,
        int? backwardOffset = null
    )
    {
        Validate(bars, conversionPeriod, basePeriod, spanBPeriod, forwardOffset, backwardOffset);
        var forward = forwardOffset ?? basePeriod;
        var backward = backwardOffset ?? basePeriod;
        var (conversion, basis, spanB) = CaptureMidpoints(bars, conversionPeriod, basePeriod, spanBPeriod);
        var result = new IchimokuCloudValue[bars.Count];
        var leadingStart = Math.Max(2L * forward, Math.Max(conversionPeriod, basePeriod)) - 1;
        for (var i = 0; i < bars.Count; i++)
        {
            double? a = null,
                b = null;
            var source = (long)i - forward;
            if (source >= 0)
            {
                if (
                    i >= leadingStart
                    && source >= conversionPeriod - 1L
                    && source >= basePeriod - 1L
                )
                    a = Midpoint(conversion[(int)source], basis[(int)source]);
                if (source >= spanBPeriod - 1L) b = spanB[(int)source];
            }
            var future = (long)i + backward;
            result[i] = new(
                i >= conversionPeriod - 1 ? conversion[i] : null,
                i >= basePeriod - 1 ? basis[i] : null,
                a,
                b,
                future < bars.Count ? bars[(int)future].Close : null
            );
        }
        return result;
    }

    private static void Validate(IReadOnlyList<Bar> bars, int conversionPeriod, int basePeriod,
        int spanBPeriod, int? forwardOffset, int? backwardOffset)
    {
        if (bars is null) throw new ArgumentNullException(nameof(bars));
        if (conversionPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(conversionPeriod));
        if (basePeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(basePeriod));
        if (spanBPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(spanBPeriod));
        var forward = forwardOffset ?? basePeriod;
        var backward = backwardOffset ?? basePeriod;
        if (forward < 0)
            throw new ArgumentOutOfRangeException(nameof(forwardOffset));
        if (backward < 0)
            throw new ArgumentOutOfRangeException(nameof(backwardOffset));
        foreach (var b in bars)
            if (!FrameworkCompatibility.IsFinite(b.High) || !FrameworkCompatibility.IsFinite(b.Low) || !FrameworkCompatibility.IsFinite(b.Close))
                throw new ArgumentOutOfRangeException(nameof(bars));
    }

    private static (double[] Conversion, double[] Base, double[] SpanB) CaptureMidpoints(
        IReadOnlyList<Bar> bars, int conversionPeriod, int basePeriod, int spanBPeriod)
    {
        // Presence is determined solely by each complete-window boundary.
        var conversion = Midpoints(bars, conversionPeriod);
        var basis = basePeriod == conversionPeriod ? conversion : Midpoints(bars, basePeriod);
        var spanB = spanBPeriod == conversionPeriod ? conversion
            : spanBPeriod == basePeriod ? basis : Midpoints(bars, spanBPeriod);
        return (conversion, basis, spanB);
    }

    private static double Midpoint(double high, double low)
    {
        var sum = new ExactMeanAccumulator();
        sum.Add(high);
        sum.Add(low);
        return sum.Mean(2);
    }

    private static double[] Midpoints(IReadOnlyList<Bar> bars, int period)
    {
        if (period > bars.Count) return [];
        var result = new double[bars.Count];
        var highs = new WindowExtremeDeque(period, true);
        var lows = new WindowExtremeDeque(period, false);
        for (var i = 0; i < bars.Count; i++)
        {
            highs.Add(bars[i].High);
            lows.Add(bars[i].Low);
            if (i >= period - 1)
                result[i] = Midpoint(highs.Value, lows.Value);
        }
        return result;
    }
}
