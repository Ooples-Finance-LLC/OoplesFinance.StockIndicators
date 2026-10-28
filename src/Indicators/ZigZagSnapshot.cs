using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Confirmed pivot direction. Unconfirmed endpoints have no direction.</summary>
public enum ZigZagPointKind
{
    High,
    Low,
}

/// <summary>Retrospective zigzag and same-side retracement lines.</summary>
public sealed record ZigZagValue(
    double? ZigZag,
    ZigZagPointKind? PointType,
    double? RetraceHigh,
    double? RetraceLow
);

/// <summary>Percentage-reversal pivots with latest-tie extremes and an unconfirmed final endpoint.</summary>
public static class ZigZagSnapshot
{
    private readonly record struct Point(int Index, BigInteger Price, ZigZagPointKind? Kind);
    private struct PendingRow
    {
        internal double? ZigZag, RetraceHigh, RetraceLow;
        internal ZigZagPointKind? PointType;
    }

    // Grid exponents are relative to the smallest binary64 subnormal. Zero
    // imposes no grid constraint; all finite nonzero significands fit Int64.
    private static int PriceGrid(double value)
    {
        long bits = BitConverter.DoubleToInt64Bits(value);
        if ((bits & long.MaxValue) == 0) return int.MaxValue;
        return Math.Max(0, (int)((bits >> 52) & 2047) - 1);
    }

    private static BigInteger GridUnits(double value, int grid)
    {
        long bits = BitConverter.DoubleToInt64Bits(value);
        int exponent = (int)((bits >> 52) & 2047);
        long significand = bits & 0xfffffffffffffL;
        if (exponent != 0) significand |= 1L << 52;
        if (significand == 0) return BigInteger.Zero;
        // The chosen common grid is no larger than any nonzero input grid.
        return new BigInteger(bits < 0 ? -significand : significand)
            << (Math.Max(0, exponent - 1) - grid);
    }

    /// <summary>Calculates all retrospective outputs using closes, or candle highs/lows when selected.</summary>
    /// <remarks>The first segment is absent except for its endpoint. Ratios use exact signed arithmetic; convex line interpolation rounds once.
    /// Zero reference prices cannot establish a percentage change. Recalculating with more bars can repaint the final segment.</remarks>
    public static IReadOnlyList<ZigZagValue> Calculate(
        IReadOnlyList<Bar> bars,
        double percentChange = 5,
        bool highLow = false
    )
    {
        if (bars is null) throw new ArgumentNullException(nameof(bars));
        if (!FrameworkCompatibility.IsFinite(percentChange) || percentChange <= 0)
            throw new ArgumentOutOfRangeException(nameof(percentChange));
        int priceScale = int.MaxValue;
        foreach (var b in bars)
        {
            if (
                !FrameworkCompatibility.IsFinite(b.Close)
                || highLow && (!FrameworkCompatibility.IsFinite(b.High) || !FrameworkCompatibility.IsFinite(b.Low))
            )
                throw new ArgumentOutOfRangeException(nameof(bars));
            priceScale = Math.Min(priceScale, PriceGrid(highLow ? b.High : b.Close));
            if (highLow) priceScale = Math.Min(priceScale, PriceGrid(b.Low));
        }
        if (bars.Count == 0)
            return Array.Empty<ZigZagValue>();
        // An unconfirmed first pivot can use Close even in high/low mode.
        priceScale = Math.Min(priceScale, PriceGrid(bars[0].Close));
        if (priceScale == int.MaxValue) priceScale = 0;
        var result = new PendingRow[bars.Count];
        BigInteger U(double v) => GridUnits(v, priceScale);
        double Publish(BigInteger numerator, int denominator) =>
            ExactMeanAccumulator.ScaledRatio(numerator, denominator, priceScale - 1074);
        // Cancel the same power of two on both sides of every reversal test.
        int thresholdScale = Math.Min(1074, PriceGrid(percentChange));
        var threshold = GridUnits(percentChange, thresholdScale);
        var grid = BigInteger.One << (1074 - thresholdScale);
        var highs = bars.Select(b => U(highLow ? b.High : b.Close)).ToArray();
        var lows = highLow ? bars.Select(b => U(b.Low)).ToArray() : highs;
        bool Reversal(BigInteger change, BigInteger price) =>
            price.Sign > 0
                ? change * 100 * grid >= threshold * price
                : price.Sign < 0 && change * 100 * grid <= threshold * price;
        var firstKind = (ZigZagPointKind?)null;
        for (var i = 0; i < bars.Count; i++)
        {
            if (lows[0].IsZero || highs[0].IsZero)
                continue;
            var up = highs[i] - lows[0];
            var down = highs[0] - lows[i];
            var comparison =
                (up * highs[0]).CompareTo(down * lows[0]) * lows[0].Sign * highs[0].Sign;
            if (Reversal(up, lows[0]) && comparison > 0)
            {
                firstKind = ZigZagPointKind.Low;
                break;
            }
            if (Reversal(down, highs[0]) && comparison < 0)
            {
                firstKind = ZigZagPointKind.High;
                break;
            }
        }
        var initial = new Point(
            0,
            firstKind == ZigZagPointKind.Low ? lows[0]
                : firstKind == ZigZagPointKind.High ? highs[0]
                : U(bars[0].Close),
            firstKind
        );
        var points = new List<Point> { initial };
        while (points[points.Count - 1].Index < bars.Count - 1)
        {
            var last = points[points.Count - 1];
            var rising = last.Kind == ZigZagPointKind.Low;
            var candidate = new Point(
                last.Index,
                last.Price,
                rising ? ZigZagPointKind.High : ZigZagPointKind.Low
            );
            for (var i = last.Index + 1; i < bars.Count; i++)
            {
                var price = rising ? highs[i] : lows[i];
                var newer = rising ? price >= candidate.Price : price <= candidate.Price;
                if (newer)
                    candidate = new(i, price, candidate.Kind);
                else if (
                    Reversal(
                        rising ? candidate.Price - lows[i] : highs[i] - candidate.Price,
                        candidate.Price
                    )
                )
                    break;
                if (i == bars.Count - 1)
                    candidate = new(i, price, null);
            }
            points.Add(candidate);
        }
        var previousHigh = new Point(0, highs[0], ZigZagPointKind.High);
        var previousLow = new Point(0, lows[0], ZigZagPointKind.Low);
        for (var k = 1; k < points.Count; k++)
        {
            var a = points[k - 1];
            var b = points[k];
            int width = b.Index - a.Index;
            var step = a.Index > 0 ? b.Price - a.Price : BigInteger.Zero;
            var numerator = a.Index > 0 ? a.Price * width : BigInteger.Zero;
            // Advance the exact interpolation numerator; round only at publication.
            for (var i = a.Index + 1; i <= b.Index; i++)
            {
                if (a.Index > 0)
                {
                    numerator += step;
                    result[i].ZigZag = Publish(numerator, width);
                }
                else
                    result[i].ZigZag = i == b.Index
                        ? Publish(b.Price, 1) : null;
                result[i].PointType = i == b.Index ? b.Kind : null;
            }
            if (!a.Kind.HasValue)
                continue;
            var prior = a.Kind == ZigZagPointKind.Low ? previousHigh : previousLow;
            if (a.Kind == ZigZagPointKind.Low)
                previousHigh = b;
            else
                previousLow = b;
            if (prior.Index == 0 || prior.Index == b.Index)
                continue;
            int retraceWidth = b.Index - prior.Index;
            var retraceStep = b.Price - prior.Price;
            var retraceNumerator = prior.Price * retraceWidth;
            for (var i = prior.Index; i <= b.Index; i++)
            {
                double value = Publish(retraceNumerator, retraceWidth);
                if (a.Kind == ZigZagPointKind.Low)
                    result[i].RetraceHigh = value;
                else
                    result[i].RetraceLow = value;
                retraceNumerator += retraceStep;
            }
        }
        var rows = new ZigZagValue[result.Length];
        for (var i = 0; i < rows.Length; i++)
            rows[i] = new(result[i].ZigZag, result[i].PointType, result[i].RetraceHigh, result[i].RetraceLow);
        return rows;
    }
}
