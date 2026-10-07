using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class ClassicDeviationBandsReference
{
    internal static double?[][] Values(IReadOnlyList<Bar> bars, ClassicDeviationBands owner)
    {
        var center = ClassicAverageReference.ExtendedValues(bars, owner.Average);
        var result = Enumerable.Range(0, 3).Select(_ => new double?[bars.Count]).ToArray();
        for (var i = owner.Period - 1; i < bars.Count; i++)
        {
            if (!center[i].HasValue)
                continue;
            var values = bars.Skip(i - owner.Period + 1)
                .Take(owner.Period)
                .Select(b => ReferenceFraction.FromDouble(b.Close))
                .ToArray();
            var mean =
                values.Aggregate(new ReferenceFraction(0), (a, b) => a + b)
                / new ReferenceFraction(owner.Period);
            var variance =
                values.Aggregate(new ReferenceFraction(0), (a, b) => a + (b - mean) * (b - mean))
                / new ReferenceFraction(owner.Period);
            var deviation = ReferenceFraction.FromDouble(variance.SqrtToDouble());
            var upper = (
                deviation * ReferenceFraction.FromDouble(owner.UpperFactor)
            ).RoundExtendedBinary64();
            var lower = (
                deviation * ReferenceFraction.FromDouble(owner.LowerFactor)
            ).RoundExtendedBinary64();
            result[0][i] = (center[i]!.Value + upper).ToDouble();
            result[1][i] = center[i]!.Value.ToDouble();
            result[2][i] = (center[i]!.Value - lower).ToDouble();
        }
        return result;
    }
}
