using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> StochasticRsiOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var kind = AverageKind(options, 6);
        var rsiLength = Integer(options, "RsiLength", Integer(options, "Length", 14));
        var stochLength = Integer(options, "StochLength", rsiLength);
        var rsi = RoundedPriceRsi(bars, rsiLength, kind);
        var raw = new ReferenceFraction[bars.Count];
        for (var i = 0; i < raw.Length; i++)
        {
            var values = Window(rsi, i, stochLength).ToArray();
            var low = ReferenceFraction.FromDouble(values.Min()); var high = ReferenceFraction.FromDouble(values.Max());
            raw[i] = (high - low).Sign == 0 ? new ReferenceFraction(0) : ReferenceFraction.FromDouble((new ReferenceFraction(100) * (ReferenceFraction.FromDouble(rsi[i]) - low) / (high - low)).ToDouble());
        }
        var line = SmoothStrengthStage(raw, Integer(options, "SmoothLength1", 3), kind);
        var signal = SmoothStrengthStage(line, Integer(options, "SmoothLength2", 3), kind);
        return Outputs(("StochRsi", line.Select(v => v.ToDouble()).ToArray()), ("Signal", signal.Select(v => v.ToDouble()).ToArray()));
    }
}
