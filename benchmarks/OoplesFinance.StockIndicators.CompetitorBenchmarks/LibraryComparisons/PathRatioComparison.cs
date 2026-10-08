using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class PathRatioComparison
{
    internal static readonly ComparisonPair[] Pairs = new[]
    {
        "Skender.GetCmo",
        "Trady.Indicator.EfficiencyRatio",
    }
        .Select(id => new ComparisonPair(
            id,
            nameof(WindowPathRatio),
            (d, p) => Native(id, d, p),
            (d, p) => Owned(id, d, p),
            (d, p) => Reference(id, d, p, false),
            CompetitorReference: (d, p) => Reference(id, d, p, true),
            ErrorBudget: IndicatorErrorBudget.Exact
        ))
        .ToArray();

    internal static bool Absolute(string id) => id.StartsWith("Trady.", StringComparison.Ordinal);

    private static ComparisonSeries Native(string id, CompetitorData data, int period) =>
        VolumePriceComparison.Mask(
            Absolute(id)
                ? new Trady.Analysis.Indicator.EfficiencyRatio(data.Candles, period)
                    .Compute()
                    .Select(r => (double?)r.Tick)
                    .ToArray()
                : data.Quotes.GetCmo(period).Select(r => r.Cmo).ToArray()
        );

    private static ComparisonSeries Owned(string id, CompetitorData data, int period)
    {
        var indicator = new WindowPathRatio(
            period,
            Absolute(id) ? PathRatioConvention.AbsoluteFraction : PathRatioConvention.SignedPercent
        );
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var flags = run[indicator.IsDefined].ToArray();
        return VolumePriceComparison.Mask(
            run[indicator.Value]
                .ToArray()
                .Select((v, i) => flags[i] > 0 ? (double?)v : null)
                .ToArray()
        );
    }

    private static ComparisonSeries Reference(
        string id,
        CompetitorData data,
        int period,
        bool native
    )
    {
        var result = new double?[data.Count];
        if (native && Absolute(id))
        {
            var prices = data.Candles.Select(c => c.Close).ToArray();
            for (var i = period; i < prices.Length; i++)
            {
                var distance = Enumerable
                    .Range(i - period + 1, period)
                    .Sum(j => Math.Abs(prices[j] - prices[j - 1]));
                if (distance > 0)
                    result[i] = (double)(Math.Abs(prices[i] - prices[i - period]) / distance);
            }
        }
        else if (native)
        {
            var prices = data.Quotes.Select(q => (double)q.Close).ToArray();
            for (var i = period; i < prices.Length; i++)
            {
                double up = 0,
                    down = 0;
                for (var j = i - period + 1; j <= i; j++)
                {
                    var difference = Subtract(prices[j], prices[j - 1]);
                    if (difference > 0)
                        up = Add(up, difference);
                    else if (difference < 0)
                        down = Add(down, -difference);
                }
                var distance = Add(up, down);
                if (distance > 0)
                    result[i] = Divide(Multiply(100, Subtract(up, down)), distance);
            }
        }
        else
        {
            var prices = data.Closes.Select(Units).ToArray();
            for (var i = period; i < prices.Length; i++)
            {
                var path = Enumerable
                    .Range(i - period + 1, period)
                    .Aggregate(
                        BigInteger.Zero,
                        (sum, j) => sum + BigInteger.Abs(prices[j] - prices[j - 1])
                    );
                if (path.IsZero)
                    continue;
                var net = prices[i] - prices[i - period];
                result[i] = Absolute(id)
                    ? Round(BigInteger.Abs(net), path)
                    : Round(100 * net, path);
            }
        }
        return VolumePriceComparison.Mask(result);
    }

    internal static double?[] NullableReference(double?[] input, int period, bool absolute)
    {
        var result = new double?[input.Length];
        for (var i = period; i < input.Length; i++)
        {
            if (!input[i].HasValue || !input[i - period].HasValue)
                continue;
            BigInteger path = 0;
            for (var j = i - period + 1; j <= i; j++)
                if (input[j].HasValue && input[j - 1].HasValue)
                    path += BigInteger.Abs(Units(input[j]!.Value) - Units(input[j - 1]!.Value));
            if (path.IsZero)
                continue;
            var net = Units(input[i]!.Value) - Units(input[i - period]!.Value);
            result[i] = absolute ? Round(BigInteger.Abs(net), path) : Round(100 * net, path);
        }
        return result;
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([1, 1, 2, -1, -1, 3, 3, 3, 3, 2, 4, 1, 0]);
}
