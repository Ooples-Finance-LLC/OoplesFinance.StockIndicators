using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> DoubleStochasticOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return DoubleStochasticValues(bars, Integer(o, "Length", 2), 3, 15, 3, AverageKind(o, 3)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) DoubleStochasticValues(IReadOnlyList<Bar> bars, int length1, int length2, int length3, int length4, int kind, double[][]? external = null)
    {
        length1 = Math.Max(1, length1); length2 = Math.Max(1, length2); length3 = Math.Max(1, length3); length4 = Math.Max(1, length4); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var low = bars.Select((_, i) => R(Window(bars, i, length1).Min(b => b.Low))).ToArray(); var high = bars.Select((_, i) => R(Window(bars, i, length1).Max(b => b.High))).ToArray();
        var numerator = bars.Select((b, i) => (R(b.Close) - low[i]).RoundExtendedBinary64()).ToArray(); var denominator = high.Select((v, i) => (v - low[i]).RoundExtendedBinary64()).ToArray();
        var firstNumerator = external is null ? SmoothRocBankStage(numerator, length2, kind) : external[0].Select(R).ToArray(); var firstDenominator = external is null ? SmoothRocBankStage(denominator, length2, kind) : external[1].Select(R).ToArray();
        var secondNumerator = external is null ? SmoothRocBankStage(firstNumerator, length3, kind) : external[2].Select(R).ToArray(); var secondDenominator = external is null ? SmoothRocBankStage(firstDenominator, length3, kind) : external[3].Select(R).ToArray();
        var line = secondNumerator.Select((v, i) => secondDenominator[i].Sign == 0 ? R(0) : R(Math.Max(0, Math.Min(100, (R(100) * v / secondDenominator[i]).ToDouble())))).ToArray();
        var signal = external is null ? SmoothRocBankStage(line, length4, kind) : external[4].Select(R).ToArray(); var signals = new Signal[bars.Count]; var previous = R(0); var previousSpread = R(0);
        for (var i = 0; i < bars.Count; i++) { var spread = line[i] - signal[i]; signals[i] = spread.Sign > 0 && spread.CompareTo(previousSpread) > 0 ? Signal.StrongBuy : spread.Sign < 0 && spread.CompareTo(previousSpread) < 0 ? Signal.StrongSell : spread.Sign > 0 || (previous.CompareTo(R(30)) < 0 && line[i].CompareTo(R(30)) > 0) ? Signal.Buy : spread.Sign < 0 || (previous.CompareTo(R(70)) > 0 && line[i].CompareTo(R(70)) < 0) ? Signal.Sell : Signal.None; previous = line[i]; previousSpread = spread; }
        return (new Dictionary<string, double[]> { ["Dss"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = signal.Select(v => v.ToDouble()).ToArray() }, signals);
    }
}
