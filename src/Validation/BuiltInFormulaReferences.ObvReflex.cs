using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static readonly IndicatorErrorBudget ObvReflexBudget = new(0, 4e-15, requireSameSign: true);
    internal static Dictionary<string, double[]> ObvReflexOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return ObvReflexValues(bars, Integer(options, "Length", 4), Integer(options, "SignalLength", 14), AverageKind(options, 1)).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) ObvReflexValues(IReadOnlyList<Bar> bars,
        int length, int signalLength, int kind = 1, double[]? selected = null, double[]? externalSignal = null)
    {
        length = Math.Max(1, length); signalLength = Math.Max(1, signalLength);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var zero = R(0);
        ReferenceFraction[] Mean(ReferenceFraction[] values, int period) => DisparityReference.Mean(values, period, kind);
        var prices = (selected ?? Closes(bars)).Select(R).ToArray(); var line = new ReferenceFraction[bars.Count];
        var total = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            var prior = i < length ? zero : prices[i - length]; var direction = prices[i].CompareTo(prior);
            if (direction > 0) total += R(bars[i].Volume); else if (direction < 0) total -= R(bars[i].Volume);
            line[i] = total;
        }
        var signal = externalSignal?.Select(R).ToArray() ?? Mean(line, signalLength);
        var previous = zero; var trades = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var margin = line[i] - signal[i]; var change = margin - previous;
            trades[i] = margin.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : margin.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
                : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
            previous = margin;
        }
        return (new Dictionary<string, double[]> { ["Obvr"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = signal.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}
