using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] SimpleLinesOutputs(IReadOnlyList<Bar> bars, int length = 10, double multiplier = 10)
    {
        length = Math.Max(1, length); var anchor = bars.Count == 0 ? new ReferenceFraction(0) : ReferenceFraction.FromDouble(bars[0].Close); var one = new ReferenceFraction(1);
        long ticks = 0, previousTicks = 0; var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            if (i > 0)
            {
                var price = RoundRocBankStage(RoundRocBankStage(ReferenceFraction.FromDouble(bars[i].Close) - anchor) * new ReferenceFraction(length));
                var previous = i == 1 ? price : ReferenceFraction.FromDouble(previousTicks);
                var tickValue = ReferenceFraction.FromDouble(ticks);
                var residual = RoundRocBankStage(RoundRocBankStage(tickValue - previous) * ReferenceFraction.FromDouble(multiplier));
                var displacement = RoundRocBankStage(RoundRocBankStage(price - tickValue) + residual);
                previousTicks = ticks;
                if ((displacement - one).Sign > 0) ticks++; else if ((displacement + one).Sign < 0) ticks--;
            }
            result[i] = (anchor + ReferenceFraction.FromDouble(ticks / (double)length)).ToDouble();
        }
        return result;
    }
}
