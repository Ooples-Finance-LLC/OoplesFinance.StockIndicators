using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AdaptiveZoneOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); return AdaptiveZoneOutputs(bars, Math.Max(1, Integer(options, "Length", 20)), AverageKind(options, 3), Number(options, 2, "Pct"));
    }
    internal static IReadOnlyDictionary<string, double[]> AdaptiveZoneOutputs(IReadOnlyList<Bar> bars, int length, int kind, double pct)
    {
        var period = Math.Max(2, Math.Min(530, (int)Math.Ceiling(Math.Sqrt(length))));
        var center = SmoothRocBankStage(SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(), period, kind), period, kind);
        var range = bars.Select(b => (ReferenceFraction.FromDouble(b.High) - ReferenceFraction.FromDouble(b.Low)).RoundExtendedBinary64()).ToArray();
        var width = SmoothRocBankStage(SmoothRocBankStage(range, period, kind), period, kind);
        var distance = width.Select(v => (v * ReferenceFraction.FromDouble(pct)).RoundExtendedBinary64()).ToArray();
        return Outputs(("UpperBand", center.Select((v, i) => (v + distance[i]).ToDouble()).ToArray()), ("MiddleBand", center.Select(v => v.ToDouble()).ToArray()), ("LowerBand", center.Select((v, i) => (v - distance[i]).ToDouble()).ToArray()));
    }
}
