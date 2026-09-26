using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> MoneyFlowAccumulationOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator,
        double[]? customerFirst = null, double[]? customerSecond = null)
    {
        var total = new ReferenceFraction(0);
        var line = bars.Select(bar =>
        {
            var high = ReferenceFraction.FromDouble(bar.High); var low = ReferenceFraction.FromDouble(bar.Low);
            var price = ReferenceFraction.FromDouble(bar.Close); var volume = ReferenceFraction.FromDouble(bar.Volume);
            if (bar.High != bar.Low) total += RoundRocBankStage((new ReferenceFraction(2) * price - high - low) * volume / (high - low));
            return RoundRocBankStage(total);
        }).ToArray();
        var options = indicator.CreateOptions(); var oscillator = indicator.BatchName == IndicatorName.ChaikinOscillator;
        var kind = AverageKind(options, 3); var firstLength = Integer(options, oscillator ? "FastLength" : "Length", oscillator ? 3 : 14);
        var first = customerFirst is null ? SmoothRocBankStage(line, firstLength, kind) : customerFirst.Select(ReferenceFraction.FromDouble).ToArray();
        if (!oscillator) return Outputs(("Adl", line.Select(v => v.ToDouble()).ToArray()), ("AdlSignal", first.Select(v => v.ToDouble()).ToArray()));
        var second = customerSecond is null ? SmoothRocBankStage(line, Integer(options, "SlowLength", 10), kind) : customerSecond.Select(ReferenceFraction.FromDouble).ToArray();
        return Outputs(("ChaikinOsc", first.Select((v, i) => (v - second[i]).ToDouble()).ToArray()));
    }
}
