using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] UltimatePressureOutputs(IReadOnlyList<Bar> bars, int first, int second, int third)
    {
        var pressure = new ReferenceFraction[bars.Count]; var range = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var previous = i > 0 ? bars[i - 1].Close : bars[i].Close;
            var lower = ReferenceFraction.FromDouble(Math.Min(bars[i].Low, previous)); var upper = ReferenceFraction.FromDouble(Math.Max(bars[i].High, previous));
            pressure[i] = RoundRocBankStage(ReferenceFraction.FromDouble(bars[i].Close) - lower); range[i] = RoundRocBankStage(upper - lower);
        }
        var periods = new[] { Math.Max(1, first), Math.Max(1, second), Math.Max(1, third) };
        return Enumerable.Range(0, bars.Count).Select(i =>
        {
            var blend = new ReferenceFraction(0);
            for (var slot = 0; slot < 3; slot++)
            {
                var p = new ReferenceFraction(0); var r = new ReferenceFraction(0);
                for (var j = Math.Max(0, i - periods[slot] + 1); j <= i; j++) { p += pressure[j]; r += range[j]; }
                var ratio = r.Sign == 0 ? new ReferenceFraction(0) : RoundRocBankStage(p / r);
                blend += ratio * new ReferenceFraction(slot == 0 ? 4 : slot == 1 ? 2 : 1);
            }
            return Math.Max(0, Math.Min(100, RoundRocBankStage(RoundRocBankStage(blend / new ReferenceFraction(7)) * new ReferenceFraction(100)).ToDouble()));
        }).ToArray();
    }
}
