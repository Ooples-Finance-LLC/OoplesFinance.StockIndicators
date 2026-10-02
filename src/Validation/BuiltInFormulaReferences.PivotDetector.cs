using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) PivotDetectorValues(IReadOnlyList<Bar> bars,
        int length1 = 200, int length2 = 14, int kind = 1, double[]? selected = null,
        double[]? externalGain = null, double[]? externalLoss = null, double[]? externalLevel = null)
    {
        length1 = Math.Max(1, length1); length2 = Math.Max(1, length2);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var zero = R(0); var prices = (selected ?? Closes(bars)).Select(R).ToArray();
        ReferenceFraction[] Mean(ReferenceFraction[] values, int period)
        {
            if (kind is not (1 or 2 or 3 or 6)) return Average(values.Select(v => v.ToDouble()).ToArray(), period, kind).Select(R).ToArray();
            var sums = new ReferenceFraction[values.Length + 1]; var moments = new ReferenceFraction[sums.Length];
            sums[0] = moments[0] = zero;
            for (var i = 0; i < values.Length; i++) { sums[i + 1] = sums[i] + values[i]; moments[i + 1] = moments[i] + values[i] * R(i + 1); }
            var means = new ReferenceFraction[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                var previous = i == 0 ? zero : means[i - 1];
                if (kind == 6) means[i] = (previous * R(period - 1) + values[i]) / R(period);
                else if (kind == 3 && i >= period) means[i] = (previous * R(period - 1) + R(2) * values[i]) / R(period + 1L);
                else if (kind == 1 && i + 1 < period) means[i] = zero;
                else if (kind == 2)
                {
                    var start = Math.Max(0, i - period + 1); var sum = sums[i + 1] - sums[start];
                    means[i] = (moments[i + 1] - moments[start] + R((long)period - i - 1) * sum)
                        / new ReferenceFraction((long)period * (period + 1L) / 2);
                }
                else means[i] = (sums[i + 1] - sums[Math.Max(0, i - period + 1)]) / R(Math.Min(i + 1, period));
            }
            return means;
        }
        var changes = prices.Select((p, i) => i == 0 ? zero : p - prices[i - 1]).ToArray();
        var gains = externalGain?.Select(R).ToArray() ?? Mean(changes.Select(v => v.Sign > 0 ? v : zero).ToArray(), length2);
        var losses = externalLoss?.Select(R).ToArray() ?? Mean(changes.Select(v => v.Sign < 0 ? zero - v : zero).ToArray(), length2);
        var levels = externalLevel?.Select(R).ToArray() ?? Mean(prices, length1);
        var values = new ReferenceFraction[prices.Length]; var signals = new Signal[prices.Length]; var lastRsi = zero;
        for (var i = 0; i < values.Length; i++)
        {
            var total = gains[i] + losses[i];
            var rsi = losses[i].Sign == 0 ? R(100) : total.Sign == 0 ? zero : R(100) * gains[i] / total;
            if (rsi.Sign < 0) rsi = zero; if (rsi.CompareTo(R(100)) > 0) rsi = R(100);
            if (i > 0 && length2 > 1 && kind is 3 or 6 && changes[i].Sign == 0) rsi = lastRsi;
            values[i] = R(2) * rsi - R(prices[i].CompareTo(levels[i]) > 0 ? 70 : 40); lastRsi = rsi;
            var previous = i == 0 ? zero : values[i - 1]; var before = i < 2 ? zero : values[i - 2];
            var slope = values[i] - previous; var oldSlope = previous - before;
            signals[i] = slope.Sign > 0 && slope.CompareTo(oldSlope) > 0 ? Signal.StrongBuy
                : slope.Sign < 0 && slope.CompareTo(oldSlope) < 0 ? Signal.StrongSell
                : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (new() { ["Pdo"] = values.Select(v => v.ToDouble()).ToArray() }, signals);
    }
}
