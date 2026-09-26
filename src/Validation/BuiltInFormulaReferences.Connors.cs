using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ConnorsOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var kind = AverageKind(options, 6);
        var priceLength = Integer(options, "Length2", Integer(options, "Length", 3));
        var parts = ConnorsTrajectories(Closes(bars), Integer(options, "Length1", 2), priceLength, Integer(options, "Length3", 100), kind);
        if (indicator.BatchName == IndicatorName.ConnorsRelativeStrengthIndex) return parts;
        var line = parts["ConnorsRsi"]; var raw = new ReferenceFraction[line.Length];
        for (var i = 0; i < line.Length; i++)
        {
            var values = Window(line, i, priceLength).ToArray();
            var low = ReferenceFraction.FromDouble(values.Min()); var high = ReferenceFraction.FromDouble(values.Max());
            raw[i] = (high - low).Sign == 0 ? new ReferenceFraction(0) : ReferenceFraction.FromDouble((new ReferenceFraction(100) * (ReferenceFraction.FromDouble(line[i]) - low) / (high - low)).ToDouble());
        }
        var fast = SmoothStrengthStage(raw, Integer(options, "SmoothLength1", 3), kind);
        var signal = SmoothStrengthStage(fast, Integer(options, "SmoothLength2", 3), kind);
        return Outputs(("SaRsi", fast.Select(v => v.ToDouble()).ToArray()), ("Signal", signal.Select(v => v.ToDouble()).ToArray()));
    }
}
