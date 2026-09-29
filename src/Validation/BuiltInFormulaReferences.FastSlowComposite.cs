using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> FastSlowCompositeOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var rsi = indicator.BatchName == IndicatorName.FastandSlowRelativeStrengthIndexOscillator; return FastSlowCompositeValues(bars, rsi, 3, 6, 9, rsi ? 6 : 9, 2).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) FastSlowCompositeValues(IReadOnlyList<Bar> bars, bool rsi, int length1, int length2, int length3, int length4, int kind, double[][]? external = null)
    {
        length1 = Math.Max(1, length1); length2 = Math.Max(1, length2); length3 = Math.Max(1, length3); length4 = Math.Max(1, length4); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var changes = bars.Select((b, i) => i < length1 ? R(0) : (R(b.Close) - R(bars[i - length1].Close)).RoundExtendedBinary64()).ToArray(); var momentum = new ReferenceFraction[bars.Count]; var previous = R(0);
        for (var i = 0; i < bars.Count; i++) { var difference = (changes[i] - (i == 0 ? R(0) : changes[i - 1])).RoundExtendedBinary64(); previous = ((difference * R(.03)).RoundExtendedBinary64() + (previous * R(1 - .03)).RoundExtendedBinary64()).RoundExtendedBinary64(); momentum[i] = previous; }
        var velocity = external is null ? SmoothRocBankStage(momentum, length2, kind) : external[0].Select(R).ToArray(); ReferenceFraction[] level;
        if (external is not null) level = external[1].Select(R).ToArray();
        else if (rsi) level = RoundedPriceRsi(bars, length3, kind).Select(R).ToArray();
        else
        {
            var raw = bars.Select((b, i) => { var window = Window(bars, i, length3).ToArray(); var low = R(window.Min(v => v.Low)); var high = R(window.Max(v => v.High)); return high.CompareTo(low) == 0 ? R(0) : R(Math.Max(0, Math.Min(100, (R(100) * (R(b.Close) - low) / (high - low)).ToDouble()))); }).ToArray(); level = SmoothRocBankStage(raw, length3, kind);
        }
        var line = velocity.Select((v, i) => ((v * R(rsi ? 10000 : 500)).RoundExtendedBinary64() + level[i]).RoundExtendedBinary64()).ToArray(); var signal = external is null ? SmoothRocBankStage(line, length4, kind) : external[2].Select(R).ToArray(); var signals = new Signal[bars.Count]; var previousSpread = R(0);
        for (var i = 0; i < bars.Count; i++) { var spread = line[i] - signal[i]; signals[i] = spread.Sign > 0 && spread.CompareTo(previousSpread) > 0 ? Signal.StrongBuy : spread.Sign < 0 && spread.CompareTo(previousSpread) < 0 ? Signal.StrongSell : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None; previousSpread = spread; }
        return (new Dictionary<string, double[]> { [rsi ? "Fsrsi" : "Fsst"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = signal.Select(v => v.ToDouble()).ToArray() }, signals);
    }
}
