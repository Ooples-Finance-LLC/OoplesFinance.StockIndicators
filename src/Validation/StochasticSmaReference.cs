namespace OoplesFinance.StockIndicators.Validation;

internal static class StochasticSmaReference
{
    internal static double[] Raw(IReadOnlyList<Indicators.Bar> bars, int period) =>
        RawExtended(bars, period).Select(v => v.ToDouble()).ToArray();

    private static ReferenceFraction[] RawExtended(IReadOnlyList<Indicators.Bar> bars, int period)
    {
        var result = Enumerable.Repeat(new ReferenceFraction(50), bars.Count).ToArray();
        for (var i = period - 1; i < bars.Count; i++)
        {
            var high = bars.Skip(i - period + 1).Take(period).Max(b => b.High);
            var low = bars.Skip(i - period + 1).Take(period).Min(b => b.Low);
            if (high == low) // NOSONAR: Exact equality defines the flat-range formula.
                continue;
            result[i] = (
                (ReferenceFraction.FromDouble(bars[i].Close) - ReferenceFraction.FromDouble(low))
                * new ReferenceFraction(100)
                / (ReferenceFraction.FromDouble(high) - ReferenceFraction.FromDouble(low))
            ).RoundExtendedBinary64();
        }
        return result;
    }

    internal static IReadOnlyList<double> Output(
        IReadOnlyList<Indicators.Bar> bars,
        int period,
        int k,
        int d,
        int slot,
        bool difference
    )
    {
        var raw = RawExtended(bars, period);
        ReferenceFraction?[] Mean(ReferenceFraction?[] values, int length)
        {
            var result = new ReferenceFraction?[values.Length];
            for (var i = length - 1; i < values.Length; i++)
            {
                var present = values
                    .Skip(i - length + 1)
                    .Take(length)
                    .Where(v => v.HasValue)
                    .ToArray();
                if (present.Length == 0)
                    continue;
                var sum = new ReferenceFraction(0);
                foreach (var value in present)
                    sum += value!.Value;
                result[i] = (sum / new ReferenceFraction(present.Length)).RoundExtendedBinary64();
            }
            return result;
        }
        var a = Mean(raw.Select(v => (ReferenceFraction?)v).ToArray(), k);
        var b = Mean(a, d);
        var output = new double[bars.Count];
        for (var i = 0; i < output.Length; i++)
        {
            var field = difference ? 2 : slot % 3;
            var exists =
                field == 0 ? a[i].HasValue
                : field == 1 ? b[i].HasValue
                : a[i].HasValue && b[i].HasValue;
            if (difference ? slot == 1 : slot >= 3)
            {
                output[i] = exists ? 1 : 0;
                continue;
            }
            if (!exists)
                continue;
            output[i] =
                field == 0 ? a[i]!.Value.ToDouble()
                : field == 1 ? b[i]!.Value.ToDouble()
                : (
                    a[i]!.Value * new ReferenceFraction(difference ? 1 : 3)
                    - b[i]!.Value * new ReferenceFraction(difference ? 1 : 2)
                ).ToDouble();
        }
        return output;
    }
}
