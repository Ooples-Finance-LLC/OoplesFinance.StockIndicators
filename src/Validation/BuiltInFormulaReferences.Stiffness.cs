using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] StiffnessOutputs(IReadOnlyList<Bar> bars, int length = 100, int lookback = 60, int smoothing = 3, int kind = 1)
    {
        length = Math.Max(1, length); lookback = Math.Max(1, lookback);
        var prices = Closes(bars); var mean = SmoothRocBankStage(prices.Select(ReferenceFraction.FromDouble).ToArray(), length, kind).Select(v => v.ToDouble()).ToArray();
        var deviation = PopulationDeviation(prices, length);
        var flags = prices.Select((value, i) => value > mean[i] - .2 * deviation[i]).ToArray();
        var votes = flags.Select((_, i) => ReferenceFraction.FromDouble(Window(flags, i, lookback).Count(v => v) * 100d / lookback)).ToArray();
        return SmoothRocBankStage(votes, Math.Max(1, smoothing), 3).Select(v => v.ToDouble()).ToArray();
    }
}
