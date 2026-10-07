using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class WindowMoneyFlowReference
{
    internal static double?[] Calculate(
        IReadOnlyList<Bar> bars,
        int period,
        bool minimumTotalOne,
        int suppressed
    )
    {
        var prices = bars.Select(b =>
                ReferenceFraction.FromDouble(
                    (
                        (
                            ReferenceFraction.FromDouble(b.High)
                            + ReferenceFraction.FromDouble(b.Low)
                            + ReferenceFraction.FromDouble(b.Close)
                        ) / new ReferenceFraction(3)
                    ).ToDouble()
                )
            )
            .ToArray();
        var result = new double?[bars.Count];
        for (long end = (long)period + suppressed; end < bars.Count; end++)
        {
            var i = (int)end;
            var positive = new ReferenceFraction(0);
            var negative = new ReferenceFraction(0);
            for (var j = i - period + 1; j <= i; j++)
            {
                var flow = prices[j] * ReferenceFraction.FromDouble(bars[j].Volume);
                var direction = prices[j].CompareTo(prices[j - 1]);
                if (direction > 0)
                    positive += flow;
                else if (direction < 0)
                    negative += flow;
            }
            var total = positive + negative;
            result[i] =
                minimumTotalOne && total.CompareTo(new ReferenceFraction(1)) < 0 ? 0
                : !minimumTotalOne && negative.Sign == 0 ? 100
                : total.Sign == 0 ? double.NegativeInfinity
                : (new ReferenceFraction(100) * positive / total).ToDouble();
        }
        return result;
    }
}
