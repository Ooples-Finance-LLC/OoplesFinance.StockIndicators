using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static readonly IndicatorErrorBudget ObvReflexBudget = new(0, 4e-15, requireSameSign: true);
    internal static Dictionary<string, double[]> ObvReflexOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return ObvReflexValues(bars, Integer(options, "Length", 4), Integer(options, "SignalLength", 14), AverageKind(options, 1)).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) ObvReflexValues(IReadOnlyList<Bar> bars,
        int length, int signalLength, int kind = 1, double[]? selected = null, double[]? externalSignal = null)
    {
        length = Math.Max(1, length); signalLength = Math.Max(1, signalLength);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var zero = R(0); var one = R(1); var factor = new ReferenceFraction(BigInteger.One << 512);
        ReferenceFraction Compact(ReferenceFraction value)
        {
            if (value.Sign == 0) return zero;
            var scale = one; var magnitude = Math.Abs(value.ToDouble());
            while (double.IsInfinity(magnitude) || magnitude >= Math.Pow(2, 512)) { value /= factor; scale *= factor; magnitude = Math.Abs(value.ToDouble()); }
            while (magnitude < Math.Pow(2, -256)) { value *= factor; scale /= factor; magnitude = Math.Abs(value.ToDouble()); }
            var result = zero;
            for (var part = 0; part < 4; part++) { var component = R(value.ToDouble()); result += component; value -= component; }
            return result * scale;
        }
        ReferenceFraction[] Mean(ReferenceFraction[] values, int period)
        {
            if (kind is not (1 or 2 or 3 or 6)) return Average(values.Select(v => v.ToDouble()).ToArray(), period, kind).Select(R).ToArray();
            var result = new ReferenceFraction[values.Length]; var previous = zero;
            for (var i = 0; i < values.Length; i++)
            {
                if (period == 1) result[i] = values[i];
                else if (kind == 6) result[i] = Compact((previous * R(period - 1) + values[i]) / R(period));
                else if (kind == 3 && i >= period) result[i] = Compact((previous * R(period - 1) + R(2) * values[i]) / new ReferenceFraction(period + 1L));
                else if (kind == 1 && i + 1 < period) result[i] = zero;
                else
                {
                    var total = zero;
                    for (var j = Math.Max(0, i - period + 1); j <= i; j++) total += values[j] * R(kind == 2 ? period - i + j : 1);
                    result[i] = total / new ReferenceFraction(kind == 2 ? (long)period * (period + 1L) / 2 : Math.Min(i + 1, period));
                }
                previous = result[i];
            }
            return result;
        }
        var prices = (selected ?? Closes(bars)).Select(R).ToArray(); var line = new ReferenceFraction[bars.Count];
        var total = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            var prior = i < length ? zero : prices[i - length]; var direction = prices[i].CompareTo(prior);
            if (direction > 0) total += R(bars[i].Volume); else if (direction < 0) total -= R(bars[i].Volume);
            line[i] = total;
        }
        var signal = externalSignal?.Select(R).ToArray() ?? Mean(line, signalLength);
        var previous = zero; var trades = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var margin = line[i] - signal[i]; var change = margin - previous;
            trades[i] = margin.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : margin.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
                : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
            previous = margin;
        }
        return (new Dictionary<string, double[]> { ["Obvr"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = signal.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}
