using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> BilateralOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return BilateralValues(bars, Integer(o, "Length", 100), 20, AverageKind(o, 1)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) BilateralValues(IReadOnlyList<Bar> bars, int length, int signalLength, int kind, double[][]? external = null)
    {
        length = Math.Max(1, length); signalLength = Math.Max(1, signalLength); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var mean = external is null ? SmoothRocBankStage(bars.Select(b => R(b.Close)).ToArray(), length, kind) : external[0].Select(R).ToArray();
        var highs = mean.Select((_, i) => Window(mean, i, length).Max()).ToArray(); var lows = mean.Select((_, i) => Window(mean, i, length).Min()).ToArray(); var ranges = highs.Select((v, i) => (v - lows[i]).RoundExtendedBinary64()).ToArray();
        var scale = external is null ? SmoothRocBankStage(ranges, length, kind) : external[1].Select(R).ToArray();
        var bull = mean.Select((v, i) => scale[i].Sign == 0 ? R(0) : ((v - lows[i]) / scale[i]).RoundExtendedBinary64()).ToArray();
        var bear = mean.Select((v, i) => scale[i].Sign == 0 ? R(0) : ((v - highs[i]) / scale[i]).Abs().RoundExtendedBinary64()).ToArray(); var line = bull.Select((v, i) => v.CompareTo(bear[i]) > 0 ? v : bear[i]).ToArray();
        var signal = external is null ? SmoothRocBankStage(line, signalLength, kind) : external[2].Select(R).ToArray(); var signals = bull.Select((v, i) => v.CompareTo(bear[i]) > 0 || v.CompareTo(signal[i]) > 0 ? Signal.Buy : bear[i].CompareTo(v) > 0 || v.CompareTo(signal[i]) < 0 ? Signal.Sell : Signal.None).ToArray();
        return (new Dictionary<string, double[]> { ["Bull"] = bull.Select(v => v.ToDouble()).ToArray(), ["Bear"] = bear.Select(v => v.ToDouble()).ToArray(), ["Bso"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = signal.Select(v => v.ToDouble()).ToArray() }, signals);
    }
}
