using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AbsoluteStrengthMtfOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return AbsoluteStrengthMtfOutputs(bars, Math.Max(1, Integer(options, "Length", 50)), Math.Max(1, Integer(options, "SmoothLength", 25)), AverageKind(options, 1));
    }
    internal static IReadOnlyDictionary<string, double[]> AbsoluteStrengthMtfOutputs(IReadOnlyList<Bar> bars, int length, int smoothLength, int kind)
    {
        var zero = new ReferenceFraction(0);
        var current = SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(), length, kind);
        var previous = SmoothRocBankStage(bars.Select((_, i) => i == 0 ? zero : ReferenceFraction.FromDouble(bars[i - 1].Close)).ToArray(), length, kind);
        var changes = current.Select((value, i) => (value - previous[i]).RoundExtendedBinary64()).ToArray();
        var bulls = SmoothRocBankStage(changes.Select(value => value.Sign > 0 ? value : zero).ToArray(), smoothLength, kind);
        var bears = SmoothRocBankStage(changes.Select(value => value.Sign < 0 ? zero - value : zero).ToArray(), smoothLength, kind);
        return Outputs(("Bulls", bulls.Select(value => value.ToDouble()).ToArray()), ("Bears", bears.Select(value => value.ToDouble()).ToArray()));
    }
}
