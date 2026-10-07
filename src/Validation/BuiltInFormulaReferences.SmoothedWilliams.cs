using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SmoothedWilliamsOutputs(IReadOnlyList<Bar> bars, int length, int smoothing)
    {
        length = Math.Max(1, length); var gainValue = 2d / (Math.Max(1, smoothing) + 1L);
        var gain = ReferenceFraction.FromDouble(gainValue); var retain = ReferenceFraction.FromDouble(1 - gainValue);
        var previous = new ReferenceFraction(0); var values = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var raw = new ReferenceFraction(-50);
            if ((long)i + 1 >= length)
            {
                var sample = bars.Skip(Math.Max(0, i - length + 1)).Take(length).ToArray(); var high = sample.Max(b => b.High); var low = sample.Min(b => b.Low);
                if (high > low) raw = RoundRocBankStage((ReferenceFraction.FromDouble(bars[i].Close) - ReferenceFraction.FromDouble(high)) * new ReferenceFraction(100) / (ReferenceFraction.FromDouble(high) - ReferenceFraction.FromDouble(low)));
            }
            previous = i == 0 ? raw : RoundRocBankStage(RoundRocBankStage(raw * gain) + RoundRocBankStage(previous * retain)); values[i] = previous.ToDouble();
        }
        return Outputs(("Swr", values));
    }
}
