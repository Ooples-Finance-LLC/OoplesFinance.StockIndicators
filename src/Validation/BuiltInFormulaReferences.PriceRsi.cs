using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static double[] RoundedPriceRsi(IReadOnlyList<Bar> bars, int length, int kind)
    {
        var zero = new ReferenceFraction(0);
        var changes = bars.Select((b, i) => i == 0 ? zero : RoundStrengthStage(ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(bars[i - 1].Close))).ToArray();
        var up = SmoothStrengthStage(changes.Select(v => v.Sign > 0 ? v : zero).ToArray(), length, kind);
        var down = SmoothStrengthStage(changes.Select(v => v.Sign < 0 ? zero - v : zero).ToArray(), length, kind);
        var result = new double[bars.Count];
        for (var i = 0; i < result.Length; i++)
            result[i] = i > 0 && length > 1 && kind is 3 or 6 && bars[i].Close == bars[i - 1].Close ? result[i - 1]
                : (up[i] + down[i]).Sign == 0 ? 100 : (new ReferenceFraction(100) * up[i] / (up[i] + down[i])).ToDouble();
        return result;
    }
    internal static IReadOnlyDictionary<string, double[]> PriceRsiOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var kind = AverageKind(options, 6);
        var line = RoundedPriceRsi(bars, Integer(options, "Length", 14), kind);
        var signal = SmoothStrengthStage(line.Select(ReferenceFraction.FromDouble).ToArray(), Integer(options, "SignalLength", 3), kind).Select(v => v.ToDouble()).ToArray();
        var histogram = line.Select((v, i) => (ReferenceFraction.FromDouble(v) - ReferenceFraction.FromDouble(signal[i])).ToDouble()).ToArray();
        return Outputs(("Rsi", line), ("Signal", signal), ("Histogram", histogram));
    }
}
