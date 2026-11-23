namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastSnapshots
{
    /// <summary>
    /// Volatility stop: the extreme close of the trend less (or plus) a multiple of the average true range
    /// as of the bar before, reversing when the close crosses it. Nothing is published up to and including
    /// the first reversal.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static VolatilityStopValue[] VolatilityStop(IReadOnlyList<Bar> bars, int period, double multiplier)
    {
        var count = bars.Count;
        if (count == 0) return [];
        var rows = Rows(bars);
        var result = new VolatilityStopValue[count];
        var absent = new VolatilityStopValue(null, null, null, null);
        var pool = System.Buffers.ArrayPool<double>.Shared;
        var stops = pool.Rent(count);
        // One byte per bar: bit 0 the side, bit 1 a reversal, bit 2 a stop.
        var flags = System.Buffers.ArrayPool<byte>.Shared.Rent(count);
        try
        {
            var initial = Math.Min(period, count);
            var bullish = rows[initial - 1].Close > rows[0].Close;
            var extreme = rows[0].Close;
            double rangeSum = 0, average = 0;
            var ranges = 0;
            for (var i = 0; i < initial; i++)
            {
                var price = rows[i].Close;
                extreme = bullish ? Math.Max(extreme, price) : Math.Min(extreme, price);
                if (i == 0) continue;
                rangeSum += FastKernels.TrueRange(rows[i].High, rows[i].Low, rows[i - 1].Close);
                ranges++;
            }
            var firstStop = -1;
            var weight = (period - 1.0) / period;
            var scale = 1.0 / period;
            for (var i = period; i < count; i++)
            {
                ref readonly var bar = ref rows[i];
                var price = bar.Close;
                byte flag = bullish ? (byte)1 : (byte)0;
                var reverse = false;
                // The average is the one the bar before completed.
                if (ranges >= period)
                {
                    var width = average * multiplier;
                    var stop = bullish ? extreme - width : extreme + width;
                    stops[i] = stop;
                    flag |= 4;
                    reverse = bullish ? price < stop : price > stop;
                }
                if (reverse)
                {
                    flag |= 2;
                    if (firstStop < 0) firstStop = i;
                    extreme = price;
                    bullish = !bullish;
                }
                else extreme = bullish ? Math.Max(extreme, price) : Math.Min(extreme, price);
                flags[i] = flag;
                var range = FastKernels.TrueRange(bar.High, bar.Low, rows[i - 1].Close);
                if (ranges < period)
                {
                    rangeSum += range;
                    if (++ranges == period) average = rangeSum / period;
                }
                else average = weight * average + scale * range;
            }
            for (var i = 0; i < count; i++)
            {
                if (i <= firstStop || i < period)
                {
                    result[i] = absent;
                    continue;
                }
                var flag = flags[i];
                if ((flag & 4) == 0)
                {
                    result[i] = new VolatilityStopValue(null, null, null, false);
                    continue;
                }
                var value = stops[i];
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new OverflowException("Volatility stop is not representable.");
                var longSide = (flag & 1) != 0;
                result[i] = new VolatilityStopValue(value, longSide ? null : value, longSide ? value : null, (flag & 2) != 0);
            }
            return result;
        }
        finally
        {
            pool.Return(stops);
            System.Buffers.ArrayPool<byte>.Shared.Return(flags);
        }
    }
}
