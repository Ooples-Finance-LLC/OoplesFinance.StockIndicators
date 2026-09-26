using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RangeChannelOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var rounded = indicator.BatchName == IndicatorName.AverageTrueRangeChannel;
        return RangeChannelOutputs(bars, Math.Max(1, Integer(options, "Length", 14)), AverageKind(options, 1), Number(options, 2, rounded ? "Multiplier" : "AtrMult", "FibRatio3"), rounded);
    }
    internal static IReadOnlyDictionary<string, double[]> RangeChannelOutputs(IReadOnlyList<Bar> bars, int length, int kind, double multiplier, bool rounded)
    {
        var close = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var ranges = bars.Select((b, i) =>
        {
            var high = ReferenceFraction.FromDouble(b.High); var low = ReferenceFraction.FromDouble(b.Low); var previous = close[i == 0 ? 0 : i - 1];
            return new[] { high - low, (high - previous).Abs(), (low - previous).Abs() }.Max().RoundExtendedBinary64();
        }).ToArray();
        var center = SmoothRocBankStage(close, length, kind); var atr = SmoothRocBankStage(ranges, length, kind); var factor = ReferenceFraction.FromDouble(multiplier);
        ReferenceFraction Boundary(ReferenceFraction v)
        {
            if (!rounded) return v;
            var binary = v.RoundExtendedBinary64(); var published = binary.ToDouble();
            return double.IsInfinity(published) ? binary : ReferenceFraction.FromDouble(Math.Round(published));
        }
        var upper = center.Select((v, i) => Boundary((rounded ? close[i] : v) + factor * atr[i])).ToArray();
        var lower = center.Select((v, i) => Boundary((rounded ? close[i] : v) - factor * atr[i])).ToArray();
        var middle = rounded ? upper.Select((v, i) => ((v + lower[i]) / new ReferenceFraction(2)).ToDouble()).ToArray() : center.Select(v => v.ToDouble()).ToArray();
        var result = Outputs(("UpperBand", upper.Select(v => v.ToDouble()).ToArray()), ("MiddleBand", middle), ("LowerBand", lower.Select(v => v.ToDouble()).ToArray()));
        return rounded ? Outputs(("UpperBand", result["UpperBand"]), ("MiddleBand", middle), ("LowerBand", result["LowerBand"]), ("Sma", center.Select(v => v.ToDouble()).ToArray())) : result;
    }
}
