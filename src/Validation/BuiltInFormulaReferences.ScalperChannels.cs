using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    // Independent high-precision logarithm versus platform log and exponent reduction.
    internal static IndicatorErrorBudget ScalperBudget { get; } = new(2e-12, 8e-15, true);
    internal static IReadOnlyDictionary<string, double[]> ScalperChannelOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return ScalperChannelOutputs(bars, Math.Max(1, Integer(options, "Length1", 15)), Math.Max(1, Integer(options, "Length2", 20)), AverageKind(options, 1)); }
    internal static IReadOnlyDictionary<string, double[]> ScalperChannelOutputs(IReadOnlyList<Bar> bars, int rangeLength, int averageLength, int kind, double[]? externalAverage = null, double[]? externalAtr = null)
    {
        rangeLength = Math.Max(1, rangeLength); averageLength = Math.Max(1, averageLength); var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var ranges = bars.Select((b, i) => { var high = ReferenceFraction.FromDouble(b.High); var low = ReferenceFraction.FromDouble(b.Low); var previous = prices[i == 0 ? 0 : i - 1]; return new[] { high - low, (high - previous).Abs(), (low - previous).Abs() }.Max().RoundExtendedBinary64(); }).ToArray();
        var average = externalAverage is null ? SmoothRocBankStage(prices, averageLength, kind) : externalAverage.Select(ReferenceFraction.FromDouble).ToArray();
        var atr = externalAtr is null ? SmoothRocBankStage(ranges, averageLength, kind) : externalAtr.Select(ReferenceFraction.FromDouble).ToArray();
        var upper = new double[bars.Count]; var lower = new double[bars.Count]; var middle = new double[bars.Count]; var scalper = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var window = bars.Skip(Math.Max(0, i - rangeLength + 1)).Take(Math.Min(rangeLength, i + 1)); upper[i] = window.Max(b => b.High); lower[i] = window.Min(b => b.Low);
            middle[i] = ((ReferenceFraction.FromDouble(upper[i]) + ReferenceFraction.FromDouble(lower[i])) / new ReferenceFraction(2)).ToDouble();
            var product = (atr[i] * ReferenceFraction.FromDouble(Math.PI)).RoundExtendedBinary64();
            scalper[i] = product.Sign <= 0 ? average[i].ToDouble() : (average[i] - ReferenceFraction.FromDouble(product.LogToDouble())).ToDouble();
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower), ("Scalper", scalper));
    }
}
