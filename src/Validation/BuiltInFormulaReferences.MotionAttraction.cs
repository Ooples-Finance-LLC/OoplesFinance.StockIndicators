using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> MotionAttractionOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var values = MotionAttractionOutputs(bars, Math.Max(1, Integer(indicator.CreateOptions(), "Length", 14)));
        return indicator.BatchName == IndicatorName.MotionToAttractionTrailingStop ? Outputs(("Ts", values["Ts"])) : Outputs(("UpperBand", values["UpperBand"]), ("MiddleBand", values["MiddleBand"]), ("LowerBand", values["LowerBand"]));
    }
    internal static IReadOnlyDictionary<string, double[]> MotionAttractionOutputs(IReadOnlyList<Bar> bars, int length)
    {
        var zero = new ReferenceFraction(0); var one = new ReferenceFraction(1); var step = one / new ReferenceFraction(Math.Max(1, length));
        var a = zero; var b = zero; var previousUpper = zero; var previousLower = zero; var c = zero; var d = zero; var isLong = false;
        var upper = new double[bars.Count]; var middle = new double[bars.Count]; var lower = new double[bars.Count]; var stops = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var close = ReferenceFraction.FromDouble(bars[i].Close); if (i == 0) a = b = previousUpper = previousLower = close;
            var nextA = close.CompareTo(previousUpper) > 0 ? close : a; var nextB = close.CompareTo(previousLower) < 0 ? close : b;
            var aChanged = nextA.CompareTo(a) != 0; var bChanged = nextB.CompareTo(b) != 0;
            c = bChanged ? new[] { one, c + step }.Min() : aChanged ? zero : c;
            d = aChanged ? new[] { one, d + step }.Min() : bChanged ? zero : d;
            var center = ReferenceFraction.FromDouble(((nextA + nextB) / new ReferenceFraction(2)).ToDouble());
            upper[i] = (c * center + (one - c) * nextA).ToDouble(); lower[i] = (d * center + (one - d) * nextB).ToDouble();
            middle[i] = ((ReferenceFraction.FromDouble(upper[i]) + ReferenceFraction.FromDouble(lower[i])) / new ReferenceFraction(2)).ToDouble();
            isLong = close.CompareTo(previousUpper) > 0 ? true : close.CompareTo(previousLower) < 0 ? false : isLong;
            stops[i] = isLong ? lower[i] : upper[i]; a = nextA; b = nextB; previousUpper = ReferenceFraction.FromDouble(upper[i]); previousLower = ReferenceFraction.FromDouble(lower[i]);
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower), ("Ts", stops));
    }
}
