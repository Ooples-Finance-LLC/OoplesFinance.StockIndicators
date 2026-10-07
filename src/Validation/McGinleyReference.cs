using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class McGinleyReference
{
    internal static double?[] Values(
        IReadOnlyList<Bar> bars,
        int period,
        double factor,
        McGinleyStartup startup
    )
    {
        var result = new double?[bars.Count];
        if (bars.Count == 0)
            return result;
        var previous = bars[0].Close;
        long start = 1;
        var scale = ReferenceFraction.FromDouble(factor) * new ReferenceFraction(period);
        for (var i = 0; i < bars.Count; i++)
        {
            var value = bars[i].Close;
            if (i == 0 && startup == McGinleyStartup.Immediate)
            {
                result[i] = previous;
                continue;
            }
            if (startup == McGinleyStartup.ResetDelay && previous == 0) // NOSONAR: Exact zero restart.
            {
                previous = value;
                start = (long)i + period;
                continue;
            }
            var x = ReferenceFraction.FromDouble(value);
            var m = ReferenceFraction.FromDouble(previous);
            if (x.Sign == 0 && m.Sign != 0)
            {
                result[i] = m.Sign > 0 ? double.NegativeInfinity : double.PositiveInfinity;
                return result;
            }
            var ratio = m.Sign == 0 ? new ReferenceFraction(1) : x / m;
            var squared = ratio * ratio;
            previous = (m + (x - m) / (scale * squared * squared)).ToDouble();
            if (!FrameworkCompatibility.IsFinite(previous))
            {
                result[i] = previous;
                return result;
            }
            if (startup == McGinleyStartup.Immediate || i >= start)
                result[i] = previous;
        }
        return result;
    }
}
