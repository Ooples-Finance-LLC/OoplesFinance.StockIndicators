using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AverageMoneyFlowOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return AverageMoneyFlowValues(bars, Integer(options, "Length", 5), 3, AverageKind(options, 2)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals, double[] Scaled) AverageMoneyFlowValues(IReadOnlyList<Bar> bars, int length, int smoothLength, int kind, double[][]? external = null)
    {
        length = Math.Max(1, length); smoothLength = Math.Max(1, smoothLength); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var volumes = external is null ? SmoothRocBankStage(bars.Select(b => R(b.Volume)).ToArray(), length, kind) : external[0].Select(R).ToArray();
        var differences = bars.Select((b, i) => i == 0 ? R(0) : (R(b.Close) - R(bars[i - 1].Close)).RoundExtendedBinary64()).ToArray();
        var changes = external is null ? SmoothRocBankStage(differences, length, kind) : external[1].Select(R).ToArray();
        var flows = changes.Select((v, i) => v.Sign == 0 || volumes[i].Sign == 0 ? 0 : (v * volumes[i]).Abs().LogToDouble() * v.Sign).ToArray();
        var scaled = flows.Select((v, i) =>
        {
            var window = Window(flows, i, length).ToArray(); var low = window.Min(); var high = window.Max();
            var percent = high == low ? 0 : (R(100) * (R(v) - R(low)) / (R(high) - R(low))).ToDouble(); return percent * 2 - 100; // NOSONAR: S1244 - Exact equality identifies a flat range and the defined zero denominator.
        }).ToArray();
        var values = external is null ? SmoothRocBankStage(scaled.Select(R).ToArray(), smoothLength, kind) : external[2].Select(R).ToArray();
        var signals = new Signal[bars.Count]; var previous = R(0);
        for (var i = 0; i < bars.Count; i++) { var value = values[i]; signals[i] = value.Sign > 0 && value.CompareTo(previous) > 0 ? Signal.StrongBuy : value.Sign < 0 && value.CompareTo(previous) < 0 ? Signal.StrongSell : value.Sign > 0 ? Signal.Buy : value.Sign < 0 ? Signal.Sell : Signal.None; previous = value; }
        return (new Dictionary<string, double[]> { ["Amfo"] = values.Select(v => v.ToDouble()).ToArray() }, signals, scaled);
    }
}
