using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> CommodityOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var inverse = indicator.BatchName == IndicatorName.EhlersCommodityChannelIndexInverseFisherTransform;
        var kind = AverageKind(options, inverse ? 2 : 1);
        var prices = indicator is IIndicator { Source: not null } ? Closes(bars) : bars.Select(b => ExactPriceMean(b.High, b.Low, b.Close)).ToArray();
        if (indicator.BatchName == IndicatorName.WoodieCommodityChannelIndex)
        {
            var fast = CommodityValues(prices, Integer(options, "FastLength", 6), kind);
            var slow = CommodityValues(prices, Integer(options, "SlowLength", 14), kind);
            return Outputs(("FastCci", fast), ("SlowCci", slow), ("Histogram", fast.Select((v, i) =>
                (ReferenceFraction.FromDouble(v) - ReferenceFraction.FromDouble(slow[i])).ToDouble()).ToArray()));
        }
        var line = CommodityValues(prices, Integer(options, "Length", 20), kind, Number(options, .015, "Constant"));
        if (!inverse) return Outputs(("Cci", line));
        var scaled = line.Select(v => ReferenceFraction.FromDouble((ReferenceFraction.FromDouble((ReferenceFraction.FromDouble(v) - new ReferenceFraction(50)).ToDouble())
            * ReferenceFraction.FromDouble(.1)).ToDouble())).ToArray();
        var smoothed = SmoothStrengthStage(scaled, Integer(options, "SignalLength", 9), kind);
        return Outputs(("Eiftcci", smoothed.Select(v => v.TanhToDouble()).ToArray()));
    }

    internal static double[] CommodityValues(double[] prices, int length, int kind, double constant = .015)
    {
        length = Math.Max(1, length);
        var values = prices.Select(ReferenceFraction.FromDouble).ToArray();
        var divisor = ReferenceFraction.FromDouble(constant);
        if (kind == 1)
        {
            var result = new double[prices.Length];
            for (var i = length - 1; i < result.Length; i++)
            {
                var window = values.Skip(i - length + 1).Take(length).ToArray();
                var n = new ReferenceFraction(length);
                var mean = window.Aggregate(new ReferenceFraction(0), (a, v) => a + v) / n;
                var deviation = window.Aggregate(new ReferenceFraction(0), (a, v) => a + (v - mean).Abs()) / n;
                result[i] = deviation.Sign == 0 ? 0 : ((values[i] - mean) / (divisor * deviation)).ToDouble();
            }
            return result;
        }
        var means = SmoothStrengthStage(values, length, kind);
        var residuals = values.Select((v, i) => RoundStrengthStage(v - means[i])).ToArray();
        var deviations = SmoothStrengthStage(residuals.Select(v => v.Abs()).ToArray(), length, kind);
        return residuals.Select((v, i) => deviations[i].Sign == 0 ? 0 : (v / (divisor * deviations[i])).ToDouble()).ToArray();
    }
}
