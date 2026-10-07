using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> EfficiencyDerivedOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Math.Max(1, Integer(options, "Length", 50));
        var auto = indicator.BatchName == IndicatorName.EfficientAutoLine;
        var fast = ReferenceFraction.FromDouble(Number(options, .0001, "FastAlpha"));
        var slow = ReferenceFraction.FromDouble(Number(options, .005, "SlowAlpha"));
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(); var zero = new ReferenceFraction(0);
        var prefix = new ReferenceFraction[bars.Count + 1]; prefix[0] = zero;
        for (var i = 0; i < bars.Count; i++) prefix[i + 1] = prefix[i] + (prices[i] - (i == 0 ? zero : prices[i - 1])).Abs();
        var output = new double[bars.Count]; var previous = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            var travel = prefix[i + 1] - prefix[Math.Max(0, i - length + 1)];
            var delta = i < length ? zero : prices[i] - prices[i - length];
            var efficiency = ReferenceFraction.FromDouble(travel.Sign == 0 ? 0 : (delta.Abs() / travel).ToDouble());
            if (auto)
            {
                var deviation = (slow + efficiency * (fast - slow)).RoundExtendedBinary64();
                if (i < 9 || (prices[i] - previous).Abs().CompareTo(deviation) > 0) previous = prices[i];
            }
            else previous = (previous + (delta.RoundExtendedBinary64() * efficiency).RoundExtendedBinary64()).RoundExtendedBinary64();
            output[i] = previous.ToDouble();
        }
        return Outputs((auto ? "Eal" : "Ep", output));
    }
}
