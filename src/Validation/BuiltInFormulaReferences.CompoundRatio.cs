using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] CompoundRatioRaw(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(1, length);
        var ratio = length == 1 ? 1 : Math.Pow(length, 1d / (length - 1) - 1);
        var weights = Enumerable.Range(1, length).Select(power => ReferenceFraction.FromDouble(Math.Pow(1 + 2 * ratio, power))).ToArray();
        var denominator = weights.Aggregate(new ReferenceFraction(0), (a, b) => a + b); var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var top = new ReferenceFraction(0);
            for (var j = Math.Max(0, i - length + 1); j <= i; j++) top += ReferenceFraction.FromDouble(bars[j].Close) * weights[length - 1 - i + j];
            result[i] = (top / denominator).ToDouble();
        }
        return result;
    }
    internal static IReadOnlyDictionary<string, double[]> CompoundRatioOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Math.Max(1, Integer(options, "Length", 20)); var kind = AverageKind(options, 2);
        var raw = CompoundRatioRaw(bars, length).Select(ReferenceFraction.FromDouble).ToArray();
        var period = Math.Max(1, (int)Math.Round(Math.Sqrt(length)));
        return Outputs(("Crma", SmoothRocBankStage(raw, period, kind).Select(v => v.ToDouble()).ToArray()));
    }
}
