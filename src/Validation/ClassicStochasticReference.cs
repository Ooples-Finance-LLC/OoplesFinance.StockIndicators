using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class ClassicStochasticReference
{
    internal static double?[][] Values(IReadOnlyList<Bar> bars, ClassicStochastic owner)
    {
        var result = Enumerable.Range(0, 2).Select(_ => new double?[bars.Count]).ToArray();
        if (owner.First >= bars.Count)
            return result;
        var raw = new List<ReferenceFraction>();
        for (var i = owner.Period - 1; i < bars.Count; i++)
        {
            var window = bars.Skip(i - owner.Period + 1).Take(owner.Period).ToArray();
            var high = ReferenceFraction.FromDouble(window.Max(b => b.High));
            var low = ReferenceFraction.FromDouble(window.Min(b => b.Low));
            raw.Add(
                (high - low).Sign == 0
                    ? new ReferenceFraction(0)
                    : (
                        new ReferenceFraction(100)
                        * (ReferenceFraction.FromDouble(bars[i].Close) - low)
                        / (high - low)
                    ).RoundExtendedBinary64()
            );
        }
        var k = ClassicAverageReference.ExtendedValues(raw, owner.KAverage);
        var start = (int)owner.KAverage.First;
        var d = ClassicAverageReference.ExtendedValues(
            k.Skip(start).Select(v => v!.Value).ToArray(),
            owner.DAverage
        );
        for (var j = 0; j < d.Length; j++)
        {
            if (!d[j].HasValue)
                continue;
            var i = owner.Period - 1 + start + j;
            result[0][i] = k[start + j]!.Value.ToDouble();
            result[1][i] = d[j]!.Value.ToDouble();
        }
        return result;
    }
}
