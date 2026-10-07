using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> DynamicMomentumOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => DynamicMomentumValues(bars, AverageKind(indicator.CreateOptions(), 1), 5, 10, 14, 5, 30).Outputs;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals, int[] Periods, double[] Deviations) DynamicMomentumValues(IReadOnlyList<Bar> bars, int kind, int deviationLength, int averageLength, int baseline, int minimum, int maximum, double[]? external = null)
    {
        deviationLength = Math.Max(1, deviationLength); averageLength = Math.Max(1, averageLength); baseline = Math.Max(1, baseline); minimum = Math.Max(1, minimum); maximum = Math.Max(minimum, maximum);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var prices = bars.Select(b => R(b.Close)).ToArray();
        var deviations = prices.Select((_, i) =>
        {
            if (i + 1L < deviationLength) return 0;
            var window = prices.Skip(i + 1 - deviationLength).Take(deviationLength).ToArray(); var center = window.Aggregate(R(0), (sum, p) => sum + p) / R(deviationLength);
            return (window.Aggregate(R(0), (sum, p) => sum + (p - center) * (p - center)) / R(deviationLength)).SqrtToDouble();
        }).ToArray();
        var means = external ?? (kind is 1 or 2 or 3 or 6 ? SmoothRocBankStage(deviations.Select(R).ToArray(), averageLength, kind).Select(v => v.ToDouble()).ToArray() : Average(deviations, averageLength, kind));
        double Scale(double mean, double sigma) { var ratio = (R(mean) / R(sigma)).ToDouble(); return double.IsInfinity(ratio) ? ratio : (R(baseline) * R(ratio)).ToDouble(); }
        var periods = deviations.Select((sigma, i) =>
        {
            var raw = means[i] <= 0 ? baseline : sigma <= 0 ? maximum : Scale(means[i], sigma);
            var bounded = Math.Max(minimum, Math.Min(maximum, raw)); var nearest = Math.Round(bounded);
            // This tolerance is part of the existing discrete-period contract.
            if (Math.Abs(bounded - nearest) <= 1e-12 * Math.Max(1, bounded)) bounded = nearest;
            return (int)Math.Floor(bounded);
        }).ToArray();
        var changes = prices.Select((p, i) => i == 0 ? R(0) : (p - prices[i - 1]).RoundExtendedBinary64()).ToArray();
        var line = new double[bars.Count]; var signal = new double[bars.Count]; var histogram = new double[bars.Count]; var trades = new Signal[bars.Count]; var oldHistogram = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var start = (int)Math.Max(0L, i - (long)periods[i] + 1); var window = changes.Skip(start).Take(i - start + 1).ToArray();
            var up = window.Where(v => v.Sign > 0).Aggregate(R(0), (sum, v) => sum + v); var down = window.Where(v => v.Sign < 0).Aggregate(R(0), (sum, v) => sum - v);
            line[i] = down.Sign == 0 ? 100 : (R(100) * up / (up + down)).ToDouble();
            signal[i] = (line.Skip(start).Take(i - start + 1).Aggregate(R(0), (sum, v) => sum + R(v)) / R(i - start + 1)).ToDouble();
            var residual = R(line[i]) - R(signal[i]); histogram[i] = residual.ToDouble(); var previous = i == 0 ? 0 : line[i - 1];
            trades[i] = residual.Sign > 0 && residual.CompareTo(oldHistogram) > 0 ? Signal.StrongBuy : residual.Sign < 0 && residual.CompareTo(oldHistogram) < 0 ? Signal.StrongSell
                : residual.Sign > 0 || previous < 30 && line[i] > 30 ? Signal.Buy : residual.Sign < 0 || previous > 70 && line[i] < 70 ? Signal.Sell : Signal.None;
            oldHistogram = residual;
        }
        return (new Dictionary<string, double[]> { ["Dmi"] = line, ["Signal"] = signal, ["Histogram"] = histogram }, trades, periods, deviations);
    }
}
