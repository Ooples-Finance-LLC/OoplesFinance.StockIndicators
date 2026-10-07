using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> DEnvelopeOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) => DEnvelopeOutputs(bars, Math.Max(1, Integer(indicator.CreateOptions(), "Length", 20)), Number(indicator.CreateOptions(), 2, "DevFactor"));
    internal static IReadOnlyDictionary<string, double[]> DEnvelopeOutputs(IReadOnlyList<Bar> bars, int length, double multiplier)
    {
        length = Math.Max(1, length); var first = new ReferenceFraction(0); var second = first; var firstDeviation = first; var secondDeviation = first; var previous = first;
        var factor = ReferenceFraction.FromDouble(multiplier); var upper = new double[bars.Count]; var middle = new double[bars.Count]; var lower = new double[bars.Count];
        ReferenceFraction Smooth(ReferenceFraction value, ReferenceFraction prior) => ((new ReferenceFraction(2) * value + new ReferenceFraction(length - 1) * prior) / new ReferenceFraction(length + 1L)).RoundExtendedBinary64();
        ReferenceFraction Correct(ReferenceFraction a, ReferenceFraction b) => ((new ReferenceFraction(2L * length) * a - new ReferenceFraction(length + 1L) * b) / new ReferenceFraction(length - 1)).RoundExtendedBinary64();
        for (var i = 0; i < bars.Count; i++)
        {
            var close = ReferenceFraction.FromDouble(bars[i].Close); first = Smooth(close, first); second = Smooth(first, second);
            var center = length == 1 ? (new ReferenceFraction(2) * close - previous).RoundExtendedBinary64() : Correct(first, second);
            var distance = (close - center).Abs().RoundExtendedBinary64(); var previousDeviation = firstDeviation;
            firstDeviation = Smooth(distance, firstDeviation); secondDeviation = Smooth(firstDeviation, secondDeviation);
            var width = length == 1 ? (new ReferenceFraction(2) * distance - previousDeviation).RoundExtendedBinary64() : Correct(firstDeviation, secondDeviation);
            if (width.Sign < 0) width = new ReferenceFraction(0);
            upper[i] = (center + factor * width).ToDouble(); middle[i] = center.ToDouble(); lower[i] = (center - factor * width).ToDouble(); previous = close;
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower));
    }
}
