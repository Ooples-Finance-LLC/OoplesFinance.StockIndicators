using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class WindowFisherReference
{
    private static ReferenceFraction F(double v) => ReferenceFraction.FromDouble(v);

    internal static double?[][] Values(IReadOnlyList<Bar> bars, int period, bool midpoint)
    {
        var prices = bars.Select(b =>
                midpoint ? ((F(b.High) + F(b.Low)) / F(2)).ToDouble() : b.Close
            )
            .ToArray();
        var result = Enumerable.Range(0, 2).Select(_ => new double?[bars.Count]).ToArray();
        double position = 0,
            previous = 0;
        for (var i = 0; i < bars.Count; i++)
        {
            if (i == 0)
            {
                result[0][i] = 0;
                continue;
            }
            var window = prices
                .Skip(Math.Max(0, i - period + 1))
                .Take(Math.Min(period, i + 1))
                .ToArray();
            var low = F(window.Min());
            var range = F(window.Max()) - low;
            position =
                range.Sign == 0
                    ? 0
                    : .33 * 2 * (((F(prices[i]) - low) / range).ToDouble() - .5) + .67 * position;
            position =
                position > .99 ? .999
                : position < -.99 ? -.999
                : position;
            result[1][i] = previous;
            previous = .5 * Math.Log((1 + position) / (1 - position)) + .5 * previous;
            result[0][i] = previous;
        }
        return result;
    }
}
