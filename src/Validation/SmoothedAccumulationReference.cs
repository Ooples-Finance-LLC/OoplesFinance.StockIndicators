using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class SmoothedAccumulationReference
{
    private static ReferenceFraction F(double v) => ReferenceFraction.FromDouble(v);

    private static ReferenceFraction?[] Average(ReferenceFraction[] input, int p, bool first)
    {
        var r = new ReferenceFraction?[input.Length];
        var begin = first ? 0 : p - 1;
        if (begin >= input.Length)
            return r;
        r[begin] = (
            input.Take(first ? 1 : p).Aggregate(new ReferenceFraction(0), (s, v) => s + v)
            / new ReferenceFraction(first ? 1 : p)
        ).RoundExtendedBinary64();
        for (var i = begin + 1; i < input.Length; i++)
            r[i] = (
                (
                    r[i - 1]!.Value * new ReferenceFraction(p - 1)
                    + input[i] * new ReferenceFraction(2)
                ) / new ReferenceFraction((long)p + 1)
            ).RoundExtendedBinary64();
        return r;
    }

    internal static double?[][] Values(
        IReadOnlyList<Bar> bars,
        SmoothedAccumulationOscillator owner
    )
    {
        var result = Enumerable.Range(0, 4).Select(_ => new double?[bars.Count]).ToArray();
        var lines = new ReferenceFraction[bars.Count];
        var total = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var b = bars[i];
            var range = F(b.High) - F(b.Low);
            var multiplier =
                (owner.FirstValue ? range.Sign > 0 : range.Sign != 0)
                    ? ((F(b.Close) - F(b.Low)) - (F(b.High) - F(b.Close))) / range
                    : new ReferenceFraction(0);
            var flow = (multiplier * F(b.Volume)).RoundExtendedBinary64();
            total += flow;
            lines[i] = total.RoundExtendedBinary64();
            if (owner.IncludeDetails)
            {
                result[1][i] = multiplier.ToDouble();
                result[2][i] = flow.ToDouble();
                result[3][i] = lines[i].ToDouble();
            }
        }
        var fast = Average(lines, owner.FastPeriod, owner.FirstValue);
        var slow = Average(lines, owner.SlowPeriod, owner.FirstValue);
        for (
            var i = (long)Math.Max(owner.FastPeriod, owner.SlowPeriod) - 1 + owner.Suppression;
            i < bars.Count;
            i++
        )
            result[0][(int)i] = (fast[(int)i]!.Value - slow[(int)i]!.Value).ToDouble();
        return result;
    }
}
