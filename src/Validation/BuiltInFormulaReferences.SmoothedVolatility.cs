using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SmoothedVolatilityOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); return SmoothedVolatilityOutputs(bars, Math.Max(1, Integer(options, "Length1", 20)), Math.Max(1, Integer(options, "Length2", 21)), AverageKind(options, 3), Number(options, 2.4, "Deviation"), Number(options, .9, "BandAdjust"));
    }
    internal static IReadOnlyDictionary<string, double[]> SmoothedVolatilityOutputs(IReadOnlyList<Bar> bars, int length1, int length2, int kind, double deviation, double adjustment)
    {
        var close = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var ranges = bars.Select((b, i) =>
        {
            var high = ReferenceFraction.FromDouble(b.High); var low = ReferenceFraction.FromDouble(b.Low); var previous = close[i == 0 ? 0 : i - 1];
            return new[] { high - low, (high - previous).Abs(), (low - previous).Abs() }.Max().RoundExtendedBinary64();
        }).ToArray();
        var atr = SmoothRocBankStage(ranges, 2 * length1 - 1, kind); var basis = SmoothRocBankStage(close, length1, kind); var middle = SmoothRocBankStage(close, length2, kind);
        var factor = ReferenceFraction.FromDouble(deviation); var adjust = ReferenceFraction.FromDouble(adjustment); var upper = new double[bars.Count]; var lower = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var offset = (atr[i] * factor).RoundExtendedBinary64(); var width = bars[i].Close == 0 ? new ReferenceFraction(0) : basis[i] * offset / close[i];
            upper[i] = (basis[i] + width).ToDouble(); lower[i] = (basis[i] - width * adjust).ToDouble();
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", middle.Select(v => v.ToDouble()).ToArray()), ("LowerBand", lower));
    }
}
