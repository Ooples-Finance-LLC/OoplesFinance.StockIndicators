using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class StochasticRsiReference
{
    internal static double?[] Nullable(double?[] prices, int period)
    {
        var rsi = NullableStrengthReference.Calculate(
            prices,
            period,
            NullableStrengthConvention.RelativeStrengthIndex,
            1
        );
        return Normalize(rsi, period, 0, .5, 1);
    }

    private static double?[] Normalize(
        double?[] values,
        int period,
        int first,
        double flat,
        int scale
    )
    {
        var result = new double?[values.Length];
        for (long end = (long)first + period - 1; end < values.Length; end++)
        {
            var i = (int)end;
            var known = values
                .Skip(i - period + 1)
                .Take(period)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToArray();
            if (known.Length == 0)
            {
                result[i] = flat;
                continue;
            }
            var high = known.Max();
            var low = known.Min();
            if (high == low) // NOSONAR: Exact ties select the flat-range convention.
            {
                result[i] = flat;
                continue;
            }
            if (values[i].HasValue)
                result[i] = (
                    new ReferenceFraction(scale)
                    * (
                        ReferenceFraction.FromDouble(values[i]!.Value)
                        - ReferenceFraction.FromDouble(low)
                    )
                    / (ReferenceFraction.FromDouble(high) - ReferenceFraction.FromDouble(low))
                ).ToDouble();
        }
        return result;
    }

    internal static double[][] Smoothed(
        IReadOnlyList<Bar> bars,
        int rsiPeriod,
        int stochasticPeriod,
        int signalPeriod,
        int smoothPeriod
    )
    {
        var strength = StrengthReference.Calculate(
            bars,
            rsiPeriod,
            WilderStrengthConvention.RsiHundredFlat,
            0
        );
        var rsi = strength[0].Select((v, i) => strength[1][i] > 0 ? (double?)v : null).ToArray();
        var raw = Normalize(rsi, stochasticPeriod, rsiPeriod, 0, 100);
        double?[] Mean(double?[] input, int period)
        {
            var output = new double?[input.Length];
            for (var i = period - 1; i < input.Length; i++)
            {
                var window = input.Skip(i - period + 1).Take(period).ToArray();
                if (window.Any(v => !v.HasValue))
                    continue;
                var sum = new ReferenceFraction(0);
                foreach (var value in window)
                    sum += ReferenceFraction.FromDouble(value!.Value);
                output[i] = (sum / new ReferenceFraction(period)).ToDouble();
            }
            return output;
        }
        var value = Mean(raw, smoothPeriod);
        var signal = Mean(value, signalPeriod);
        return
        [
            value.Select(v => v ?? 0).ToArray(),
            signal.Select(v => v ?? 0).ToArray(),
            value.Select(v => v.HasValue ? 1d : 0).ToArray(),
            signal.Select(v => v.HasValue ? 1d : 0).ToArray(),
        ];
    }
}
