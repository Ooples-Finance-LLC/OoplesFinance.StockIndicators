namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastSnapshots
{
    // A product compared within this share of its size is decided exactly instead: on prices quoted to a
    // few decimals, a move of exactly the threshold percentage is an everyday event.
    private const double NearTie = 1e-13;

    private readonly struct ZigZagPoint(int index, double price, ZigZagPointKind? kind)
    {
        internal readonly int Index = index;
        internal readonly double Price = price;
        internal readonly ZigZagPointKind? Kind = kind;
    }

    // Whether the move from b to a is at least the threshold percentage of the price, on the price's side
    // of zero.
    private static bool ZigZagReversal(double a, double b, double price, double threshold)
    {
        if (price == 0) return false; // NOSONAR an exact zero has no percentage
        double change = (a - b) * 100, bound = threshold * price, difference = change - bound;
        if (Math.Abs(difference) <= NearTie * Math.Abs(bound)) return ZigZagSnapshot.ReversalExact(a, b, price, threshold);
        return price > 0 ? difference > 0 : difference < 0;
    }

    // Which of the rise from the first low and the fall from the first high is the larger share of its
    // base: positive for the rise, negative for the fall.
    private static int FirstMove(double high, double low, double firstHigh, double firstLow)
    {
        double rise = (high - firstLow) * firstHigh, fall = (firstHigh - low) * firstLow, difference = rise - fall;
        if (Math.Abs(difference) <= NearTie * (Math.Abs(rise) + Math.Abs(fall)))
            return ZigZagSnapshot.CompareFirstMoveExact(high, low, firstHigh, firstLow);
        return Math.Sign(difference) * Math.Sign(firstLow) * Math.Sign(firstHigh);
    }

    /// <summary>
    /// Zig-zag: swing points where the price turns by at least the threshold percentage, joined by straight
    /// lines, with the retracement lines between like points.
    /// </summary>
    internal static ZigZagValue[] ZigZag(IReadOnlyList<Bar> bars, double percentChange, bool highLow)
    {
        var count = bars.Count;
        if (count == 0) return [];
        var rows = Rows(bars);
        var pool = System.Buffers.ArrayPool<double>.Shared;
        double[] rentedHighs = pool.Rent(count), rentedLows = highLow ? pool.Rent(count) : rentedHighs;
        // Four published columns, not-a-number where a row has none.
        var rentedLines = pool.Rent(3 * count);
        try
        {
            Span<double> highs = rentedHighs.AsSpan(0, count), lows = rentedLows.AsSpan(0, count);
            for (var i = 0; i < count; i++)
            {
                highs[i] = highLow ? rows[i].High : rows[i].Close;
                if (highLow) lows[i] = rows[i].Low;
            }
            double firstHigh = highs[0], firstLow = lows[0];
            ZigZagPointKind? firstKind = null;
            if (firstLow != 0 && firstHigh != 0) // NOSONAR an exact zero has no percentage
                for (var i = 0; i < count; i++)
                {
                    var comparison = FirstMove(highs[i], lows[i], firstHigh, firstLow);
                    if (comparison > 0 && ZigZagReversal(highs[i], firstLow, firstLow, percentChange))
                    {
                        firstKind = ZigZagPointKind.Low;
                        break;
                    }
                    if (comparison < 0 && ZigZagReversal(firstHigh, lows[i], firstHigh, percentChange))
                    {
                        firstKind = ZigZagPointKind.High;
                        break;
                    }
                }
            var points = new List<ZigZagPoint>
            {
                new(0, firstKind == ZigZagPointKind.Low ? firstLow : firstKind == ZigZagPointKind.High ? firstHigh : rows[0].Close,
                    firstKind),
            };
            while (points[points.Count - 1].Index < count - 1)
            {
                var last = points[points.Count - 1];
                var rising = last.Kind == ZigZagPointKind.Low;
                var kind = rising ? ZigZagPointKind.High : ZigZagPointKind.Low;
                var at = last.Index;
                var extreme = last.Price;
                ZigZagPointKind? found = kind;
                for (var i = last.Index + 1; i < count; i++)
                {
                    var price = rising ? highs[i] : lows[i];
                    if (rising ? price >= extreme : price <= extreme)
                    {
                        at = i;
                        extreme = price;
                    }
                    else if (rising ? ZigZagReversal(extreme, lows[i], extreme, percentChange)
                        : ZigZagReversal(highs[i], extreme, extreme, percentChange)) break;
                    if (i == count - 1)
                    {
                        at = i;
                        extreme = price;
                        found = null;
                    }
                }
                points.Add(new(at, extreme, found));
            }

            Span<double> line = rentedLines.AsSpan(0, count), retraceHigh = rentedLines.AsSpan(count, count);
            var retraceLow = rentedLines.AsSpan(2 * count, count);
            rentedLines.AsSpan(0, 3 * count).Fill(double.NaN);
            var result = new ZigZagValue[count];
            ZigZagPoint previousHigh = new(0, firstHigh, ZigZagPointKind.High), previousLow = new(0, firstLow, ZigZagPointKind.Low);
            for (var k = 1; k < points.Count; k++)
            {
                ZigZagPoint a = points[k - 1], b = points[k];
                if (a.Index > 0) Join(a, b, a.Index + 1, line);
                else if (b.Index > 0) line[b.Index] = b.Price;
                if (!a.Kind.HasValue) continue;
                var low = a.Kind == ZigZagPointKind.Low;
                var prior = low ? previousHigh : previousLow;
                if (low) previousHigh = b;
                else previousLow = b;
                if (prior.Index == 0 || prior.Index == b.Index) continue;
                Join(prior, b, prior.Index, low ? retraceHigh : retraceLow);
            }
            var absent = new ZigZagValue(null, null, null, null);
            var next = 1;
            for (var i = 0; i < count; i++)
            {
                ZigZagPointKind? pointType = null;
                while (next < points.Count && points[next].Index < i) next++;
                if (next < points.Count && points[next].Index == i && i > 0) pointType = points[next].Kind;
                double z = line[i], high = retraceHigh[i], low = retraceLow[i];
                result[i] = double.IsNaN(z) && double.IsNaN(high) && double.IsNaN(low) && pointType is null
                    ? absent
                    : new ZigZagValue(Present(z), pointType, Present(high), Present(low));
            }
            return result;
        }
        finally
        {
            pool.Return(rentedHighs);
            if (highLow) pool.Return(rentedLows);
            pool.Return(rentedLines);
        }
    }

    private static double? Present(double value) => double.IsNaN(value) ? null : value;

    // The straight line from a to b, written from the given bar through b.
    private static void Join(ZigZagPoint a, ZigZagPoint b, int from, Span<double> output)
    {
        double span = b.Index - a.Index;
        for (var i = from; i <= b.Index; i++)
            output[i] = (a.Price * (b.Index - i) + b.Price * (i - a.Index)) / span;
    }
}
