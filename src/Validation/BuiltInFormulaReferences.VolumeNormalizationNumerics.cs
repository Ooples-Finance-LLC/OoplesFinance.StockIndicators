using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedNetVolume(IReadOnlyList<Bar> bars)
        => bars.Select((b, i) => i == 0 ? 0 : (ReferenceFraction.FromDouble(b.Close)
            - ReferenceFraction.FromDouble(bars[i - 1].Close)).Sign * b.Volume).ToArray();

    internal static double[] RoundedNormalizedVolume(IReadOnlyList<Bar> bars, int length)
        => bars.Select((b, i) =>
        {
            if (i + 1 < length) return 0;
            var sum = new ReferenceFraction(0);
            for (var j = i - length + 1; j <= i; j++) sum += ReferenceFraction.FromDouble(bars[j].Volume);
            return sum.Sign == 0 ? 0 : (new ReferenceFraction(length) * ReferenceFraction.FromDouble(b.Volume) / sum).ToDouble();
        }).ToArray();
}
