using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> JrcOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) =>
        JrcValues(bars, Integer(indicator.CreateOptions(), "Length1", 20), Integer(indicator.CreateOptions(), "Length2", 5),
            Integer(indicator.CreateOptions(), "SmoothLength", 5), AverageKind(indicator.CreateOptions(), 1), (indicator as IIndicator)?.Source is not null).Outputs;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals, double[] Raw) JrcValues(IReadOnlyList<Bar> bars,
        int length, int scale, int smoothing, int kind, bool selected = false, double[]? externalLine = null, double[]? externalSignal = null)
    {
        length = Math.Max(1, length); scale = Math.Max(1, scale); smoothing = Math.Max(1, smoothing);
        var aggregation = (int)Math.Max(2L, Math.Min(530L, (long)(scale - 1) * length));
        var longer = (int)Math.Max(2L, Math.Min(530L, (long)scale * length));
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var zero = R(0); var one = R(1); var two = R(2);
        var highs = bars.Select((b, i) => !selected || b.Close >= b.Low && b.Close <= b.High ? b.High : Math.Max(b.Close, bars[Math.Max(0, i - 1)].Close)).ToArray();
        var lows = bars.Select((b, i) => !selected || b.Close >= b.Low && b.Close <= b.High ? b.Low : Math.Min(b.Close, bars[Math.Max(0, i - 1)].Close)).ToArray();
        ReferenceFraction Range(int index, int period)
        {
            var start = (int)Math.Max(0L, index - (long)period + 1); var count = index - start + 1;
            var previous = index < period ? 0 : bars[index - period].Close;
            return R(Math.Max(previous, highs.Skip(start).Take(count).Max())) - R(Math.Min(previous, lows.Skip(start).Take(count).Min()));
        }
        double Log(ReferenceFraction ratio)
        {
            if (ratio.CompareTo(R(.5)) >= 0 && ratio.CompareTo(two) <= 0)
            {
                var relative = (ratio - one).ToDouble(); var argument = 1 + relative;
                return argument == 1 ? relative : (R(Math.Log(argument)) * R(relative) / R(argument - 1)).ToDouble(); // NOSONAR: S1244 - Detect exactly rounded 1+relative to avoid division by exactly zero; adjacent representable arguments must retain logarithm correction.
            }
            var exponent = 0;
            while (ratio.CompareTo(two) >= 0) { ratio /= two; exponent++; }
            while (ratio.CompareTo(one) < 0) { ratio *= two; exponent--; }
            // Platform log consumes a rounded normalized argument; the complete
            // exponent correction is rounded once. Exact rational log is tested separately.
            return (R(Math.Log(ratio.ToDouble())) + R(exponent) * R(Math.Log(2))).ToDouble();
        }
        var ranges = Enumerable.Range(0, bars.Count).Select(i => Range(i, length)).ToArray();
        var raw = Enumerable.Range(0, bars.Count).Select(i =>
        {
            if (scale == 1) return 0d;
            var sum = ranges.Skip(Math.Max(0, i - aggregation + 1)).Take(Math.Min(aggregation, i + 1)).Aggregate(zero, (s, v) => s + v);
            var big = Range(i, longer);
            return sum.Sign == 0 || big.Sign == 0 ? 2 : (two - R(Log(big * R(aggregation) / sum)) / R(Math.Log(scale))).ToDouble();
        }).ToArray();
        ReferenceFraction[] Mean(double[] values) => kind is 1 or 2 or 3 or 6
            ? SmoothRocBankStage(values.Select(R).ToArray(), smoothing, kind) : Average(values, smoothing, kind).Select(R).ToArray();
        var line = externalLine is null ? Mean(raw) : externalLine.Select(R).ToArray();
        var signal = externalSignal is null ? Mean(line.Select(v => v.ToDouble()).ToArray()) : externalSignal.Select(R).ToArray();
        var trades = new Signal[bars.Count]; var previous = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            var spread = line[i] - signal[i];
            trades[i] = spread.Sign < 0 && spread.CompareTo(previous) < 0 ? Signal.StrongBuy : spread.Sign > 0 && spread.CompareTo(previous) > 0 ? Signal.StrongSell
                : spread.Sign < 0 ? Signal.Buy : spread.Sign > 0 ? Signal.Sell : Signal.None;
            previous = spread;
        }
        return (new Dictionary<string, double[]> { ["Jrcfd"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = signal.Select(v => v.ToDouble()).ToArray() }, trades, raw);
    }
}
