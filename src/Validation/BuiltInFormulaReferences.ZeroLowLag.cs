using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ZeroLowLagOutputs(IReadOnlyList<Bar> bars, int length, double lag = 1.4)
    {
        length = Math.Max(1, length); var lookback = Math.Max(1, Math.Min(530, (int)Math.Ceiling(length / 2d)));
        var factor = ReferenceFraction.FromDouble(lag); var retained = ReferenceFraction.FromDouble(1 - lag);
        var a = new ReferenceFraction[bars.Count]; var b = new ReferenceFraction[bars.Count]; var zero = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close);
            var oldOutput = i < lookback ? price : b[i - lookback];
            var increment = RoundRocBankStage(RoundRocBankStage(factor * price) + RoundRocBankStage(retained * oldOutput));
            a[i] = RoundRocBankStage(increment + (i == 0 ? zero : a[i - 1]));
            var difference = RoundRocBankStage(a[i] - (i < length ? zero : a[i - length]));
            b[i] = RoundRocBankStage(difference / new ReferenceFraction(length));
        }
        return Outputs(("Zllma", b.Select(v => v.ToDouble()).ToArray()));
    }
}
