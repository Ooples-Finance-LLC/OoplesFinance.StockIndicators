using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] FiniteVolumeOutputs(IReadOnlyList<Bar> bars, int length = 22, double factor = .3, int kind = 1)
    {
        length = Math.Max(1, length); var zero = new ReferenceFraction(0); var previousTypical = zero; var previous = zero;
        var averages = SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.Volume)).ToArray(), length, kind); var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var b = bars[i]; var close = ReferenceFraction.FromDouble(b.Close); var high = ReferenceFraction.FromDouble(b.High); var low = ReferenceFraction.FromDouble(b.Low);
            var median = RoundRocBankStage((high + low) / new ReferenceFraction(2)); var typical = RoundRocBankStage((high + low + close) / new ReferenceFraction(3));
            var flow = RoundRocBankStage(RoundRocBankStage(RoundRocBankStage(close - median) + typical) - previousTypical);
            var threshold = RoundRocBankStage(RoundRocBankStage(close * ReferenceFraction.FromDouble(factor)) / new ReferenceFraction(100));
            var direction = (flow - threshold).Sign > 0 ? 1 : (flow + threshold).Sign < 0 ? -1 : 0;
            if (averages[i].Sign != 0)
            {
                var increment = RoundRocBankStage(ReferenceFraction.FromDouble(direction * b.Volume) / averages[i]); increment = RoundRocBankStage(increment / new ReferenceFraction(length)); increment = RoundRocBankStage(increment * new ReferenceFraction(100));
                previous = RoundRocBankStage(previous + increment);
            }
            result[i] = previous.ToDouble(); previousTypical = typical;
        }
        return result;
    }
}
