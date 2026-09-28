using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] RecursiveMedianValues(IReadOnlyList<Bar> bars, int length, int smoothing = 12)
    {
        length = Math.Max(1, length); var angle = Math.Min(.99, Math.Max(.01, 2 * Math.PI / Math.Max(1, smoothing)));
        var gain = ReferenceFraction.FromDouble((Math.Cos(angle) + Math.Sin(angle) - 1) / Math.Cos(angle));
        var previous = new ReferenceFraction(0); var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var sorted = bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(length, i + 1)).Select(b => b.Close).OrderBy(v => v).ToArray();
            var midpoint = ((ReferenceFraction.FromDouble(sorted[(sorted.Length - 1) / 2]) + ReferenceFraction.FromDouble(sorted[sorted.Length / 2])) / new ReferenceFraction(2)).ToDouble();
            result[i] = (gain * ReferenceFraction.FromDouble(midpoint) + (new ReferenceFraction(1) - gain) * previous).ToDouble();
            previous = ReferenceFraction.FromDouble(result[i]);
        }
        return result;
    }
}
