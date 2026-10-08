using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class WindowStochasticReference
{
    internal static double?[][] Values(IReadOnlyList<Bar> bars, WindowStochasticKdj owner)
    {
        var raw = new ReferenceFraction?[bars.Count];
        for (var i = owner.Period - 1; i < bars.Count; i++)
        {
            var window = bars.Skip(i - owner.Period + 1).Take(owner.Period).ToArray();
            var high = ReferenceFraction.FromDouble(window.Max(b => b.High));
            var low = ReferenceFraction.FromDouble(window.Min(b => b.Low));
            raw[i] =
                (high - low).Sign == 0
                    ? new ReferenceFraction(0)
                    : (
                        new ReferenceFraction(100)
                        * (ReferenceFraction.FromDouble(bars[i].Close) - low)
                        / (high - low)
                    ).RoundExtendedBinary64();
        }
        var k = Smooth(raw, owner.KPeriod, owner.WilderSmoothing);
        var d = Smooth(k, owner.DPeriod, owner.WilderSmoothing);
        var output = Enumerable.Range(0, 3).Select(_ => new double?[bars.Count]).ToArray();
        for (var i = 0; i < bars.Count; i++)
        {
            output[0][i] = k[i]?.ToDouble();
            output[1][i] = d[i]?.ToDouble();
            if (k[i].HasValue && d[i].HasValue)
                output[2][i] = (
                    ReferenceFraction.FromDouble(owner.KFactor) * k[i]!.Value
                    - ReferenceFraction.FromDouble(owner.DFactor) * d[i]!.Value
                ).ToDouble();
        }
        return output;
    }

    private static ReferenceFraction?[] Smooth(ReferenceFraction?[] values, int period, bool wilder)
    {
        var output = new ReferenceFraction?[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            if (!values[i].HasValue)
                continue;
            if (wilder)
                output[i] =
                    i == 0 || !output[i - 1].HasValue
                        ? values[i]
                        : (
                            (
                                new ReferenceFraction(period - 1) * output[i - 1]!.Value
                                + values[i]!.Value
                            ) / new ReferenceFraction(period)
                        ).RoundExtendedBinary64();
            else if (i >= period - 1)
            {
                var window = values.Skip(i - period + 1).Take(period).ToArray();
                if (window.Any(v => !v.HasValue))
                    continue;
                output[i] = (
                    window.Aggregate(new ReferenceFraction(0), (sum, v) => sum + v!.Value)
                    / new ReferenceFraction(period)
                ).RoundExtendedBinary64();
            }
        }
        return output;
    }
}
