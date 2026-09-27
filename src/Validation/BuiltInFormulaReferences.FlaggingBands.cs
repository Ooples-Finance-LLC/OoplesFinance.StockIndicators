using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> FlaggingBandOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) => FlaggingBandOutputs(bars, Math.Max(1, Integer(indicator.CreateOptions(), "Length", 14)));
    internal static IReadOnlyDictionary<string, double[]> FlaggingBandOutputs(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(1, length); var deviations = PopulationDeviation(bars.Select(b => b.Close).ToArray(), length);
        var upper = new double[bars.Count]; var lower = new double[bars.Count]; var middle = new double[bars.Count]; var stops = new double[bars.Count]; var isLong = false;
        for (var i = 0; i < bars.Count; i++)
        {
            var close = bars[i].Close; var a = i == 0 ? close : upper[i - 1]; var b = i == 0 ? close : lower[i - 1];
            var olderA = i < 2 ? close : upper[i - 2]; var olderB = i < 2 ? close : lower[i - 2];
            var step = ReferenceFraction.FromDouble(deviations[i]) / new ReferenceFraction(length);
#pragma warning disable S1244 // Decay starts only when consecutive stored band values are exactly unchanged.
            upper[i] = close > a ? close : a == olderA ? Math.Max(close, (ReferenceFraction.FromDouble(a) - step).ToDouble()) : a;
#pragma warning restore S1244
#pragma warning disable S1244 // Decay starts only when consecutive stored band values are exactly unchanged.
            lower[i] = close < b ? close : b == olderB ? Math.Min(close, (ReferenceFraction.FromDouble(b) + step).ToDouble()) : b;
#pragma warning restore S1244
            isLong = close > olderA ? true : close < olderB ? false : isLong;
            var center = ReferenceFraction.FromDouble(((ReferenceFraction.FromDouble(upper[i]) + ReferenceFraction.FromDouble(lower[i])) / new ReferenceFraction(2)).ToDouble());
            middle[i] = ((ReferenceFraction.FromDouble(isLong ? upper[i] : lower[i]) + center) / new ReferenceFraction(2)).ToDouble(); stops[i] = isLong ? lower[i] : upper[i];
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower), ("TrailingStop", stops));
    }
}
