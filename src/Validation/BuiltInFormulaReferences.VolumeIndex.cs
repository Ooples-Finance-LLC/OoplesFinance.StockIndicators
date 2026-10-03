using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> VolumeIndexOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double[]? customerSignals = null)
    {
        var options = indicator.CreateOptions(); var positive = indicator.BatchName == IndicatorName.PositiveVolumeIndex;
        var index = new ReferenceFraction(Integer(options, "InitialValue", 1000));
        var line = bars.Select((b, i) =>
        {
            if (i > 0 && bars[i - 1].Close != 0 && (positive ? b.Volume > bars[i - 1].Volume : b.Volume < bars[i - 1].Volume))
                index = (index * (new ReferenceFraction(1) + (ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(bars[i - 1].Close)) / ReferenceFraction.FromDouble(Math.Abs(bars[i - 1].Close)))).RoundExtendedBinary64();
            return index;
        }).ToArray();
        var signal = customerSignals ?? SmoothRocBankStage(line, Integer(options, "Length", 255), AverageKind(options, 3), value => value.RoundExtendedBinary64()).Select(v => v.ToDouble()).ToArray();
        var key = positive ? "Pvi" : "Nvi";
        return Outputs((key, line.Select(v => v.ToDouble()).ToArray()), (key + "Signal", signal));
    }
}
