using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedVidya(IReadOnlyList<Bar> bars, int length)
    {
        var momentum = RoundedChande(bars, length);
        var alpha = ReferenceFraction.FromDouble((new ReferenceFraction(2) / new ReferenceFraction(length + 1L)).ToDouble());
        var result = new double[bars.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var ratio = ReferenceFraction.FromDouble((ReferenceFraction.FromDouble(momentum[i]).Abs() / new ReferenceFraction(100)).ToDouble());
            var gain = ReferenceFraction.FromDouble((alpha * ratio).ToDouble());
            var previous = ReferenceFraction.FromDouble(i == 0 ? bars[i].Close : result[i - 1]);
            result[i] = ((new ReferenceFraction(1) - gain) * previous + gain * ReferenceFraction.FromDouble(bars[i].Close)).ToDouble();
        }
        return result;
    }
}
