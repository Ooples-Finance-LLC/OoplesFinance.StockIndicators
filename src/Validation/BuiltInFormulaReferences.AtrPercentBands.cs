using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AtrPercentBandOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); return AtrPercentBandOutputs(bars, Math.Max(1, Integer(options, "Length", 14)), Math.Max(1, Integer(options, "BbLength", 20)), AverageKind(options, 1), Number(options, 2, "StdDevMult"));
    }
    internal static IReadOnlyDictionary<string, double[]> AtrPercentBandOutputs(IReadOnlyList<Bar> bars, int length, int bbLength, int kind, double multiplier)
    {
        var basis = SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(), bbLength, kind);
        var upper = new double[bars.Count]; var lower = new double[bars.Count]; var percent = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var previous = ReferenceFraction.FromDouble(i == 0 ? 0 : bars[i - 1].Close); var high = ReferenceFraction.FromDouble(bars[i].High); var low = ReferenceFraction.FromDouble(bars[i].Low);
            var hc = (high - previous).Abs(); var lc = (low - previous).Abs(); var span = high - low;
            var largest = new[] { hc, lc, span }.Max(); var denominator = (largest.CompareTo(hc) == 0 ? previous : low) + largest / new ReferenceFraction(2);
            var ratio = denominator.CompareTo(new ReferenceFraction(0)) == 0 ? new ReferenceFraction(0) : (largest / denominator).RoundExtendedBinary64();
            percent = ((new ReferenceFraction(200) * ratio + new ReferenceFraction(length - 1) * percent) / new ReferenceFraction(length + 1L)).RoundExtendedBinary64();
            var deviation = (ReferenceFraction.FromDouble(multiplier) * percent).RoundExtendedBinary64();
            upper[i] = (basis[i] + basis[i] * deviation / new ReferenceFraction(100)).ToDouble();
            lower[i] = (basis[i] - basis[i] * deviation / new ReferenceFraction(100)).ToDouble();
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", basis.Select(v => v.ToDouble()).ToArray()), ("LowerBand", lower));
    }
}
