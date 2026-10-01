using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double KasePeakV2LogReference(double current, double previous)
    {
        if (current == 0 || previous == 0 || Math.Sign(current) != Math.Sign(previous)) return 0;
        current = Math.Abs(current); previous = Math.Abs(previous);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var ratio = R(current) / R(previous); var rounded = ratio.ToDouble();
        if (rounded >= .5 && rounded <= 2)
        {
            var relative = ((R(current) - R(previous)) / R(previous)).ToDouble();
            if (Math.Abs(relative) <= 1e-8) return ratio.LogToDouble();
            var argument = 1 + relative;
            return argument == 1 ? relative : (R(Math.Log(argument)) * R((R(relative) / R(argument - 1)).ToDouble())).ToDouble();
        }
        (double Mantissa, int Exponent) Normalize(double value)
        { var exponent = 0; while (value < 1) { value *= 2; exponent--; } while (value >= 2) { value /= 2; exponent++; } return (value, exponent); }
        var a = Normalize(current); var b = Normalize(previous);
        return Math.Log((R(a.Mantissa) / R(b.Mantissa)).ToDouble()) + (a.Exponent - b.Exponent) * Math.Log(2);
    }
    internal static IReadOnlyDictionary<string, double[]> KasePeakV2Outputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); return Outputs(("Kpo", KasePeakV2Values(bars, 8, 65, 9, Integer(options, "Length", 30), 3, 40, AverageKind(options, 1), (indicator as IIndicator)?.Source is not null).Values));
    }
    internal static (double[] Values, Signal[] Trades, double[] Deviation) KasePeakV2Values(IReadOnlyList<Bar> bars, int fast, int slow, int deviationLength,
        int averageLength, int smooth, double sensitivity, int kind, bool selected = false, double[]? externalAverage = null)
    {
        fast = Math.Max(1, fast); slow = Math.Max(1, slow); deviationLength = Math.Max(1, deviationLength); averageLength = Math.Max(1, averageLength); smooth = Math.Max(1, smooth);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value); var zero = R(0);
        var highs = bars.Select((b, i) => !selected || b.Close >= b.Low && b.Close <= b.High ? b.High : Math.Max(b.Close, bars[Math.Max(0, i - 1)].Close)).ToArray();
        var lows = bars.Select((b, i) => !selected || b.Close >= b.Low && b.Close <= b.High ? b.Low : Math.Min(b.Close, bars[Math.Max(0, i - 1)].Close)).ToArray();
        var returns = bars.Select((b, i) => i == 0 ? zero : R(KasePeakV2LogReference(b.Close, bars[i - 1].Close))).ToArray();
        var deviations = Enumerable.Range(0, bars.Count).Select(i =>
        {
            if (i < deviationLength) return zero;
            var samples = returns.Skip(i - deviationLength + 1).Take(deviationLength).ToArray();
            var mean = samples.Aggregate(zero, (sum, value) => sum + value) / R(deviationLength);
            var variance = samples.Aggregate(zero, (sum, value) => sum + (value - mean) * (value - mean)) / R(deviationLength);
            return R(variance.SqrtToDouble());
        }).ToArray();
        ReferenceFraction[] RecursiveMean()
        {
            // Retain the exact recurrence independently of the production compensated pair.
            var result = new ReferenceFraction[deviations.Length]; var previous = zero; var total = zero;
            for (var i = 0; i < deviations.Length; i++)
            {
                if (kind == 3 && i < averageLength) { total += deviations[i]; previous = total / R(i + 1); }
                else previous = (previous * R(averageLength - 1L) + deviations[i] * R(kind == 3 ? 2 : 1)) / R(kind == 3 ? averageLength + 1L : averageLength);
                result[i] = R(previous.ToDouble());
            }
            return result;
        }
        var averages = externalAverage is not null ? externalAverage.Select(R).ToArray() : kind is 1 or 2 ? SmoothRocBankStage(deviations, averageLength, kind)
            : kind is 3 or 6 ? RecursiveMean() : Average(deviations.Select(v => v.ToDouble()).ToArray(), averageLength, kind).Select(R).ToArray();
        ReferenceFraction[] Pressures(bool up) => Enumerable.Range(0, bars.Count).Select(i =>
        {
            if (averages[i].Sign == 0) return zero;
            var maximum = 0d;
            for (var lag = fast; lag <= Math.Min(i, slow - 1); lag++)
            {
                var log = up ? KasePeakV2LogReference(highs[i], lows[i - lag]) : KasePeakV2LogReference(highs[i - lag], lows[i]);
                maximum = Math.Max(maximum, (R(log) / R(Math.Sqrt(lag))).ToDouble());
            }
            return RoundRocBankStage(R(maximum) / averages[i]);
        }).ToArray();
        ReferenceFraction[] Partial(ReferenceFraction[] values) => Enumerable.Range(0, bars.Count).Select(i =>
        {
            var count = Math.Min(i + 1, smooth); var sum = values.Skip(i - count + 1).Take(count).Aggregate(zero, (s, v) => s + v);
            return RoundRocBankStage(sum / R(count));
        }).ToArray();
        var ups = Partial(Pressures(true)); var downs = Partial(Pressures(false));
        var values = ups.Select((v, i) => RoundRocBankStage((v - downs[i]) * R(sensitivity))).ToArray(); var trades = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var value = values[i]; var previous = i == 0 ? zero : values[i - 1];
            trades[i] = value.Sign > 0 && value.CompareTo(previous) > 0 ? Signal.StrongBuy : value.Sign < 0 && value.CompareTo(previous) < 0 ? Signal.StrongSell : value.Sign > 0 ? Signal.Buy : value.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (values.Select(v => v.ToDouble()).ToArray(), trades, deviations.Select(v => v.ToDouble()).ToArray());
    }
}
