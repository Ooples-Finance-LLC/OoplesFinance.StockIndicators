using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SmartEnvelopeOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) => SmartEnvelopeOutputs(bars, Math.Max(1, Integer(indicator.CreateOptions(), "Length", 14)), Number(indicator.CreateOptions(), 1, "Factor"));
    internal static IReadOnlyDictionary<string, double[]> SmartEnvelopeOutputs(IReadOnlyList<Bar> bars, int length, double multiplier)
    {
        var divisor = new ReferenceFraction(Math.Max(1, length)); var factor = ReferenceFraction.FromDouble(multiplier);
        var previousUpper = new ReferenceFraction(0); var previousLower = previousUpper; var previousClose = previousUpper; var upperSignal = previousUpper; var lowerSignal = previousUpper;
        var upper = new double[bars.Count]; var middle = new double[bars.Count]; var lower = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var close = ReferenceFraction.FromDouble(bars[i].Close); if (i == 0) previousUpper = previousLower = close;
            var change = i == 0 ? new ReferenceFraction(0) : (close - previousClose).Abs();
            var upperDistance = new[] { (close - previousUpper).Abs(), change }.Min(); var lowerDistance = new[] { (close - previousLower).Abs(), change }.Min();
            var nextUpper = (new[] { close, previousUpper }.Max() - upperDistance / divisor * upperSignal).RoundExtendedBinary64();
            var nextLower = (new[] { close, previousLower }.Min() + lowerDistance / divisor * lowerSignal).RoundExtendedBinary64();
            upperSignal = nextLower.CompareTo(previousLower) < 0 ? new ReferenceFraction(-1) * factor : factor; lowerSignal = nextUpper.CompareTo(previousUpper) > 0 ? new ReferenceFraction(-1) * factor : factor;
            upper[i] = nextUpper.ToDouble(); lower[i] = nextLower.ToDouble(); middle[i] = ((nextUpper + nextLower) / new ReferenceFraction(2)).ToDouble();
            previousUpper = nextUpper; previousLower = nextLower; previousClose = close;
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower));
    }
}
