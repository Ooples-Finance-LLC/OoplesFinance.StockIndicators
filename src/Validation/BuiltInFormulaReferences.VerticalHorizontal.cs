using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> VerticalHorizontalOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) =>
        VerticalHorizontalOutputs(bars, Math.Max(1, Integer(indicator.CreateOptions(), "Length", 18)), AverageKind(indicator.CreateOptions(), 2), 6);
    internal static IReadOnlyDictionary<string, double[]> VerticalHorizontalOutputs(IReadOnlyList<Bar> bars, int length, int kind, int signalLength)
    {
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(); var values = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var start = Math.Max(0, i - length + 1); var high = bars[i].Close; var low = high; var path = new ReferenceFraction(0);
            for (var j = start; j <= i; j++)
            {
                high = Math.Max(high, bars[j].Close); low = Math.Min(low, bars[j].Close);
                if (j > 0) { var difference = prices[j] - prices[j - 1]; path += difference.Sign < 0 ? new ReferenceFraction(0) - difference : difference; }
            }
            values[i] = path.Sign == 0 ? 0 : ((ReferenceFraction.FromDouble(high) - ReferenceFraction.FromDouble(low)) / path).ToDouble();
        }
        var signal = SmoothRocBankStage(values.Select(ReferenceFraction.FromDouble).ToArray(), signalLength, kind).Select(value => value.ToDouble()).ToArray();
        return Outputs(("Vhf", values), ("Signal", signal));
    }
}
