using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class DecayingExtremeReference
{
    internal static double[] Calculate(
        IReadOnlyList<Bar> bars,
        int period,
        bool maximum,
        double decay
    )
    {
        var result = new double[bars.Count];
        var current = 0d;
        var lastReset = 0;
        for (var i = 0; i < bars.Count; i++)
        {
            var window = bars.Skip(Math.Max(0, i - period + 1))
                .Take(Math.Min(i + 1, period))
                .Select(b => b.Close)
                .ToArray();
            var bound = maximum ? window.Max() : window.Min();
            if (decay == 0)
            {
                result[i] = bound;
                continue;
            }
            if (i == 0 || (maximum ? bars[i].Close >= current : bars[i].Close <= current))
            {
                current = bars[i].Close;
                lastReset = i;
            }
            var total = new ReferenceFraction(0);
            foreach (var price in window)
                total += ReferenceFraction.FromDouble(price);
            var mean = (total / new ReferenceFraction(window.Length)).ToDouble();
            var rate = ReferenceFraction.FromDouble(
                1 - Math.Exp(-(decay * .1) * (i - lastReset) / period)
            );
            var next = (
                ReferenceFraction.FromDouble(current) * (new ReferenceFraction(1) - rate)
                + ReferenceFraction.FromDouble(mean) * rate
            ).ToDouble();
            current = maximum ? Math.Min(next, bound) : Math.Max(next, bound);
            result[i] = current;
        }
        return result;
    }
}
