using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RocPipelineOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var kind = AverageKind(options, indicator.BatchName == IndicatorName.CoppockCurve ? 2 : 3);
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        ReferenceFraction Change(int i, int lag) => i < lag || prices[i - lag].Sign == 0 ? new ReferenceFraction(0)
            : RoundRocBankStage((prices[i] - prices[i - lag]) * new ReferenceFraction(100) / prices[i - lag]);
        if (indicator.BatchName == IndicatorName.CoppockCurve)
        {
            var fast = Integer(options, "FastLength", 11); var slow = Integer(options, "SlowLength", 14);
            var total = prices.Select((_, i) => RoundRocBankStage(Change(i, fast) + Change(i, slow))).ToArray();
            var curve = SmoothRocBankStage(total, Integer(options, "Length", 10), kind);
            return Outputs(("Cc", curve.Select(v => v.ToDouble()).ToArray()));
        }
        if (indicator.BatchName == IndicatorName.SmoothedRateOfChange)
        {
            var means = SmoothRocBankStage(prices, Integer(options, "SmoothLength", 13), kind);
            var lag = Integer(options, "RocLength", Integer(options, "Length", 21));
            return Outputs(("Sroc", means.Select((v, i) => i < lag || means[i - lag].Sign == 0 ? 100 :
                ((v - means[i - lag]) * new ReferenceFraction(100) / means[i - lag]).ToDouble()).ToArray()));
        }
        var decision = indicator.BatchName == IndicatorName.DecisionPointPriceMomentumOscillator;
        var length = Integer(options, "Length", 14);
        var firstPeriod = decision ? 2 * length + 7 : Integer(options, "Length1", Integer(options, "Length", 35));
        var secondPeriod = decision ? length + 6 : Integer(options, "Length2", 20);
        var first = new ReferenceFraction(0);
        var previous = new ReferenceFraction(0);
        var line = new ReferenceFraction[bars.Count];
        for (var i = 0; i < line.Length; i++)
        {
            first = RoundRocBankStage((new ReferenceFraction(firstPeriod - 2L) * first + new ReferenceFraction(2) * Change(i, 1))
                / new ReferenceFraction(firstPeriod));
            var scaled = RoundRocBankStage(first * new ReferenceFraction(10));
            previous = RoundRocBankStage((new ReferenceFraction(secondPeriod - 2L) * previous + new ReferenceFraction(2) * scaled)
                / new ReferenceFraction(secondPeriod));
            line[i] = previous;
        }
        var signal = SmoothRocBankStage(line, Integer(options, "SignalLength", 10), kind);
        if (!decision) return Outputs(("Pmo", line.Select(v => v.ToDouble()).ToArray()), ("Signal", signal.Select(v => v.ToDouble()).ToArray()));
        var histogram = line.Select((v, i) => (v - signal[i]).ToDouble()).ToArray();
        return Outputs(("Dppmo", line.Select(v => v.ToDouble()).ToArray()), ("Signal", signal.Select(v => v.ToDouble()).ToArray()), ("Histogram", histogram));
    }
}
