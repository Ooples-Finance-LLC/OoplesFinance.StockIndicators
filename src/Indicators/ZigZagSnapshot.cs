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
        foreach (var b in bars)
            if (
                !FrameworkCompatibility.IsFinite(b.Close)
                || highLow && (!FrameworkCompatibility.IsFinite(b.High) || !FrameworkCompatibility.IsFinite(b.Low))
            )
                throw new ArgumentOutOfRangeException(nameof(bars));
        var result = Enumerable
            .Range(0, bars.Count)
            .Select(_ => new ZigZagValue(null, null, null, null))
            .ToArray();
        if (bars.Count == 0)
            return result;
        BigInteger U(double v) => ExactVarianceWindow.Units(v);
        var threshold = U(percentChange);
        var grid = BigInteger.One << 1074;
        var highs = bars.Select(b => U(highLow ? b.High : b.Close)).ToArray();
        var lows = bars.Select(b => U(highLow ? b.Low : b.Close)).ToArray();
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
        double Line(Point a, Point b, int i) =>
            ExactMeanAccumulator.UnitRatio(
                a.Price * (b.Index - i) + b.Price * (i - a.Index),
                b.Index - a.Index
            );
        var previousHigh = new Point(0, highs[0], ZigZagPointKind.High);
        var previousLow = new Point(0, lows[0], ZigZagPointKind.Low);
        for (var k = 1; k < points.Count; k++)
        {
            var a = points[k - 1];
            var b = points[k];
            for (var i = a.Index + 1; i <= b.Index; i++)
                result[i] = result[i] with
                {
                    ZigZag = a.Index > 0 || i == b.Index ? Line(a, b, i) : null,
                    PointType = i == b.Index ? b.Kind : null,
                };
            if (!a.Kind.HasValue)
                continue;
            var prior = a.Kind == ZigZagPointKind.Low ? previousHigh : previousLow;
            if (a.Kind == ZigZagPointKind.Low)
                previousHigh = b;
            else
                previousLow = b;
            if (prior.Index == 0 || prior.Index == b.Index)
                continue;
            for (var i = prior.Index; i <= b.Index; i++)
                result[i] =
                    a.Kind == ZigZagPointKind.Low
                        ? result[i] with
                        {
                            RetraceHigh = Line(prior, b, i),
                        }
                        : result[i] with
                        {
                            RetraceLow = Line(prior, b, i),
                        };
        }
        return result;
    }
}
