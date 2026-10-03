using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RainbowOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return RainbowValues(bars, Integer(options, "Length", 2), 10, (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) RainbowValues(IReadOnlyList<Bar> bars, int length, int range, MovingAvgType kind)
    {
        length = Math.Max(1, length); range = Math.Max(2, range);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var zero = R(0); var prices = bars.Select(b => R(b.Close)).ToArray();
        ReferenceFraction[] AverageLayer(ReferenceFraction[] values)
        {
            var result = new ReferenceFraction[values.Length]; var prefix = new ReferenceFraction[values.Length + 1]; prefix[0] = zero;
            for (var i = 0; i < values.Length; i++) prefix[i + 1] = prefix[i] + values[i];
            for (var i = 0; i < values.Length; i++)
            {
                if (kind == MovingAvgType.SimpleMovingAverage)
                    result[i] = i + 1 < length ? zero : (prefix[i + 1] - prefix[Math.Max(0, i - length + 1)]) / R(length);
                else if (kind == MovingAvgType.WeightedMovingAverage)
                {
                    var sum = zero;
                    for (var j = Math.Max(0, i - length + 1); j <= i; j++) sum += values[j] * R((long)length - i + j);
                    result[i] = sum * R(2) / (R(length) * R(length + 1L));
                }
                else if (kind == MovingAvgType.ExponentialMovingAverage)
                    result[i] = i < length ? prefix[i + 1] / R(i + 1) : (result[i - 1] * R(length - 1) + values[i] * R(2)) / R(length + 1L);
                else if (kind == MovingAvgType.WildersSmoothingMethod)
                    result[i] = ((i == 0 ? zero : result[i - 1]) * R(length - 1) + values[i]) / R(length);
                else throw new NotSupportedException();
            }
            return result;
        }
        var layers = new List<ReferenceFraction[]>(); var stage = prices;
        for (var i = 0; i < 10; i++) { stage = AverageLayer(stage); layers.Add(stage); }
        var lines = new ReferenceFraction[bars.Count]; var upper = new ReferenceFraction[bars.Count]; var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var window = bars.Skip(Math.Max(0, i - range + 1)).Take(Math.Min(i + 1, range)).ToArray();
            var width = R(window.Max(b => b.Close)) - R(window.Min(b => b.Close));
            var sum = zero; var low = layers[0][i]; var high = low;
            foreach (var layer in layers) { sum += layer[i]; if (layer[i].CompareTo(low) < 0) low = layer[i]; if (layer[i].CompareTo(high) > 0) high = layer[i]; }
            lines[i] = width.Sign == 0 ? zero : R(100) * (prices[i] - sum / R(10)) / width;
            upper[i] = width.Sign == 0 ? zero : R(100) * (high - low) / width;
            var value = lines[i]; var previous = i == 0 ? zero : lines[i - 1]; var direction = value.CompareTo(previous);
            signals[i] = value.Sign > 0 && direction > 0 ? Signal.StrongBuy : value.Sign < 0 && direction < 0 ? Signal.StrongSell
                : value.Sign > 0 ? Signal.Buy : value.Sign < 0 ? Signal.Sell : Signal.None;
        }
        var bands = upper.Select(v => v.ToDouble()).ToArray();
        return (new() { ["Ro"] = lines.Select(v => v.ToDouble()).ToArray(), ["UpperBand"] = bands, ["LowerBand"] = bands.Select(v => -v).ToArray() }, signals);
    }
}
