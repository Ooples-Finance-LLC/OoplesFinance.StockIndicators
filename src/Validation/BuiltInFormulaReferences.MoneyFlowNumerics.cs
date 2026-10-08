using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedMoneyFlow(IReadOnlyList<Bar> bars, int length, bool selectedClose = false)
    {
        var prices = bars.Select(b => selectedClose ? b.Close : ExactPriceMean(b.High, b.Low, b.Close)).ToArray();
        return bars.Select((_, i) => {
            var positive = new ReferenceFraction(0);
            var negative = new ReferenceFraction(0);
            for (var j = Math.Max(1, i - length + 1); j <= i; j++)
            {
                var flow = ReferenceFraction.FromDouble(prices[j]) * ReferenceFraction.FromDouble(bars[j].Volume);
                if (prices[j] > prices[j - 1]) positive += flow;
                else if (prices[j] < prices[j - 1]) negative += flow;
            }
            if (negative.Sign == 0) return 100;
            var total = positive + negative;
            return positive.Sign == 0 || total.Sign == 0 ? 0 : Math.Max(0, Math.Min(100,
                (new ReferenceFraction(100) * positive / total).ToDouble()));
        }).ToArray();
    }
}
