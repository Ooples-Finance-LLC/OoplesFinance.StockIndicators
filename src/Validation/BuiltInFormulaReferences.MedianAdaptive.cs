using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> MedianAdaptiveOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => MedianAdaptiveValues(bars, Integer(indicator.CreateOptions(), "Length", 39), Number(indicator.CreateOptions(), .002, "Threshold")).Outputs;

    // Exhaustive candidate enumeration, sorted trailing windows and rational
    // complete means; deliberately independent of production's binary search.
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals, int[] Periods, double[] Candidates) MedianAdaptiveValues(IReadOnlyList<Bar> bars, int length, double threshold)
    {
        length = Math.Max(1, length);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        ReferenceFraction Rounded(ReferenceFraction v) => R(v.ToDouble());
        var smooth = new ReferenceFraction[bars.Count]; var filters = new ReferenceFraction[bars.Count];
        var values = new double[bars.Count]; var candidates = new double[bars.Count]; var periods = new int[bars.Count]; var signals = new Signal[bars.Count];
        var previousCandidate = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            smooth[i] = Rounded(Enumerable.Range(0, Math.Min(4, i + 1)).Select(lag => R(bars[i-lag].Close) * R(lag is 0 or 3 ? 1 : 2)).Aggregate(R(0), (sum, v) => sum + v) / R(6));
            var period = length; var candidate = R(0); var error = R(.2);
            while (period > 0 && error.CompareTo(R(threshold)) > 0)
            {
                var count = Math.Min(period, i + 1); var sorted = Enumerable.Range(i - count + 1, count).Select(j => smooth[j]).OrderBy(v => v).ToArray();
                var median = Rounded((sorted[(count-1)/2] + sorted[count/2]) / R(2));
                candidate = Rounded((R(2) * smooth[i] + R(period - 1L) * previousCandidate) / R(period + 1L));
                if (median.Sign != 0) error = (median - candidate).Abs() / median.Abs();
                period -= 2;
            }
            previousCandidate = candidate; candidates[i] = candidate.ToDouble(); periods[i] = Math.Max(3, period);
            var previousFilter = i == 0 ? R(0) : filters[i-1];
            filters[i] = Rounded((R(2) * smooth[i] + R(periods[i] - 1L) * previousFilter) / R(periods[i] + 1L));
            values[i] = filters[i].ToDouble();
            var residual = R(bars[i].Close) - filters[i]; var old = i == 0 ? R(0) : R(bars[i-1].Close) - previousFilter;
            signals[i] = residual.Sign > 0 && residual.CompareTo(old) > 0 ? Signal.StrongBuy : residual.Sign < 0 && residual.CompareTo(old) < 0 ? Signal.StrongSell
                : residual.Sign > 0 ? Signal.Buy : residual.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { ["Maaf"] = values }, signals, periods, candidates);
    }
}
