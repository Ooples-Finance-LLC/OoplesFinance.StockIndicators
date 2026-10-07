using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class ClassicStochasticRsiReference
{
    internal static double?[][] Values(IReadOnlyList<Bar> bars, ClassicStochasticRsi owner)
    {
        var output = Enumerable.Range(0, 2).Select(_ => new double?[bars.Count]).ToArray();
        var start = (long)owner.RsiPeriod + owner.RsiSuppression + owner.StochasticPeriod - 1;
        if (start >= bars.Count)
            return output;
        var rsi = StrengthReference.Calculate(
            bars,
            owner.RsiPeriod,
            WilderStrengthConvention.RsiZeroFlat,
            owner.RsiSuppression
        )[0];
        var raw = new List<Bar>();
        for (var i = (int)start; i < bars.Count; i++)
        {
            var window = rsi.Skip(i - owner.StochasticPeriod + 1)
                .Take(owner.StochasticPeriod)
                .Select(ReferenceFraction.FromDouble)
                .ToArray();
            var minimum = window.Aggregate((a, b) => a.CompareTo(b) < 0 ? a : b);
            var maximum = window.Aggregate((a, b) => a.CompareTo(b) > 0 ? a : b);
            var spread = maximum - minimum;
            var value =
                spread.Sign == 0
                    ? 0
                    : (new ReferenceFraction(100) * (window[window.Length - 1] - minimum) / spread).ToDouble();
            raw.Add(new Bar(bars[i].Time, value, value, value, value, 0));
        }
        var signal = ClassicAverageReference.Values(raw, owner.SignalAverage);
        for (var i = 0; i < raw.Count; i++)
        {
            if (!signal[i].HasValue)
                continue;
            output[0][(int)start + i] = raw[i].Close;
            output[1][(int)start + i] = signal[i];
        }
        return output;
    }
}
