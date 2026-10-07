using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SchaffShkOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return SchaffShkValues(bars, Integer(o, "FastLength", 23), Integer(o, "SlowLength", 50), Integer(o, "CycleLength", 10), Integer(o, "D1Length", 3), Integer(o, "D2Length", 3), AverageKind(o, 3)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) SchaffShkValues(IReadOnlyList<Bar> bars, int fastLength, int slowLength, int cycle, int d1, int d2, int kind, double[][]? external = null)
    {
        fastLength = Math.Max(1, fastLength); slowLength = Math.Max(1, slowLength); cycle = Math.Max(1, cycle); d1 = Math.Max(1, d1); d2 = Math.Max(1, d2);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        ReferenceFraction Pair(ReferenceFraction v) { var high = v.RoundExtendedBinary64(); return high + (v - high).RoundExtendedBinary64(); }
        var prices = bars.Select(b => R(b.Close)).ToArray();
        ReferenceFraction[] Average(int period)
        {
            var output = new ReferenceFraction[prices.Length]; var state = R(0);
            for (var i = 0; i < prices.Length; i++)
            {
                ReferenceFraction value;
                if (kind == 1) value = i + 1L < period ? R(0) : Window(prices, i, period).Aggregate(R(0), (a, b) => a + b) / R(period);
                else if (kind == 2) { var first = Math.Max(0, i - period + 1); value = Enumerable.Range(first, i - first + 1).Aggregate(R(0), (sum, j) => sum + prices[j] * R(period - i + j)) / new ReferenceFraction((long)period * (period + 1L) / 2); }
                else if (kind == 3 && i < period) value = prices.Take(i + 1).Aggregate(R(0), (a, b) => a + b) / R(i + 1);
                else value = (state * R(period - 1L) + prices[i] * R(kind == 3 ? 2 : 1)) / R(kind == 3 ? period + 1L : period);
                state = output[i] = Pair(value);
            }
            return output;
        }
        var fast = external is null ? Average(fastLength) : external[0].Select(R).ToArray(); var slow = external is null ? Average(slowLength) : external[1].Select(R).ToArray();
        var macd = fast.Select((v, i) => Pair(v - slow[i])).ToArray(); var scales = fast.Select((v, i) => (v.RoundExtendedBinary64().Abs() + slow[i].RoundExtendedBinary64().Abs()).RoundExtendedBinary64()).ToArray();
        ReferenceFraction[] Normalize(ReferenceFraction[] input, ReferenceFraction[]? scale)
        {
            var result = new ReferenceFraction[input.Length]; var previous = R(0);
            for (var i = 0; i < input.Length; i++)
            {
                var window = Window(input, i, cycle).ToArray(); var low = window.Min(); var high = window.Max(); var range = Pair(high - low);
                var magnitude = scale is not null ? Window(scale, i, cycle).Max() : new[] { low.RoundExtendedBinary64().Abs(), high.RoundExtendedBinary64().Abs() }.Max(); var threshold = (magnitude * R(Math.Pow(2, -46))).RoundExtendedBinary64();
                if (range.RoundExtendedBinary64().CompareTo(threshold) > 0) { var value = Pair(R(100) * (input[i] - low) / range); previous = value.Sign < 0 ? R(0) : value.CompareTo(R(100)) > 0 ? R(100) : value; } result[i] = previous;
            }
            return result;
        }
        ReferenceFraction[] Smooth(ReferenceFraction[] input, int period)
        { var values = new ReferenceFraction[input.Length]; var previous = R(0); for (var i = 0; i < input.Length; i++) values[i] = previous = Pair((previous * R(period - 1L) + input[i] * R(2)) / R(period + 1L)); return values; }
        var first = Normalize(macd, scales); var middle = Smooth(first, d1); var second = Normalize(middle, null); var line = Smooth(second, d2).Select(v => Math.Max(0, Math.Min(100, v.ToDouble()))).ToArray(); var signals = new Signal[line.Length]; var previousValue = R(0); var previousSlope = R(0);
        for (var i = 0; i < line.Length; i++) { var slope = R(line[i]) - previousValue; signals[i] = slope.Sign > 0 && slope.CompareTo(previousSlope) > 0 ? Signal.StrongBuy : slope.Sign < 0 && slope.CompareTo(previousSlope) < 0 ? Signal.StrongSell : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None; previousValue = R(line[i]); previousSlope = slope; }
        return (new Dictionary<string, double[]> { ["Stc"] = line, ["Macd"] = macd.Select(v => v.ToDouble()).ToArray() }, signals);
    }
}
