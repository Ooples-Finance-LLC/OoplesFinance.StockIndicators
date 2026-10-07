using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> UniChannelOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return UniChannelOutputs(bars, Math.Max(1, Integer(options, "Length", 10)), BoundedMeanKind(options, 1), Number(options, .02, "UbFac"), Number(options, .02, "LbFac"), options.GetType().GetProperty("Type1")?.GetValue(options) is true); }
    internal static IReadOnlyDictionary<string, double[]> UniChannelOutputs(IReadOnlyList<Bar> bars, int length, int kind, double upperFactor, double lowerFactor, bool additive, double[]? externalMean = null)
    {
        var middle = externalMean ?? RoundedBoundedStage(bars.Select(b => b.Close).ToArray(), Math.Max(1, length), kind); var upper = ReferenceFraction.FromDouble(upperFactor); var lower = ReferenceFraction.FromDouble(lowerFactor);
        return Outputs(("UpperBand", middle.Select(v => { var mean = ReferenceFraction.FromDouble(v); return (mean + (additive ? upper : mean * upper)).ToDouble(); }).ToArray()), ("MiddleBand", middle),
            ("LowerBand", middle.Select(v => { var mean = ReferenceFraction.FromDouble(v); return (mean - (additive ? lower : mean * lower)).ToDouble(); }).ToArray()));
    }
}
