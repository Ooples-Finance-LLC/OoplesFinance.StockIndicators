using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> GainLossAverageOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return GainLossAverageOutputs(bars, Math.Max(1, Integer(options, "Length", 14)), Math.Max(1, Integer(options, "SignalLength", 7)), AverageKind(options, 6));
    }
    internal static IReadOnlyDictionary<string, double[]> GainLossAverageOutputs(IReadOnlyList<Bar> bars, int length, int signalLength, int kind)
    {
        var changes = new ReferenceFraction[bars.Count]; var zero = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close); var previous = i == 0 ? zero : ReferenceFraction.FromDouble(bars[i - 1].Close);
            var midpoint = (price + previous) / new ReferenceFraction(2);
            changes[i] = i == 0 || midpoint.Sign == 0 ? zero : ReferenceFraction.FromDouble(((price - previous) / midpoint * new ReferenceFraction(100)).ToDouble());
        }
        var average = SmoothRocBankStage(changes, length, kind); var signal = SmoothRocBankStage(average, signalLength, kind);
        return Outputs(("Glma", average.Select(value => value.ToDouble()).ToArray()), ("Signal", signal.Select(value => value.ToDouble()).ToArray()));
    }
}
