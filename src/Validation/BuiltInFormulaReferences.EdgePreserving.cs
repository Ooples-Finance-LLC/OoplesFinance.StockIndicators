using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> EdgePreservingOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => EdgePreservingValues(bars, AverageKind(indicator.CreateOptions(), 1), Integer(indicator.CreateOptions(), "Length", 200), 50).Outputs;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals, bool[] Restarts) EdgePreservingValues(IReadOnlyList<Bar> bars, int kind, int length, int smoothLength, double[]? external = null)
    {
        length = Math.Max(1, length); smoothLength = Math.Max(1, smoothLength);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var prices = bars.Select(b => R(b.Close)).ToArray();
        var means = external?.Select(R).ToArray() ?? (kind is 1 or 2 or 3 or 6 ? SmoothRocBankStage(prices, length, kind) : Average(Closes(bars), length, kind).Select(R).ToArray());
        var offsets = prices.Select((p, i) => (p - means[i]).RoundExtendedBinary64()).ToArray();
        var fitted = offsets.Select((_, i) =>
        {
            var start = (int)Math.Max(0L, i - (long)smoothLength + 1); var values = offsets.Skip(start).Take(i - start + 1).Select(v => v.Abs()).ToArray();
            if (values.Length == 1) return values[0];
            var center = R(values.Length - 1) / R(2); var average = values.Aggregate(R(0), (sum, v) => sum + v) / R(values.Length);
            var covariance = values.Select((v, j) => (R(j) - center) * (v - average)).Aggregate(R(0), (sum, v) => sum + v);
            var scatter = values.Select((_, j) => (R(j) - center) * (R(j) - center)).Aggregate(R(0), (sum, v) => sum + v);
            return (average + covariance / scatter * center).RoundExtendedBinary64();
        }).ToArray();
        var peaks = fitted.Select((p, i) =>
        {
            var start = (int)Math.Max(0L, i - (long)length + 1); var highest = fitted.Skip(start).Take(i - start + 1).Max();
            if (highest.Sign == 0) return false;
            var ratio = (p / highest).ToDouble(); return Math.Abs(ratio - 1) <= 1e-12;
        }).ToArray();
        var values = new double[bars.Count]; var trades = new Signal[bars.Count]; var restarts = new bool[bars.Count]; var segmentStart = 0; var seeded = true;
        for (var i = 0; i < bars.Count; i++)
        {
            restarts[i] = peaks[i] && (i == 0 || !peaks[i - 1]) && offsets[i].Sign != 0;
            if (restarts[i]) { segmentStart = i; seeded = false; }
            var observations = prices.Skip(segmentStart).Take(i - segmentStart + 1); if (seeded) observations = observations.Append(prices[0]);
            var segment = observations.ToArray(); values[i] = (segment.Aggregate(R(0), (sum, v) => sum + v) / R(segment.Length)).ToDouble();
            var residual = prices[i] - R(values[i]); var previous = i == 0 ? R(0) : prices[i - 1] - R(values[i - 1]);
            trades[i] = residual.Sign > 0 && residual.CompareTo(previous) > 0 ? Signal.StrongBuy : residual.Sign < 0 && residual.CompareTo(previous) < 0 ? Signal.StrongSell
                : residual.Sign > 0 ? Signal.Buy : residual.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { ["Epf"] = values }, trades, restarts);
    }
}
