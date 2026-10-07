using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class SeededAdaptiveReference
{
    private static ReferenceFraction R(ReferenceFraction v) => v.RoundExtendedBinary64();

    internal static double?[][] Values(IReadOnlyList<Bar> bars, SeededAdaptiveAverage owner) =>
        ExtendedValues(bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(), owner)
            .Select(row => row.Select(v => v?.ToDouble()).ToArray())
            .ToArray();

    internal static ReferenceFraction?[][] ExtendedValues(
        IReadOnlyList<ReferenceFraction> prices,
        SeededAdaptiveAverage owner
    )
    {
        var r = Enumerable.Range(0, 2).Select(_ => new ReferenceFraction?[prices.Count]).ToArray();
        var fast = R(
            new ReferenceFraction(2)
                / new ReferenceFraction(
                    (long)(
                        owner.ClampFastPeriod
                            ? Math.Min(owner.Period, owner.FastPeriod)
                            : owner.FastPeriod
                    ) + 1
                )
        );
        var slow = R(new ReferenceFraction(2) / new ReferenceFraction((long)owner.SlowPeriod + 1));
        var average = new ReferenceFraction(0);
        for (var i = 0; i < prices.Count; i++)
        {
            if (i < owner.Period)
                average = prices[i];
            else
            {
                var total = new ReferenceFraction(0);
                for (var j = i - owner.Period + 1; j <= i; j++)
                    total += (prices[j] - prices[j - 1]).Abs();
                var efficiency =
                    total.Sign == 0
                        ? new ReferenceFraction(owner.FastFlat ? 1 : 0)
                        : R((prices[i] - prices[i - owner.Period]).Abs() / total);
                var rate = R(efficiency * (fast - slow) + slow);
                var square = R(rate * rate);
                average =
                    owner.ResetFlat && total.Sign == 0
                        ? prices[i]
                        : R(average + square * (prices[i] - average));
                r[1][i] = efficiency;
            }
            if ((long)i >= (owner.PublishStartup ? 0L : owner.Period - 1L) + owner.OutputDelay)
                r[0][i] = average;
        }
        return r;
    }
}
