using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RangeIndicatorOutputs(IReadOnlyList<Bar> bars, int length, int kind = 3)
    {
        length = Math.Max(1, length); var ratios = new ReferenceFraction[bars.Count]; var position = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var high = ReferenceFraction.FromDouble(bars[i].High); var low = ReferenceFraction.FromDouble(bars[i].Low);
            var close = ReferenceFraction.FromDouble(bars[i].Close); var previous = ReferenceFraction.FromDouble(bars[i == 0 ? 0 : i - 1].Close);
            var tr = high - low; var highGap = (high - previous).Abs(); var lowGap = (low - previous).Abs();
            if (highGap.CompareTo(tr) > 0) tr = highGap; if (lowGap.CompareTo(tr) > 0) tr = lowGap;
            tr = RoundRocBankStage(tr);
            ratios[i] = i > 0 && close.CompareTo(previous) > 0 ? RoundRocBankStage(tr / RoundRocBankStage(close - previous)) : tr;
            var upper = ratios[i]; var lower = ratios[i];
            for (var j = Math.Max(0, i - length + 1); j < i; j++)
            {
                if (ratios[j].CompareTo(upper) > 0) upper = ratios[j]; if (ratios[j].CompareTo(lower) < 0) lower = ratios[j];
            }
            position[i] = upper.CompareTo(lower) == 0 ? 0 : ((ratios[i] - lower) * new ReferenceFraction(100) / (upper - lower)).ToDouble();
        }
        return Outputs(("Tri", RoundedBoundedStage(position, length, kind)));
    }
}
