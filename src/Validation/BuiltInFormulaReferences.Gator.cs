using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> GatorOutputs(IReadOnlyList<Bar> bars, int jawLength = 13, int jawOffset = 8, int teethLength = 8, int teethOffset = 5, int lipsLength = 5, int lipsOffset = 3, int kind = 6, bool selected = false)
    {
        var prices = bars.Select(b => ReferenceFraction.FromDouble(selected ? b.Close : ((ReferenceFraction.FromDouble(b.High) + ReferenceFraction.FromDouble(b.Low)) / new ReferenceFraction(2)).ToDouble())).ToArray();
        double[] Line(int length, int delay)
        {
            delay = Math.Max(0, delay); var smoothed = SmoothRocBankStage(prices, Math.Max(1, length), kind);
            return Enumerable.Range(0, bars.Count).Select(i => i < delay ? 0 : smoothed[i - delay].ToDouble()).ToArray();
        }
        var jaw = Line(jawLength, jawOffset); var teeth = Line(teethLength, teethOffset); var lips = Line(lipsLength, lipsOffset);
        var top = jaw.Select((v, i) => (ReferenceFraction.FromDouble(v) - ReferenceFraction.FromDouble(teeth[i])).Abs().ToDouble()).ToArray();
        var bottom = teeth.Select((v, i) => -(ReferenceFraction.FromDouble(v) - ReferenceFraction.FromDouble(lips[i])).Abs().ToDouble()).ToArray();
        return Outputs(("Top", top), ("Bottom", bottom));
    }
}
