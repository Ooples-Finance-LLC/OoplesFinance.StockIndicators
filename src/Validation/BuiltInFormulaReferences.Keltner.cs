using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> KeltnerOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Math.Max(1, Integer(options, "Length", 20)); var width = indicator.BatchName == IndicatorName.KeltnerChannelWidth;
        var all = KeltnerOutputs(bars, width ? length : Math.Max(1, Integer(options, "Length1", length)), width ? length : Math.Max(1, Integer(options, "Length2", 10)), width ? 3 : AverageKind(options, 3), Number(options, 2, width ? "Multiplier" : "MultFactor"));
        return width ? Outputs(("Kcw", all["Kcw"])) : Outputs(("UpperBand", all["UpperBand"]), ("MiddleBand", all["MiddleBand"]), ("LowerBand", all["LowerBand"]));
    }
    internal static IReadOnlyDictionary<string, double[]> KeltnerOutputs(IReadOnlyList<Bar> bars, int centerLength, int rangeLength, int kind, double multiplier, int rangeKind = 6)
    {
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var ranges = bars.Select((b, i) =>
        {
            var high = ReferenceFraction.FromDouble(b.High); var low = ReferenceFraction.FromDouble(b.Low); var previous = prices[i == 0 ? 0 : i - 1];
            return new[] { high - low, (high - previous).Abs(), (low - previous).Abs() }.Max().RoundExtendedBinary64();
        }).ToArray();
        var middle = SmoothRocBankStage(prices, centerLength, kind); var atr = SmoothRocBankStage(ranges, rangeLength, rangeKind); var factor = ReferenceFraction.FromDouble(multiplier);
        return Outputs(("UpperBand", middle.Select((v, i) => (v + factor * atr[i]).ToDouble()).ToArray()), ("MiddleBand", middle.Select(v => v.ToDouble()).ToArray()),
            ("LowerBand", middle.Select((v, i) => (v - factor * atr[i]).ToDouble()).ToArray()), ("Kcw", middle.Select((v, i) => v.Sign == 0 ? 0 : (new ReferenceFraction(200) * factor * atr[i] / v).ToDouble()).ToArray()));
    }
}
