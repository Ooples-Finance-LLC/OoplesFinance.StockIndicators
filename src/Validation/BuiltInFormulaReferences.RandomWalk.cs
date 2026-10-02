using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RandomWalkOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return RandomWalkValues(bars, Integer(options, "Length", 14), (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) RandomWalkValues(IReadOnlyList<Bar> bars, int length, MovingAvgType kind)
    {
        length = Math.Max(1, length); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var zero = R(0); var ranges = new ReferenceFraction[bars.Count];
        ReferenceFraction Abs(ReferenceFraction value) => value.Sign < 0 ? zero - value : value;
        for (var i = 0; i < bars.Count; i++)
        {
            var previous = R(bars[i == 0 ? 0 : i - 1].Close); var high = R(bars[i].High); var low = R(bars[i].Low);
            var range = high - low; var highGap = Abs(high - previous); var lowGap = Abs(low - previous);
            if (highGap.CompareTo(range) > 0) range = highGap; if (lowGap.CompareTo(range) > 0) range = lowGap; ranges[i] = range;
        }
        var atr = new ReferenceFraction[bars.Count]; var prefix = new ReferenceFraction[bars.Count + 1]; prefix[0] = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            prefix[i + 1] = prefix[i] + ranges[i];
            if (kind == MovingAvgType.SimpleMovingAverage)
                atr[i] = i + 1 < length ? zero : (prefix[i + 1] - prefix[Math.Max(0, i - length + 1)]) / R(length);
            else if (kind == MovingAvgType.WeightedMovingAverage)
            {
                var sum = zero;
                for (var j = Math.Max(0, i - length + 1); j <= i; j++) sum += ranges[j] * R((long)length - i + j);
                atr[i] = sum * R(2) / (R(length) * R(length + 1L));
            }
            else if (kind == MovingAvgType.ExponentialMovingAverage)
                atr[i] = i < length ? prefix[i + 1] / R(i + 1) : (atr[i - 1] * R(length - 1) + ranges[i] * R(2)) / R(length + 1L);
            else if (kind == MovingAvgType.WildersSmoothingMethod)
                atr[i] = ((i == 0 ? zero : atr[i - 1]) * R(length - 1) + ranges[i]) / R(length);
            else throw new NotSupportedException();
        }
        var highValues = new double[bars.Count]; var lowValues = new double[bars.Count]; var signals = new Signal[bars.Count]; var previousSpread = zero;
        double Publish(ReferenceFraction value) => value.Sign * (value * value / R(length)).SqrtToDouble();
        for (var i = 0; i < bars.Count; i++)
        {
            var up = atr[i].Sign == 0 ? zero : (R(bars[i].High) - (i < length ? zero : R(bars[i - length].Low))) / atr[i];
            var down = atr[i].Sign == 0 ? zero : ((i < length ? zero : R(bars[i - length].High)) - R(bars[i].Low)) / atr[i];
            highValues[i] = Publish(up); lowValues[i] = Publish(down); var spread = up - down; var direction = spread.CompareTo(previousSpread);
            signals[i] = spread.Sign > 0 && direction > 0 ? Signal.StrongBuy : spread.Sign < 0 && direction < 0 ? Signal.StrongSell
                : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
            previousSpread = spread;
        }
        return (new() { ["RwiHigh"] = highValues, ["RwiLow"] = lowValues }, signals);
    }
}
