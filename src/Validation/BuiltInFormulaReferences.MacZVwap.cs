using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> MacZVwapOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return MacZVwapValues(bars, Integer(o, "FastLength", 12), Integer(o, "SlowLength", 25), Integer(o, "SignalLength", 9), Integer(o, "Length1", 20), Integer(o, "Length2", 25), Number(o, .02, "Gamma"), AverageKind(o, 1)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) MacZVwapValues(IReadOnlyList<Bar> bars, int fast, int slow, int signal, int volumeLength, int deviationLength, double gamma, int kind)
    {
        fast = Math.Max(1, fast); slow = Math.Max(1, slow); signal = Math.Max(1, signal); volumeLength = Math.Max(1, volumeLength); deviationLength = Math.Max(1, deviationLength);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction Compact(ReferenceFraction value) => CompactReferenceFraction(value);
        ReferenceFraction[] Mean(ReferenceFraction[] values, int period, int meanKind)
        {
            if (meanKind is 4 or 5) return Average(values.Select(v => v.ToDouble()).ToArray(), period, meanKind).Select(R).ToArray();
            var result = new ReferenceFraction[values.Length]; var previous = R(0);
            for (var i = 0; i < values.Length; i++)
            {
                if (meanKind == 6) result[i] = (previous * R(period - 1) + values[i]) / R(period);
                else if (meanKind == 3 && i >= period) result[i] = (previous * R(period - 1) + R(2) * values[i]) / R(period + 1d);
                else if (meanKind == 1 && i + 1 < period) result[i] = R(0);
                else
                {
                    var total = R(0); var start = Math.Max(0, i - period + 1);
                    for (var j = start; j <= i; j++) total += values[j] * R(meanKind == 2 ? period - i + j : 1);
                    result[i] = total / (meanKind == 2 ? R(period) * R(period + 1d) / R(2) : R(i - start + 1));
                }
                previous = meanKind == 6 || meanKind == 3 && i >= period ? Compact(result[i]) : result[i];
            }
            return result;
        }
        ReferenceFraction Root(ReferenceFraction square) => RefinedReferenceRoot(square);
        var prices = bars.Select(b => R(b.Close)).ToArray(); var fastValues = Mean(prices, fast, kind); var slowValues = Mean(prices, slow, kind);
        var residuals = new ReferenceFraction[bars.Count]; var raw = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var volume = R(0); var product = R(0);
            for (var j = Math.Max(0, i - volumeLength + 1); j <= i; j++) { volume += R(bars[j].Volume); product += prices[j] * R(bars[j].Volume); }
            residuals[i] = prices[i] - (volume.Sign == 0 ? R(0) : product / volume);
            var energy = R(0);
            if (i + 1 >= volumeLength) for (var j = i - volumeLength + 1; j <= i; j++) energy += residuals[j] * residuals[j] / R(volumeLength);
            raw[i] = energy.Sign == 0 ? R(0) : Root(residuals[i] * residuals[i] / energy) * R(residuals[i].Sign);
            if (i + 1 < deviationLength) continue;
            var values = Window(prices, i, deviationLength).ToArray(); var mean = values.Aggregate(R(0), (a, b) => a + b) / R(deviationLength);
            var variance = values.Aggregate(R(0), (a, b) => a + (b - mean) * (b - mean)) / R(deviationLength);
            var difference = fastValues[i] - slowValues[i];
            if (variance.Sign != 0) raw[i] += Root(difference * difference / variance) * R(difference.Sign);
        }
        // Independent transfer equation: symmetric cubic numerator over (1-g*z)^4.
        // Extend the input and output with the initial observation before time zero.
        // Keeping the output itself avoids subtracting a fixed seed from a tiny tail.
        var line = new ReferenceFraction[bars.Count]; var history = new ReferenceFraction[bars.Count];
        var g = R(gamma); var g2 = g * g; var g3 = g2 * g; var g4 = g3 * g;
        var scale = (R(1) - g) * (R(1) - g) / R(6);
        var a = scale * (R(1) - g + g2); var b = scale * (R(2) - R(5) * g + R(2) * g2);
        ReferenceFraction X(int i) => i < 0 ? raw[0] : raw[i];
        ReferenceFraction Y(int i) => i < 0 ? raw[0] : history[i];
        for (var i = 0; i < bars.Count; i++)
        {
            line[i] = R(4) * g * Y(i - 1) - R(6) * g2 * Y(i - 2) + R(4) * g3 * Y(i - 3) - g4 * Y(i - 4)
                + a * (X(i) + X(i - 3)) + b * (X(i - 1) + X(i - 2));
            history[i] = gamma == 0 || gamma == 1 ? line[i] : Compact(line[i]); // NOSONAR: S1244 - Exact gamma0/1 select algebraic finite-memory/constant-pole cases; near0/1 values require full recurrence. Never replace with epsilon.
        }
        var signals = Mean(line, signal, kind); var histogram = line.Select((v, i) => v - signals[i]).ToArray(); var trades = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var value = histogram[i]; var previous = i == 0 ? R(0) : histogram[i - 1];
            trades[i] = value.Sign > 0 && value.CompareTo(previous) > 0 ? Signal.StrongBuy : value.Sign < 0 && value.CompareTo(previous) < 0 ? Signal.StrongSell
                : value.Sign > 0 ? Signal.Buy : value.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { ["Macz"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = signals.Select(v => v.ToDouble()).ToArray(), ["Histogram"] = histogram.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}
