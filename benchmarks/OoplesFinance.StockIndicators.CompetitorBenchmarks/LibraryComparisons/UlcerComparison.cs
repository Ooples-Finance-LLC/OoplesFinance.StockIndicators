using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class UlcerComparison
{
    internal static readonly ComparisonPair Pair = new(
        "Skender.GetUlcerIndex",
        nameof(WindowUlcerIndex),
        (d, p) => VolumePriceComparison.Mask(d.Quotes.GetUlcerIndex(p).Select(r => r.UI).ToArray()),
        (d, p) => Owned(d.IndicatorBars, p),
        (d, p) => VolumePriceComparison.Mask(Reference(d.Closes, p, false)),
        CompetitorReference: (d, p) =>
            VolumePriceComparison.Mask(
                Reference(d.Quotes.Select(q => (double)q.Close).ToArray(), p, true)
            ),
        ErrorBudget: IndicatorErrorBudget.Exact
    );

    internal static ComparisonSeries Owned(Bar[] bars, int period)
    {
        var indicator = new WindowUlcerIndex(period);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[indicator.Value].ToArray();
        var present = run[indicator.IsDefined].ToArray();
        return VolumePriceComparison.Mask(
            values.Select((v, i) => present[i] > 0 ? (double?)v : null).ToArray()
        );
    }

    internal static double?[] Reference(double[] prices, int period, bool native)
    {
        var result = new double?[prices.Length];
        for (var i = period - 1; i < prices.Length; i++)
        {
            var maximum = 0d;
            var exactSquares = BigInteger.Zero;
            var nativeSquares = 0d;
            var defined = true;
            for (var j = i - period + 1; j <= i; j++)
            {
                maximum = Math.Max(maximum, prices[j]);
                if (maximum == 0)
                {
                    defined = false;
                    break;
                } // NOSONAR: Native formula tests exact zero.
                if (native)
                {
                    var drawdown = Multiply(100, Divide(Subtract(prices[j], maximum), maximum));
                    nativeSquares = Add(nativeSquares, Multiply(drawdown, drawdown));
                }
                else
                {
                    var n = 100 * (Units(prices[j]) - Units(maximum));
                    var d = Units(maximum);
                    var shift = 0;
                    double rounded;
                    while (double.IsInfinity(rounded = Round(n, d << shift)))
                        shift += 512;
                    var drawdown = Units(rounded) << shift;
                    exactSquares += drawdown * drawdown;
                }
            }
            if (defined)
                result[i] = native
                    ? Math.Sqrt(Divide(nativeSquares, period))
                    : DispersionReferenceArithmetic.Sqrt(exactSquares, period * Grid * Grid);
        }
        return result;
    }
}
