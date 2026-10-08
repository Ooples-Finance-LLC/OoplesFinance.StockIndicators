using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> RobustWeightingOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return RobustWeightingOutputs(bars, Math.Max(1, Integer(options, "Length", 200)), AverageKind(options, 1));
    }
    internal static IReadOnlyDictionary<string,double[]> RobustWeightingOutputs(IReadOnlyList<Bar> bars, int length, int kind,
        ICollection<Signal>? signals = null)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction Round(ReferenceFraction value) => value.RoundExtendedBinary64();
        var prices = bars.Select(b => R(b.Close)).ToArray();
        var means = SmoothRocBankStage(prices, length, kind, Round);
        var times = SmoothRocBankStage(Enumerable.Range(0, bars.Count).Select(i => R(i)).ToArray(), length, kind, Round);
        var residuals = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var first = Math.Max(0, i - length + 1); var count = i - first + 1;
            var center = R(first + i) / R(2); var covariance = R(0); var spread = R(0);
            for (var j = first; j <= i; j++)
            {
                var offset = R(j) - center; covariance += offset * prices[j]; spread += offset * offset;
            }
            var slope = count < length || spread.Sign == 0 ? R(0) : covariance / spread;
            var line = Round(means[i] + slope * (R(i) - times[i]));
            residuals[i] = Round(prices[i] - line);
        }
        var output = SmoothRocBankStage(residuals, length, kind, Round); var previous = R(0); var previousSlope = R(0);
        foreach (var value in output)
        {
            var slope = value - previous;
            signals?.Add(slope.Sign > 0 ? slope.CompareTo(previousSlope) > 0 ? Signal.StrongBuy : Signal.Buy
                : slope.Sign < 0 ? slope.CompareTo(previousSlope) < 0 ? Signal.StrongSell : Signal.Sell : Signal.None);
            previous = value; previousSlope = slope;
        }
        return Outputs(("Rwo", output.Select(v => v.ToDouble()).ToArray()));
    }
}
