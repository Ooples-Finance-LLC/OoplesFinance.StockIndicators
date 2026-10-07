using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class EmaDifferenceReference
{
    private static ReferenceFraction?[] Average(
        ReferenceFraction?[] source,
        long start,
        int period,
        bool first
    )
    {
        var r = new ReferenceFraction?[source.Length];
        var begin = start + (first ? 0 : period - 1L);
        if (begin >= source.Length)
            return r;
        r[(int)begin] = (
            source
                .Skip((int)start)
                .Take(first ? 1 : period)
                .Aggregate(new ReferenceFraction(0), (sum, v) => sum + v!.Value)
            / new ReferenceFraction(first ? 1 : period)
        ).RoundExtendedBinary64();
        for (var i = (int)begin + 1; i < source.Length; i++)
            r[i] = (
                (
                    r[i - 1]!.Value * new ReferenceFraction(period - 1)
                    + source[i]!.Value * new ReferenceFraction(2)
                ) / new ReferenceFraction((long)period + 1)
            ).RoundExtendedBinary64();
        return r;
    }

    internal static double?[][] Values(IReadOnlyList<Bar> bars, EmaDifferenceSignal owner)
    {
        var input = bars.Select(b =>
                (ReferenceFraction?)ReferenceFraction.FromDouble(owner.Volume ? b.Volume : b.Close)
            )
            .ToArray();
        var fast = Average(input, 0, owner.FastPeriod, owner.FirstPrice);
        var slow = Average(input, 0, owner.SlowPeriod, owner.FirstPrice);
        var oscillator = new ReferenceFraction?[bars.Count];
        var signalInput = new ReferenceFraction?[bars.Count];
        for (var i = 0; i < bars.Count; i++)
            if (fast[i].HasValue && slow[i].HasValue)
            {
                if (!owner.Percentage)
                    oscillator[i] = (fast[i]!.Value - slow[i]!.Value).RoundExtendedBinary64();
                else if (slow[i]!.Value.Sign != 0)
                    oscillator[i] = (
                        new ReferenceFraction(100)
                        * (fast[i]!.Value - slow[i]!.Value)
                        / slow[i]!.Value
                    ).RoundExtendedBinary64();
                signalInput[i] = oscillator[i] ?? new ReferenceFraction(0);
            }
        var start = owner.FirstPrice ? 0 : Math.Max(owner.FastPeriod, owner.SlowPeriod) - 1L;
        var signal = Average(signalInput, start, owner.SignalPeriod, owner.FirstPrice);
        var histogram = new ReferenceFraction?[bars.Count];
        for (var i = 0; i < bars.Count; i++)
            if (oscillator[i].HasValue && signal[i].HasValue)
                histogram[i] = (oscillator[i]!.Value - signal[i]!.Value).RoundExtendedBinary64();
        var rows = new[] { oscillator, signal, histogram, fast, slow };
        return rows.Select(
                (row, j) =>
                    row.Select(v => ((int)owner.Selection & (1 << j)) != 0 ? v?.ToDouble() : null)
                        .ToArray()
            )
            .ToArray();
    }
}
