using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static readonly IndicatorErrorBudget MacZBudget = new(4e-15, 4e-15, requireSameSign: true);
    internal static IReadOnlyDictionary<string, double[]> MacZOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return MacZValues(bars, Integer(o, "FastLength", 12), Integer(o, "SlowLength", 25), Integer(o, "SignalLength", 9), Integer(o, "Length", 25), Number(o, 1, "Mult"), AverageKind(o, 1)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) MacZValues(IReadOnlyList<Bar> bars, int fast, int slow, int signal, int length, double mult, int kind)
    {
        fast = Math.Max(1, fast); slow = Math.Max(1, slow); signal = Math.Max(1, signal); length = Math.Max(1, length);
        // Every deviation is zero on an entirely constant trajectory. This
        // exact branch avoids growing irrelevant Wilder fractions for the
        // 4,000-bar settled-flat fixtures; production still runs every bar.
        if (bars.Count == 0 || bars.All(b => b.Close == bars[0].Close)) // NOSONAR: S1244 - Exact flat trajectory shortcut; even one-ULP movement must not be classified flat.
            return (new Dictionary<string, double[]> { ["Macz"] = new double[bars.Count], ["Signal"] = new double[bars.Count], ["Histogram"] = new double[bars.Count] }, new Signal[bars.Count]);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
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
                previous = result[i];
            }
            return result;
        }
        ReferenceFraction Root(ReferenceFraction square)
        {
            if (square.Sign == 0) return R(0);
            // Normalize the validation fraction, then correct a binary64 root
            // with its exact rational residual. Independent of the production
            // integer-root and 106-bit rounding algorithm.
            var factor = new ReferenceFraction(BigInteger.One << 512); var scale = R(1); var estimate = square.ToDouble();
            while (double.IsInfinity(estimate) || estimate >= Math.Pow(2, 512)) { square /= factor * factor; scale *= factor; estimate = square.ToDouble(); }
            while (estimate < Math.Pow(2, -512)) { square *= factor * factor; scale /= factor; estimate = square.ToDouble(); }
            var high = Math.Sqrt(estimate);
            var seed = R(high); var correction = (square - seed * seed) / (R(2) * seed);
            var improved = seed + correction;
            var refined = (improved + square / improved) / R(2);
            var low = (refined - seed).ToDouble();
            return (seed + R(low)) * scale;
        }
        var prices = bars.Select(b => R(b.Close)).ToArray(); var fastValues = Mean(prices, fast, kind); var slowValues = Mean(prices, slow, kind); var wilder = Mean(prices, length, 6);
        var line = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            line[i] = R(0); if (i + 1 < length) continue;
            var values = Window(prices, i, length).ToArray(); var mean = values.Aggregate(R(0), (a, b) => a + b) / R(length);
            var variance = values.Aggregate(R(0), (a, b) => a + (b - mean) * (b - mean)) / R(length);
            if (variance.Sign == 0) continue;
            var numerator = R(mult) * (prices[i] - wilder[i] + fastValues[i] - slowValues[i]);
            line[i] = Root(numerator * numerator / variance) * R(numerator.Sign); // NOSONAR: S4143 - The initialized zero is retained on early-continue paths; this assignment handles the remaining nonzero domain.
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
