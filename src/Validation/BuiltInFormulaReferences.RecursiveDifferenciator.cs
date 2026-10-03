using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] RecursiveDifferenciatorTrajectory(IReadOnlyList<Bar> bars, int period, double alpha, int kind)
    {
        period = Math.Max(1, period);
        var average = SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(), period, kind);
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, average[i].ToDouble(), b.Volume)).ToArray();
        var strength = RoundedPriceRsi(projected, period, 6);
        var line = new ReferenceFraction[bars.Count]; var changes = new ReferenceFraction[bars.Count];
        var gain = ReferenceFraction.FromDouble(alpha); var retention = ReferenceFraction.FromDouble(1 - alpha);
        for (var i = 0; i < bars.Count; i++)
        {
            var source = ReferenceFraction.FromDouble(strength[i] / 100);
            var previous = i == 0 ? source : changes[i - 1];
            line[i] = RoundRocBankStage(RoundRocBankStage(gain * source) + RoundRocBankStage(retention * previous));
            changes[i] = RoundRocBankStage(line[i] - (i < period ? new ReferenceFraction(0) : line[i - period]));
        }
        return line.Select(v => v.ToDouble()).ToArray();
    }
}
