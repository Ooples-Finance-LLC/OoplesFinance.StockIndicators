using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] RunningEquityOutputs(IReadOnlyList<Bar> bars, int length = 100, int kind = 1)
    {
        length = Math.Max(1, length); var zero = new ReferenceFraction(0); var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var average = SmoothRocBankStage(prices, length, kind); var changes = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var side = i == 0 ? 0 : bars[i - 1].Close.CompareTo(average[i - 1].ToDouble());
            changes[i] = i == 0 ? zero : RoundRocBankStage(RoundRocBankStage(prices[i] - prices[i - 1]) * new ReferenceFraction(side));
        }
        return prices.Select((v, i) => Window(changes, i, length).Aggregate(zero, (a, b) => a + b).ToDouble()).ToArray();
    }
}
