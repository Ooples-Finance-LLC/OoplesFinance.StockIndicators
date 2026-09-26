using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> OpenCloseAverageOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var delta = indicator.BatchName == IndicatorName.DeltaMovingAverage;
        var options = indicator.CreateOptions(); var length = Integer(options, delta ? "Length1" : "Length", delta ? 10 : 14);
        var lag = delta ? Integer(options, "Length2", 5) : 0;
        var difference = bars.Select((b, i) => RoundRocBankStage(ReferenceFraction.FromDouble(b.Close)
            - ReferenceFraction.FromDouble(i >= lag ? bars[i - lag].Open : 0))).ToArray();
        var signal = SmoothRocBankStage(difference, length, AverageKind(options, 1));
        return delta ? Outputs(("Delta", difference.Select(v => v.ToDouble()).ToArray()),
            ("Signal", signal.Select(v => v.ToDouble()).ToArray()),
            ("Histogram", difference.Select((v, i) => (v - signal[i]).ToDouble()).ToArray()))
            : Outputs(("Cqs", signal.Select(v => v.ToDouble()).ToArray()));
    }
}
