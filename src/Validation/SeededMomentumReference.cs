using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class SeededMomentumReference
{
    private static ReferenceFraction F(double x) => ReferenceFraction.FromDouble(x);

    private static ReferenceFraction?[] Smooth(
        ReferenceFraction?[] input,
        long start,
        int period,
        ReferenceFraction alpha
    )
    {
        var r = new ReferenceFraction?[input.Length];
        var first = start + period - 1;
        if (first >= input.Length)
            return r;
        var seed = input.Skip((int)start).Take(period).ToArray();
        if (seed.All(v => v.HasValue))
            r[(int)first] = (
                seed.Aggregate(new ReferenceFraction(0), (s, v) => s + v!.Value)
                / new ReferenceFraction(period)
            ).RoundExtendedBinary64();
        for (var i = (int)first + 1; i < input.Length; i++)
            if (r[i - 1].HasValue && input[i].HasValue)
                r[i] = (
                    r[i - 1]!.Value * (new ReferenceFraction(1) - alpha) + input[i]!.Value * alpha
                ).RoundExtendedBinary64();
        return r;
    }

    internal static double?[][] Pmo(IReadOnlyList<Bar> bars, int period, int smooth, int signal)
    {
        var rates = new ReferenceFraction?[bars.Count];
        for (var i = 1; i < bars.Count; i++)
            if (bars[i - 1].Close != 0) // NOSONAR: Exact zero defines the missing-value boundary.
                rates[i] = (
                    new ReferenceFraction(100)
                    * (F(bars[i].Close) - F(bars[i - 1].Close))
                    / F(bars[i - 1].Close)
                ).RoundExtendedBinary64();
        var first = Smooth(
            rates,
            1,
            period,
            new ReferenceFraction(2) / new ReferenceFraction(period)
        );
        var scaled = first
            .Select(v =>
                v.HasValue
                    ? (ReferenceFraction?)
                        (new ReferenceFraction(10) * v.Value).RoundExtendedBinary64()
                    : null
            )
            .ToArray();
        var momentum = Smooth(
            scaled,
            period,
            smooth,
            new ReferenceFraction(2) / new ReferenceFraction(smooth)
        );
        var signaled = Smooth(
            momentum,
            (long)period + smooth - 1,
            signal,
            new ReferenceFraction(2) / new ReferenceFraction((long)signal + 1)
        );
        return
        [
            momentum.Select(v => v?.ToDouble()).ToArray(),
            signaled.Select(v => v?.ToDouble()).ToArray(),
        ];
    }

    internal static double?[][] Tsi(IReadOnlyList<Bar> bars, int period, int smooth, int signal)
    {
        var changes = new ReferenceFraction?[bars.Count];
        var absolute = new ReferenceFraction?[bars.Count];
        for (var i = 1; i < bars.Count; i++)
        {
            var delta = (F(bars[i].Close) - F(bars[i - 1].Close)).RoundExtendedBinary64();
            changes[i] = delta;
            absolute[i] = delta.Abs();
        }
        var alpha = new ReferenceFraction(2) / new ReferenceFraction((long)period + 1);
        var signed = Smooth(changes, 1, period, alpha);
        var abs = Smooth(absolute, 1, period, alpha);
        ReferenceFraction?[] Second(ReferenceFraction?[] input)
        {
            if (smooth > 1)
                return Smooth(
                    input,
                    period,
                    smooth,
                    new ReferenceFraction(2) / new ReferenceFraction((long)smooth + 1)
                );
            var r = new ReferenceFraction?[bars.Count];
            for (var i = (long)period + 1; i < bars.Count; i++)
                r[(int)i] = input[(int)i];
            return r;
        }
        var num = Second(signed);
        var den = Second(abs);
        var r = new[] { new double?[bars.Count], new double?[bars.Count] };
        for (var i = 0; i < bars.Count; i++)
            if (num[i].HasValue && den[i].HasValue && den[i]!.Value.Sign != 0)
                r[0][i] = (new ReferenceFraction(100) * num[i]!.Value / den[i]!.Value).ToDouble();
        if (signal <= 1)
            return r;
        var first = (long)period + smooth - 1;
        var signalStart = first + signal - 1;
        if (signalStart >= bars.Count)
            return r;
        var seed = Enumerable
            .Range((int)first, signal)
            .Select(i => smooth == 1 && i == first ? (double?)0 : r[0][i])
            .ToArray();
        if (seed.All(v => v.HasValue))
            r[1][(int)signalStart] = (
                seed.Aggregate(new ReferenceFraction(0), (sum, v) => sum + F(v!.Value))
                / new ReferenceFraction(signal)
            ).ToDouble();
        for (var i = (int)signalStart + 1; i < bars.Count; i++)
            if (r[1][i - 1].HasValue && r[0][i].HasValue)
                r[1][i] = (
                    (
                        F(r[1][i - 1]!.Value) * new ReferenceFraction(signal - 1)
                        + new ReferenceFraction(2) * F(r[0][i]!.Value)
                    ) / new ReferenceFraction((long)signal + 1)
                ).ToDouble();
        return r;
    }
}
