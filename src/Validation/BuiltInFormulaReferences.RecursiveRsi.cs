using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RecursiveRsiOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return RecursiveRsiValues(bars, Integer(options, "Length", 14), (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) RecursiveRsiValues(IReadOnlyList<Bar> bars, int length, MovingAvgType kind)
    {
        length = Math.Max(1, length); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var zero = R(0); var prices = bars.Select(b => R(b.Close)).ToArray();
        var changes = prices.Select((v, i) => i < length ? zero : v - prices[i - length]).ToArray();
        var source = new ReferenceFraction[bars.Count]; var prefix = new ReferenceFraction[bars.Count + 1]; prefix[0] = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            prefix[i + 1] = prefix[i] + changes[i];
            if (kind == MovingAvgType.SimpleMovingAverage)
                source[i] = i + 1 < length ? zero : (prefix[i + 1] - prefix[Math.Max(0, i - length + 1)]) / R(length);
            else if (kind == MovingAvgType.WeightedMovingAverage)
            {
                var sum = zero;
                for (var j = Math.Max(0, i - length + 1); j <= i; j++) sum += changes[j] * R((long)length - i + j);
                source[i] = sum * R(2) / (R(length) * R(length + 1L));
            }
            else if (kind == MovingAvgType.ExponentialMovingAverage)
                source[i] = i < length ? prefix[i + 1] / R(i + 1) : (source[i - 1] * R(length - 1) + changes[i] * R(2)) / R(length + 1L);
            else if (kind == MovingAvgType.WildersSmoothingMethod)
                source[i] = ((i == 0 ? zero : source[i - 1]) * R(length - 1) + changes[i]) / R(length);
            else throw new NotSupportedException();
        }
        var output = new ReferenceFraction[bars.Count]; var midpoint = new ReferenceFraction[bars.Count]; var votes = new bool[bars.Count];
        var gain = zero; var loss = zero; var previousChange = zero; var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var change = i == 0 ? zero : source[i] - source[i - 1];
            gain = (R(length - 1) * gain + (change.Sign > 0 ? change : zero)) / R(length);
            loss = (R(length - 1) * loss + (change.Sign < 0 ? zero - change : zero)) / R(length);
            var strength = loss.Sign == 0 ? R(100) : R(100) * gain / (gain + loss);
            midpoint[i] = (strength + (i < length ? source[i] : output[i - length])) / R(2);
            votes[i] = midpoint[i].CompareTo(i < length ? zero : midpoint[i - length]) >= 0;
            output[i] = i < length ? R(votes[i] ? 100 : 0) : R(100) * R(Enumerable.Range(i - length, length).Count(j => votes[j])) / R(length);
            var slope = output[i] - (i == 0 ? zero : output[i - 1]); var acceleration = slope.CompareTo(previousChange);
            signals[i] = slope.Sign > 0 && acceleration > 0 ? Signal.StrongBuy : slope.Sign < 0 && acceleration < 0 ? Signal.StrongSell
                : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
            previousChange = slope;
        }
        return (new() { ["Rrsi"] = output.Select(v => v.ToDouble()).ToArray() }, signals);
    }
}
