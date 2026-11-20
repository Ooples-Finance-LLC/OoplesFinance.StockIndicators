using System.Numerics;

namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastSnapshots
{
    /// <summary>
    /// Renko bricks. Every brick edge is the rounded first close plus a whole number of brick sizes, so the
    /// position is kept as that whole number and never accumulates rounding. A price within rounding of a
    /// brick edge is counted exactly, since on prices quoted to a few decimals that is an everyday event.
    /// The bricks are built at once, so an unrepresentable one fails the call rather than its enumeration.
    /// </summary>
    internal static List<RenkoBrick> Renko(IReadOnlyList<Bar> bars, double brickSize, bool highLow)
    {
        var bricks = new List<RenkoBrick>(bars.Count);
        if (bars.Count == 0) return bricks;
        var rows = Rows(bars);
        var grid = RenkoSnapshot.Grid(rows[0].Close, brickSize);
        double scale = (double)grid.Scale, origin = (double)grid.Origin, step = brickSize * scale;
        long open = 0, close = 0;
        double high = 0, low = 0, volume = 0;
        var reset = true;
        for (var i = 1; i < rows.Length; i++)
        {
            ref readonly var bar = ref rows[i];
            high = reset ? bar.High : Math.Max(high, bar.High);
            low = reset ? bar.Low : Math.Min(low, bar.Low);
            volume = reset ? bar.Volume : volume + bar.Volume;
            long upper = Math.Max(open, close), lower = Math.Min(open, close);
            var count = highLow
                ? RangeBricks(bar.High, bar.Low, upper, lower, origin, step, scale, grid)
                : CloseBricks(bar.Close, upper, lower, origin, step, scale, grid);
            reset = count != 0;
            if (count == 0) continue;
            var quantity = Math.Abs(count);
            var share = volume / quantity;
            if (double.IsNaN(share) || double.IsInfinity(share)) throw new OverflowException("Renko volume is not representable.");
            var direction = Math.Sign(count);
            var up = direction > 0;
            open = up ? Math.Max(open, close) : Math.Min(open, close);
            // Each brick opens where the one before closed, so that price is worked out once.
            var from = RenkoPrice(origin, open, step, scale);
            for (long j = 0; j < quantity; j++)
            {
                close = open + direction;
                var to = RenkoPrice(origin, close, step, scale);
                bricks.Add(new(bar.Time, from, high, low, to, share, up));
                open = close;
                from = to;
            }
            // The last brick's own open, as the next bar measures from both of its edges.
            open = close - direction;
        }
        return bricks;
    }

    private static double RenkoPrice(double origin, long bricks, double step, double scale)
    {
        var published = (origin + bricks * step) / scale;
        return !double.IsNaN(published) && !double.IsInfinity(published)
            ? published
            : throw new OverflowException("Renko price is not representable.");
    }

    // How far a brick count may be from a whole number before the double arithmetic is trusted with it.
    private static double RenkoSlack(double price, double origin, double step) =>
        1e-12 * (Math.Abs(price) + Math.Abs(origin)) / step + 1e-12;

    private static long CloseBricks(double close, long upper, long lower, double origin, double step, double scale,
        RenkoSnapshot.BrickGrid grid)
    {
        var price = close * scale;
        double above = (price - (origin + upper * step)) / step, below = (price - (origin + lower * step)) / step;
        var slack = RenkoSlack(price, origin, step);
        if (above > slack && TryWhole(above, slack, out var up)) return up;
        if (below < -slack && TryWhole(-below, slack, out var down)) return -down;
        if (above < -slack && below > slack) return 0;
        return RenkoSnapshot.CloseBricksExact(close, upper, lower, grid);
    }

    private static long RangeBricks(double high, double low, long upper, long lower, double origin, double step, double scale,
        RenkoSnapshot.BrickGrid grid)
    {
        double top = high * scale, bottom = low * scale;
        double up = (top - (origin + upper * step)) / step, down = ((origin + lower * step) - bottom) / step;
        var slack = RenkoSlack(Math.Max(Math.Abs(top), Math.Abs(bottom)), origin, step);
        if (Math.Abs(up - down) > 2 * slack)
        {
            var move = up > down ? up : -down;
            if (Math.Abs(move) < slack) return 0;
            if (TryWhole(Math.Abs(move), slack, out var bricks)) return move > 0 ? bricks : -bricks;
        }
        return RenkoSnapshot.RangeBricksExact(high, low, upper, lower, grid);
    }

    // The whole bricks in a positive count, when it is clear of every whole number by the slack.
    private static bool TryWhole(double count, double slack, out long whole)
    {
        whole = 0;
        if (!(count < 1e15) || !(slack < 0.25)) return false;
        var floor = Math.Floor(count);
        if (count - floor <= slack || floor + 1 - count <= slack) return false;
        whole = (long)floor;
        return true;
    }

    /// <summary>The brick size the average-true-range convention uses: the Wilder average at the last bar.</summary>
    internal static double? RenkoAtrSize(IReadOnlyList<Bar> bars, int period)
    {
        var rows = Rows(bars);
        if (rows.Length <= period) return null;
        double sum = 0;
        for (var i = 1; i <= period; i++) sum += FastKernels.TrueRange(rows[i].High, rows[i].Low, rows[i - 1].Close);
        var average = sum / period;
        var weight = (period - 1.0) / period;
        var share = 1.0 / period;
        for (var i = period + 1; i < rows.Length; i++)
            average = weight * average + share * FastKernels.TrueRange(rows[i].High, rows[i].Low, rows[i - 1].Close);
        return average;
    }
}
