using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AdaptiveStochasticOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return AdaptiveStochasticValues(bars, 50, Integer(options, "MinLength", 5), Integer(options, "MaxLength", 20)).Outputs; }

    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) AdaptiveStochasticValues(IReadOnlyList<Bar> bars, int length, int fastLength, int slowLength)
    {
        length = Math.Max(1, length); fastLength = Math.Max(1, fastLength); slowLength = Math.Max(1, slowLength);
        var fitLength = Math.Max(1, Math.Abs(slowLength - fastLength));
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var endpoints = new ReferenceFraction[bars.Count]; var values = new double[bars.Count]; var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var prices = Window(bars, i, fitLength).Select(b => R(b.Close)).ToArray(); var count = prices.Length;
            var meanX = new ReferenceFraction(count - 1L) / R(2); var meanY = prices.Aggregate(R(0), (sum, v) => sum + v) / new ReferenceFraction(count);
            var covariance = R(0); var variance = R(0);
            for (var j = 0; j < count; j++) { var x = new ReferenceFraction(j) - meanX; covariance += x * (prices[j] - meanY); variance += x * x; }
            endpoints[i] = (count == 1 ? prices[0] : meanY + covariance / variance * meanX).RoundExtendedBinary64();
            var travel = R(0); double efficiency = 0;
            if (i >= length)
            {
                for (var j = i - length + 1; j <= i; j++) { var difference = R(bars[j].Close) - R(bars[j - 1].Close); travel += difference.Sign < 0 ? R(0) - difference : difference; }
                var displacement = R(bars[i].Close) - R(bars[i - length].Close); if (displacement.Sign < 0) displacement = R(0) - displacement;
                efficiency = travel.Sign == 0 ? 0 : (displacement / travel).ToDouble();
            }
            var fast = Window(endpoints, i, fastLength).ToArray(); var slow = Window(endpoints, i, slowLength).ToArray();
            ReferenceFraction Extreme(ReferenceFraction[] sequence, bool maximum) => sequence.Aggregate((a, b) => (maximum ? a.CompareTo(b) >= 0 : a.CompareTo(b) <= 0) ? a : b);
            ReferenceFraction Blend(bool maximum) => ((Extreme(fast, maximum) * R(efficiency)).RoundExtendedBinary64() + (Extreme(slow, maximum) * R(1 - efficiency)).RoundExtendedBinary64()).RoundExtendedBinary64();
            var low = Blend(false); var high = Blend(true);
            values[i] = high.CompareTo(low) == 0 ? 0 : Math.Max(0, Math.Min(1, ((endpoints[i] - low) / (high - low)).ToDouble()));
            var previous = i == 0 ? 0 : values[i - 1]; var older = i < 2 ? 0 : values[i - 2]; var slope = values[i] - previous; var oldSlope = previous - older;
            signals[i] = slope > 0 && slope > oldSlope ? Signal.StrongBuy : slope < 0 && slope < oldSlope ? Signal.StrongSell : slope > 0 || previous < .2 && values[i] > .2 ? Signal.Buy : slope < 0 || previous > .8 && values[i] < .8 ? Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { ["Ast"] = values }, signals);
    }
}
