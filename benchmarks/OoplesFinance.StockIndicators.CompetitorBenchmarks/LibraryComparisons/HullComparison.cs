using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class HullComparison
{
    internal static readonly ComparisonPair[] Pairs = Enum.GetValues<HullWindowConvention>()
        .Select(Create)
        .ToArray();

    internal static ComparisonPair Create(HullWindowConvention convention) =>
        new(
            convention switch
            {
                HullWindowConvention.ExpandingFloor => "QuanTAlib.Hma",
                HullWindowConvention.FullWindowFloor => "Skender.GetHma",
                _ => "Trady.Indicator.HullMovingAverage",
            },
            nameof(HullWindow),
            (d, p) => Native(d, p, convention),
            (d, p) => Owned(d.IndicatorBars, p, convention),
            (d, p) => Reference(d.Closes, p, convention),
            CompetitorReference: (d, p) => NativeReference(d, p, convention),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    private static ComparisonSeries Native(
        CompetitorData data,
        int period,
        HullWindowConvention convention
    )
    {
        if (convention == HullWindowConvention.FullWindowFloor)
            return VolumePriceComparison.Mask(
                data.Quotes.GetHma(period).Select(r => r.Hma).ToArray()
            );
        if (convention == HullWindowConvention.FullWindowRounded)
            return VolumePriceComparison.Mask(
                new Trady.Analysis.Indicator.HullMovingAverage(data.Candles, period)
                    .Compute()
                    .Select(r => (double?)r.Tick)
                    .ToArray()
            );
        var indicator = new QuanTAlib.Hma(period);
        return VolumePriceComparison.Mask(
            data.Closes.Select(x =>
                    (double?)indicator.Calc(new QuanTAlib.TValue(x, true, false)).Value
                )
                .ToArray()
        );
    }

    internal static ComparisonSeries Owned(
        IReadOnlyList<Bar> bars,
        int period,
        HullWindowConvention convention
    )
    {
        var indicator = new HullWindow(period, convention);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
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

    private static (int Half, int Root) Lengths(int period, HullWindowConvention convention) =>
        convention == HullWindowConvention.FullWindowRounded
            ? ((int)Math.Round(period / 2d), (int)Math.Round(Math.Sqrt(period)))
            : (period / 2, (int)Math.Sqrt(period));

    internal static ComparisonSeries Reference(
        double[] prices,
        int period,
        HullWindowConvention convention
    )
    {
        var (half, root) = Lengths(period, convention);
        var input = prices.Select(Units).ToArray();
        var synthetic = new List<BigInteger>();
        var result = new double?[prices.Length];
        for (var end = 0; end < prices.Length; end++)
        {
            if (convention != HullWindowConvention.ExpandingFloor && end < period - 1)
                continue;
            var shortMean = Average(input, end, half);
            var longMean = Average(input, end, period);
            synthetic.Add(ExtendedUnits(2 * shortMean - longMean, Grid));
            if (convention != HullWindowConvention.ExpandingFloor && synthetic.Count < root)
                continue;
            result[end] = Round(Average(synthetic, synthetic.Count - 1, root), Grid);
        }
        return VolumePriceComparison.Mask(result);
    }

    private static BigInteger Average(IReadOnlyList<BigInteger> values, int end, int period)
    {
        BigInteger sum = 0,
            mass = 0;
        for (var index = Math.Max(0, end - period + 1); index <= end; index++)
        {
            var weight = period - end + index;
            sum += values[index] * weight;
            mass += weight;
        }
        return ExtendedUnits(sum, mass * Grid);
    }

    private static BigInteger ExtendedUnits(BigInteger numerator, BigInteger denominator)
    {
        var shift = 0;
        double rounded;
        while (!double.IsFinite(rounded = Round(numerator, denominator << shift)))
            shift += 512;
        return Units(rounded) << shift;
    }

    private static ComparisonSeries NativeReference(
        CompetitorData data,
        int period,
        HullWindowConvention convention
    )
    {
        if (convention == HullWindowConvention.FullWindowRounded)
            return VolumePriceComparison.Mask(
                DecimalReference(data.Candles.Select(c => (decimal?)c.Close).ToArray(), period)
                    .Select(x => (double?)x)
                    .ToArray()
            );
        var (half, root) = Lengths(period, convention);
        if (convention == HullWindowConvention.ExpandingFloor)
        {
            var shortMean = FixedWeightedComparison.Stage(data.Closes, half, true);
            var longMean = FixedWeightedComparison.Stage(data.Closes, period, true);
            var synthetic = shortMean
                .Select((v, i) => Subtract(Multiply(2, v), longMean[i]))
                .ToArray();
            return VolumePriceComparison.Mask(
                FixedWeightedComparison
                    .Stage(synthetic, root, true)
                    .Select(x => (double?)x)
                    .ToArray()
            );
        }
        var prices = data.Quotes.Select(q => (double)q.Close).ToArray();
        return SkenderReference(prices, period);
    }

    internal static ComparisonSeries SkenderReference(double[] prices, int period)
    {
        var half = period / 2;
        var root = (int)Math.Sqrt(period);
        var synthetic = new List<double>();
        var result = new double?[prices.Length];
        for (var end = period - 1; end < prices.Length; end++)
        {
            synthetic.Add(
                Subtract(
                    Multiply(2, SkenderStage(prices, end, half)),
                    SkenderStage(prices, end, period)
                )
            );
            if (synthetic.Count >= root)
                result[end] = SkenderStage(synthetic, synthetic.Count - 1, root);
        }
        return VolumePriceComparison.Mask(result);
    }

    private static double SkenderStage(IReadOnlyList<double> values, int end, int period)
    {
        var divisor = Divide(Multiply(period, unchecked(period + 1)), 2);
        var sum = 0d;
        for (var i = end - period + 1; i <= end; i++)
            sum = Add(sum, Divide(Multiply(values[i], i - end + period), divisor));
        return sum;
    }

    internal static decimal?[] DecimalReference(decimal?[] prices, int period)
    {
        var half = Convert.ToInt32(Math.Round(period / 2d));
        var root = Convert.ToInt32(Math.Round(Math.Sqrt(period)));
        var result = new decimal?[prices.Length];
        decimal Stage(int end, int length)
        {
            var divisor = unchecked((1 + length) * length / 2);
            decimal sum = 0;
            for (var i = end - length + 1; i <= end; i++)
                sum += (prices[i] * (i - end + length) / divisor) ?? 0;
            return sum;
        }
        for (var end = 0; end < prices.Length; end++)
        {
            if (end < (long)period + root - 2)
                continue;
            decimal sum = 0;
            for (var i = end - root + 1; i <= end; i++)
                sum +=
                    (i - end + root)
                    * (2 * Stage(i, half) - Stage(i, period))
                    / unchecked((1 + root) * root / 2);
            result[end] = sum;
        }
        return result;
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([
            1,
            2,
            4,
            8,
            -3,
            9,
            2,
            2,
            2,
            0,
            3,
            -1,
            8,
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8,
        ]);
}
