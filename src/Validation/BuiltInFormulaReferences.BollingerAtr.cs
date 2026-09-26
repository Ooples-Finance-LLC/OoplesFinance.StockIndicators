using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> BollingerAtrOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); return BollingerAtrOutputs(bars, Math.Max(1, Integer(options, "AtrLength", 22)), Math.Max(1, Integer(options, "Length", 55)), AverageKind(options, 1), Number(options, 2, "StdDevMult", "Multiplier"));
    }
    internal static IReadOnlyDictionary<string, double[]> BollingerAtrOutputs(IReadOnlyList<Bar> bars, int atrLength, int length, int kind, double multiplier)
    {
        var close = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var ranges = bars.Select((b, i) =>
        {
            var high = ReferenceFraction.FromDouble(b.High); var low = ReferenceFraction.FromDouble(b.Low); var previous = close[i == 0 ? 0 : i - 1];
            return new[] { high - low, (high - previous).Abs(), (low - previous).Abs() }.Max().RoundExtendedBinary64();
        }).ToArray();
        var atr = SmoothRocBankStage(ranges, atrLength, kind); var deviation = PopulationDeviation(bars.Select(b => b.Close).ToArray(), length); var factor = ReferenceFraction.FromDouble(multiplier);
        return Outputs(("AtrDev", atr.Select((v, i) =>
        {
            var width = new ReferenceFraction(2) * factor * ReferenceFraction.FromDouble(deviation[i]);
            return width.Sign == 0 ? 0 : (v / width).ToDouble();
        }).ToArray()));
    }
}
