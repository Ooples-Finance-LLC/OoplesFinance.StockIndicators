using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class TripleRateReference
{
    private static ReferenceFraction F(double x) => ReferenceFraction.FromDouble(x);

    internal static double?[][] Calculate(
        IReadOnlyList<Bar> bars,
        int period,
        TripleRateSeed mode,
        int? signal,
        int suppression
    )
    {
        var layers = Enumerable
            .Range(0, 3)
            .Select(_ => new ReferenceFraction?[bars.Count])
            .ToArray();
        var shared = mode == TripleRateSeed.SharedMean;
        var lookback = (long)period - 1 + suppression;
        var alpha = new ReferenceFraction(2) / new ReferenceFraction((long)period + 1);
        for (var stage = 0; stage < 3; stage++)
        {
            var start = shared ? 0 : stage * lookback;
            var initial = start + (mode == TripleRateSeed.CascadedFirstPrice ? 0 : period - 1L);
            if (initial >= bars.Count)
                continue;
            ReferenceFraction Input(int i) =>
                stage == 0 ? F(bars[i].Close) : layers[stage - 1][i]!.Value;
            var initialValue =
                mode == TripleRateSeed.CascadedFirstPrice
                    ? Input((int)start)
                    : Enumerable
                        .Range((int)start, period)
                        .Aggregate(
                            new ReferenceFraction(0),
                            (sum, i) => sum + (shared ? F(bars[i].Close) : Input(i))
                        ) / new ReferenceFraction(period);
            layers[stage][(int)initial] = initialValue.RoundExtendedBinary64();
            for (var i = (int)initial + 1; i < bars.Count; i++)
                layers[stage][i] = (
                    layers[stage][i - 1]!.Value * (new ReferenceFraction(1) - alpha)
                    + Input(i) * alpha
                ).RoundExtendedBinary64();
        }
        var r = Enumerable.Range(0, 3).Select(_ => new double?[bars.Count]).ToArray();
        var first = shared ? period : 3 * lookback + 1;
        for (var i = 0; i < bars.Count; i++)
        {
            if (i >= (shared ? period : 3 * lookback))
                r[1][i] = layers[2][i]!.Value.ToDouble();
            if (i < first)
                continue;
            var now = layers[2][i]!.Value;
            var prior = layers[2][i - 1]!.Value;
            r[0][i] =
                prior.Sign == 0
                    ? shared
                        ? now.Sign == 0
                            ? null
                            : now.Sign > 0
                                ? double.PositiveInfinity
                                : double.NegativeInfinity
                        : 0
                    : (new ReferenceFraction(100) * (now - prior) / prior).ToDouble();
            if (!signal.HasValue || i < first + signal.Value - 1)
                continue;
            var window = r[0].Skip(i - signal.Value + 1).Take(signal.Value).ToArray();
            if (window.Any(v => !v.HasValue))
                continue;
            r[2][i] = window.Any(v => double.IsInfinity(v!.Value) || double.IsNaN(v!.Value))
                ? window.Sum(v => v!.Value) / signal.Value
                : (
                    window.Aggregate(new ReferenceFraction(0), (sum, v) => sum + F(v!.Value))
                    / new ReferenceFraction(signal.Value)
                ).ToDouble();
        }
        return r;
    }
}
