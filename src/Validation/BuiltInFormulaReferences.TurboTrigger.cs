using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TurboTriggerOutputs(IReadOnlyList<Bar> bars, int length, int smoothing = 2, int kind = 1)
    {
        length = Math.Max(1, length); smoothing = Math.Max(1, smoothing);
        ReferenceFraction[] Smooth(ReferenceFraction[] input, int period) => SmoothRocBankStage(input, period, kind);
        var close = Smooth(bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(), smoothing);
        var open = Smooth(bars.Select(b => ReferenceFraction.FromDouble(b.Open)).ToArray(), smoothing);
        var high = Smooth(bars.Select(b => ReferenceFraction.FromDouble(b.High)).ToArray(), smoothing);
        var low = Smooth(bars.Select(b => ReferenceFraction.FromDouble(b.Low)).ToArray(), smoothing);
        var center = Smooth(close.Select((v, i) => RoundRocBankStage((v + open[i]) / new ReferenceFraction(2))).ToArray(), length);
        var bull = Smooth(high.Select((v, i) => RoundRocBankStage(v - center[i])).ToArray(), length);
        var bear = Smooth(low.Select((v, i) => RoundRocBankStage(center[i] - v)).ToArray(), length);
        var trigger = Smooth(bull.Select((v, i) => RoundRocBankStage(v - bear[i])).ToArray(), length);
        return Outputs(("BullLine", bull.Select(v => v.ToDouble()).ToArray()), ("Trigger", trigger.Select(v => v.ToDouble()).ToArray()));
    }
}
