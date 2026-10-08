using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] ChopZoneOutputs(IReadOnlyList<Bar> bars, int length, int smoothing = 34, int kind = 3, bool selected = false)
    {
        length = Math.Max(1, length); var averages = SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(), Math.Max(1, smoothing), kind);
        return Enumerable.Range(0, bars.Count).Select(i =>
        {
            var window = bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i + 1, length)).ToArray();
            var low = ReferenceFraction.FromDouble(window.Min(b => b.Low)); var high = ReferenceFraction.FromDouble(window.Max(b => b.High));
            var span = RoundRocBankStage(high - low);
            var typical = selected ? ReferenceFraction.FromDouble(bars[i].Close) : RoundRocBankStage((ReferenceFraction.FromDouble(bars[i].High) + ReferenceFraction.FromDouble(bars[i].Low) + ReferenceFraction.FromDouble(bars[i].Close)) / new ReferenceFraction(3));
            if (span.Sign == 0 || typical.Sign == 0) return 0;
            var scale = RoundRocBankStage(RoundRocBankStage(new ReferenceFraction(25) / span) * low);
            var change = RoundRocBankStage((i > 0 ? averages[i - 1] : new ReferenceFraction(0)) - averages[i]);
            var slope = RoundRocBankStage(RoundRocBankStage(change / typical) * scale).ToDouble();
            return -Math.Round(Math.Atan2(slope, 1) * (180 / Math.PI));
        }).ToArray();
    }
}
