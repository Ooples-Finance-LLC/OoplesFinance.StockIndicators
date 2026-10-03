using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> BreakoutRsiOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, bool selected = false)
    { var options = indicator.CreateOptions(); return BreakoutRsiValues(bars, Integer(options, "Length", 14), 2, selected).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) BreakoutRsiValues(IReadOnlyList<Bar> bars, int length, int volumeLength, bool selected = false)
    {
        length = Math.Max(1, length); volumeLength = Math.Max(1, volumeLength); ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x);
        var prices = bars.Select(b => selected ? R(b.Close) : R(((R(b.Open) + R(b.High) + R(b.Low) + R(b.Close)) / R(4)).ToDouble())).ToArray();
        var powers = new ReferenceFraction[bars.Count]; var positive = new ReferenceFraction[bars.Count]; var negative = new ReferenceFraction[bars.Count]; var output = new double[bars.Count]; var signals = new Signal[bars.Count]; var previousSlope = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var b = bars[i]; var price = prices[i]; var prior = i == 0 ? price : prices[i - 1]; var high = R(b.High); var low = R(b.Low);
            if (price.CompareTo(low) < 0 || price.CompareTo(high) > 0) { high = price.CompareTo(prior) > 0 ? price : prior; low = price.CompareTo(prior) < 0 ? price : prior; }
            var range = high - low; var volume = Window(bars, i, volumeLength).Aggregate(R(0), (sum, v) => sum + R(v.Volume));
            powers[i] = range.Sign == 0 ? R(0) : (price * (R(b.Close) - R(b.Open)) * volume / range).RoundExtendedBinary64();
            var priorPower = i == 0 ? R(0) : powers[i - 1]; positive[i] = powers[i].CompareTo(priorPower) > 0 ? powers[i].Abs() : R(0); negative[i] = powers[i].CompareTo(priorPower) < 0 ? powers[i].Abs() : R(0);
            var gain = Window(positive, i, length).Aggregate(R(0), (a, b) => a + b); var loss = Window(negative, i, length).Aggregate(R(0), (a, b) => a + b);
            output[i] = loss.Sign == 0 ? 100 : (R(100) * gain / (gain + loss)).ToDouble();
            var previous = i == 0 ? 0 : output[i - 1]; var slope = R(output[i]) - R(previous);
            signals[i] = slope.Sign > 0 && slope.CompareTo(previousSlope) > 0 ? Signal.StrongBuy : slope.Sign < 0 && slope.CompareTo(previousSlope) < 0 ? Signal.StrongSell
                : slope.Sign > 0 || previous < 20 && output[i] > 20 ? Signal.Buy : slope.Sign < 0 || previous > 80 && output[i] < 80 ? Signal.Sell : Signal.None;
            previousSlope = slope;
        }
        return (new Dictionary<string, double[]> { ["Brsi"] = output }, signals);
    }
}
