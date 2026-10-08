using System.Buffers;

namespace OoplesFinance.StockIndicators.Helpers;

internal static class EhlersAutocorrelation
{
    internal static double Normalized(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        if (x.Length < 2) return 0;
        double sx = 0, sy = 0;
        for (var i = 0; i < x.Length; i++) { sx += x[i] - x[0]; sy += y[i] - y[0]; }
        var mx = sx / x.Length;
        var my = sy / x.Length;
        double xx = 0, yy = 0, xy = 0;
        for (var i = 0; i < x.Length; i++)
        {
            var dx = (x[i] - x[0]) - mx;
            var dy = (y[i] - y[0]) - my;
            xx += dx * dx;
            yy += dy * dy;
            xy += dx * dy;
        }
        if (xx == 0 || yy == 0) return 0;
        var correlation = xy / (Math.Sqrt(xx) * Math.Sqrt(yy));
        return .5 * (1 + Math.Max(-1, Math.Min(1, correlation)));
    }

    internal static void Compute(ReadOnlySpan<double> values, Span<double> output, int period)
    {
        period = Math.Max(1, period);
        var x = ArrayPool<double>.Shared.Rent(period);
        var y = ArrayPool<double>.Shared.Rent(period);
        try
        {
            for (var i = 0; i < values.Length; i++)
            {
                var count = Math.Min(i + 1, period);
                var start = i - count + 1;
                for (var j = 0; j < count; j++)
                {
                    x[j] = values[start + j];
                    y[j] = start + j < period ? 0 : values[start + j - period];
                }
                output[i] = Normalized(x.AsSpan(0, count), y.AsSpan(0, count));
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(x);
            ArrayPool<double>.Shared.Return(y);
        }
    }
}
