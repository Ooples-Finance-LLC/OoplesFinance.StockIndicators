using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class AlignedMacdReference
{
    private static ReferenceFraction?[] Average(
        ReferenceFraction?[] input,
        long start,
        int period,
        bool first,
        ReferenceFraction alpha
    )
    {
        var r = new ReferenceFraction?[input.Length];
        var begin = start + (first ? 0 : period - 1L);
        if (begin >= input.Length)
            return r;
        r[(int)begin] = (
            input
                .Skip((int)start)
                .Take(first ? 1 : period)
                .Aggregate(new ReferenceFraction(0), (s, v) => s + v!.Value)
            / new ReferenceFraction(first ? 1 : period)
        ).RoundExtendedBinary64();
        for (var i = (int)begin + 1; i < input.Length; i++)
            r[i] = (
                r[i - 1]!.Value * (new ReferenceFraction(1) - alpha) + input[i]!.Value * alpha
            ).RoundExtendedBinary64();
        return r;
    }

    internal static double?[][] Values(IReadOnlyList<Bar> bars, AlignedEmaMacd owner)
    {
        var input = bars.Select(b => (ReferenceFraction?)ReferenceFraction.FromDouble(b.Close))
            .ToArray();
        ReferenceFraction Alpha(int period, double fixedValue) =>
            owner.FixedCoefficients
                ? ReferenceFraction.FromDouble(fixedValue)
                : new ReferenceFraction(2) / new ReferenceFraction((long)period + 1);
        var fast = Average(
            input,
            owner.FirstPrice ? 0 : owner.SlowPeriod - owner.FastPeriod,
            owner.FastPeriod,
            owner.FirstPrice,
            Alpha(owner.FastPeriod, .15)
        );
        var slow = Average(
            input,
            0,
            owner.SlowPeriod,
            owner.FirstPrice,
            Alpha(owner.SlowPeriod, .075)
        );
        var start = (long)owner.SlowPeriod - 1 + owner.Suppression;
        var difference = new ReferenceFraction?[bars.Count];
        for (var i = start; i < bars.Count; i++)
            difference[(int)i] = (
                fast[(int)i]!.Value - slow[(int)i]!.Value
            ).RoundExtendedBinary64();
        var signal = Average(
            difference,
            start,
            owner.SignalPeriod,
            owner.FirstPrice,
            new ReferenceFraction(2) / new ReferenceFraction((long)owner.SignalPeriod + 1)
        );
        var first = start + owner.SignalPeriod - 1L + owner.Suppression;
        var result = new[]
        {
            new double?[bars.Count],
            new double?[bars.Count],
            new double?[bars.Count],
        };
        for (var i = first; i < bars.Count; i++)
        {
            var values = new[]
            {
                difference[(int)i]!.Value,
                signal[(int)i]!.Value,
                (difference[(int)i]!.Value - signal[(int)i]!.Value).RoundExtendedBinary64(),
            };
            for (var j = 0; j < 3; j++)
                if (((int)owner.Selection & (1 << j)) != 0)
                    result[j][(int)i] = values[j].ToDouble();
        }
        return result;
    }
}
