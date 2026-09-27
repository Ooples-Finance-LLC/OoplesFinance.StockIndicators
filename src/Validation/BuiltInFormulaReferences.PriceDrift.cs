using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> PriceDriftOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return PriceDriftOutputs(bars, Math.Max(1, Integer(options, "Length", 100)), AverageKind(options, 6), indicator.BatchName == IndicatorName.PriceCurveChannel); }
    internal static IReadOnlyDictionary<string, double[]> PriceDriftOutputs(IReadOnlyList<Bar> bars, int length, int kind, bool curve, double[]? externalAtr = null)
    {
        length = Math.Max(1, length); var period = new ReferenceFraction(length);
        var ranges = bars.Select((b, i) => { var high = ReferenceFraction.FromDouble(b.High); var low = ReferenceFraction.FromDouble(b.Low); var previous = ReferenceFraction.FromDouble(bars[i == 0 ? 0 : i - 1].Close); return new[] { high - low, (high - previous).Abs(), (low - previous).Abs() }.Max().RoundExtendedBinary64(); }).ToArray();
        var atr = externalAtr is null ? SmoothRocBankStage(ranges, length, kind) : externalAtr.Select(ReferenceFraction.FromDouble).ToArray();
        var upper = new double[bars.Count]; var lower = new double[bars.Count]; var middle = new double[bars.Count];
        var rises = new List<int>(); var falls = new List<int>(); var sizeA = new ReferenceFraction(0); var sizeB = sizeA;
        for (var i = 0; i < bars.Count; i++)
        {
            var close = bars[i].Close; var previousUpper = i == 0 ? close : upper[i - 1]; var previousLower = i == 0 ? close : lower[i - 1];
            var rise = previousUpper > (i < 2 ? 0 : upper[i - 2]); var fall = previousLower < (i < 2 ? 0 : lower[i - 2]);
            if (i == 0) sizeA = sizeB = (atr[i] / period).RoundExtendedBinary64();
            if (rise) rises.Add(i); if (fall) falls.Add(i);
            if (rise || (curve && fall)) sizeA = atr[i]; if (fall || (curve && rise)) sizeB = atr[i];
            var ageA = curve ? i - rises.DefaultIfEmpty(-1).Last() + 1 : 1; var ageB = curve ? i - falls.DefaultIfEmpty(-1).Last() + 1 : 1;
            var divisor = curve ? period * period : period;
            upper[i] = Math.Max(close, (ReferenceFraction.FromDouble(Math.Max(close, previousUpper)) - sizeA * new ReferenceFraction(ageA) / divisor).ToDouble());
            lower[i] = Math.Min(close, (ReferenceFraction.FromDouble(Math.Min(close, previousLower)) + sizeB * new ReferenceFraction(ageB) / divisor).ToDouble());
            middle[i] = ((ReferenceFraction.FromDouble(upper[i]) + ReferenceFraction.FromDouble(lower[i])) / new ReferenceFraction(2)).ToDouble();
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower));
    }
}
