using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class TillsonComparison
{
    internal static readonly string[] Ids = ["Skender.GetT3", "TaLib.Functions.T3", "QuanTAlib.T3"];
    internal static readonly ComparisonPair[] Pairs = Enumerable
        .Range(0, 3)
        .Select(v => Pair(v))
        .ToArray();

    internal static TillsonSeed Seed(int variant, bool useSma) =>
        variant == 1 ? TillsonSeed.CascadedMeans
        : variant == 2 && useSma ? TillsonSeed.FollowingPrefix
        : TillsonSeed.FirstPrice;

    internal static ComparisonPair Pair(
        int variant,
        double factor = .7,
        bool useSma = true,
        int suppression = 0
    ) =>
        new(
            Ids[variant],
            nameof(TillsonAverage),
            (d, p) => Native(d, p, variant, factor, useSma, suppression),
            (d, p) => Owned(d.IndicatorBars, p, factor, Seed(variant, useSma), suppression),
            (d, p) =>
                VolumePriceComparison.Mask(
                    Reference(d.Closes, p, factor, Seed(variant, useSma), suppression)
                ),
            MinimumInputCount: variant == 1 ? 2 : 1,
            CompetitorReference: (d, p) =>
                VolumePriceComparison.Mask(
                    NativeReference(
                        variant == 0 ? d.Quotes.Select(q => (double)q.Close).ToArray() : d.Closes,
                        p,
                        factor,
                        variant,
                        useSma,
                        suppression
                    )
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    private static ComparisonSeries Native(
        CompetitorData data,
        int p,
        int variant,
        double factor,
        bool useSma,
        int suppression
    )
    {
        if (variant == 0)
            return VolumePriceComparison.Mask(
                data.Quotes.GetT3(p, factor).Select(v => v.T3).ToArray()
            );
        if (variant == 2)
        {
            var indicator = new QuanTAlib.T3(p, factor, useSma);
            return VolumePriceComparison.Mask(
                data.Closes.Select(v =>
                        (double?)indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value
                    )
                    .ToArray()
            );
        }
        if (TALib.Core.UnstablePeriodSettings.Get(TALib.Core.UnstableFunc.T3) != suppression)
            throw new InvalidOperationException("T3 unstable setting mismatch");
        var result = new double?[data.Count];
        if (data.Count == 0)
            return VolumePriceComparison.Mask(result);
        var buffer = new double[data.Count];
        var status = Functions.T3<double>(
            data.Closes,
            System.Range.All,
            buffer,
            out var range,
            p,
            factor
        );
        if (status != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("Native T3 error " + status);
        var first = 6L * (p - 1) + suppression;
        var count = (int)Math.Max(0, data.Count - first);
        var start = count > 0 ? (int)first : 0;
        if (!range.Equals(new System.Range(start, start + count)))
            throw new InvalidOperationException("Unexpected T3 native alignment");
        for (var i = 0; i < count; i++)
            result[start + i] = buffer[i];
        return VolumePriceComparison.Mask(result);
    }

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int p,
        double factor,
        TillsonSeed seed,
        int suppression = 0
    )
    {
        var indicator = new TillsonAverage(p, factor, seed, suppression);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[indicator.Value].ToArray();
        var flags = run[indicator.IsDefined].ToArray();
        return VolumePriceComparison.Mask(
            values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray()
        );
    }

    internal static double?[] Reference(
        double[] prices,
        int p,
        double factor,
        TillsonSeed seed,
        int suppression = 0
    ) =>
        ExtendedReference(prices, p, factor, seed, suppression)
            .Select(v => v.HasValue ? (double?)Round(v.Value, Grid) : null)
            .ToArray();

    internal static BigInteger?[] ExtendedReference(
        double[] prices,
        int p,
        double factor,
        TillsonSeed seed,
        int suppression = 0
    ) => ExtendedReference(prices.Select(Units).ToArray(), p, factor, seed, suppression);

    internal static BigInteger?[] ExtendedReference(
        BigInteger[] prices,
        int p,
        double factor,
        TillsonSeed seed,
        int suppression = 0
    )
    {
        var stages = Enumerable.Range(0, 6).Select(_ => new BigInteger[prices.Length]).ToArray();
        for (var j = 0; j < 6; j++)
        {
            var input = j == 0 ? prices : stages[j - 1];
            var start = seed == TillsonSeed.CascadedMeans ? (long)j * (p - 1) : 0;
            for (var i = (int)Math.Min(prices.Length, start); i < prices.Length; i++)
            {
                if (seed == TillsonSeed.CascadedMeans)
                {
                    if (i < start + p - 1)
                        continue;
                    stages[j][i] =
                        i == start + p - 1
                            ? DirectionalComparison.RoundedUnits(
                                input
                                    .Skip((int)start)
                                    .Take(p)
                                    .Aggregate(BigInteger.Zero, (a, b) => a + b),
                                p
                            )
                            : DirectionalComparison.RoundedUnits(
                                stages[j][i - 1] * (p - 1) + 2 * input[i],
                                (long)p + 1
                            );
                }
                else if (i == 0)
                    stages[j][i] = input[i];
                else if (seed == TillsonSeed.FollowingPrefix && i < p)
                    stages[j][i] = DirectionalComparison.RoundedUnits(
                        input.Skip(1).Take(i).Aggregate(BigInteger.Zero, (a, b) => a + b),
                        i
                    );
                else
                    stages[j][i] = DirectionalComparison.RoundedUnits(
                        stages[j][i - 1] * (p - 1) + 2 * input[i],
                        (long)p + 1
                    );
            }
        }
        var v = Units(factor);
        var v2 = v * v;
        var v3 = v2 * v;
        var g2 = Grid * Grid;
        var g3 = g2 * Grid;
        var coefficients = new[]
        {
            g3 + 3 * v * g2 + 3 * v2 * Grid + v3,
            -3 * v * g2 - 6 * v2 * Grid - 3 * v3,
            3 * v2 * Grid + 3 * v3,
            -v3,
        };
        var result = new BigInteger?[prices.Length];
        var first = (seed == TillsonSeed.CascadedMeans ? 6L * (p - 1) : 0) + suppression;
        for (var i = 0; i < prices.Length; i++)
            if (i >= first)
                result[i] = DirectionalComparison.RoundedUnits(
                    Enumerable
                        .Range(0, 4)
                        .Aggregate(
                            BigInteger.Zero,
                            (s, j) => s + coefficients[j] * stages[j + 2][i]
                        ),
                    g3
                );
        return result;
    }

    internal static double?[] NativeReference(
        double[] prices,
        int p,
        double factor,
        int variant,
        bool useSma = true,
        int suppression = 0
    )
    {
        var seed = Seed(variant, useSma);
        var stages = Enumerable.Range(0, 6).Select(_ => new double[prices.Length]).ToArray();
        var alpha = Divide(
            2,
            variant == 1 ? (long)p + 1
                : variant == 0 ? checked(p + 1)
                : unchecked(p + 1)
        );
        var complement = Subtract(1, alpha);
        for (var j = 0; j < 6; j++)
        {
            var input = j == 0 ? prices : stages[j - 1];
            var start = seed == TillsonSeed.CascadedMeans ? (long)j * (p - 1) : 0;
            for (var i = (int)Math.Min(prices.Length, start); i < prices.Length; i++)
            {
                if (seed == TillsonSeed.CascadedMeans)
                {
                    if (i < start + p - 1)
                        continue;
                    stages[j][i] =
                        i == start + p - 1
                            ? Divide(input.Skip((int)start).Take(p).Aggregate(0d, Add), p)
                            : Add(
                                Multiply(alpha, input[i]),
                                Multiply(complement, stages[j][i - 1])
                            );
                }
                else if (i == 0)
                    stages[j][i] = input[i];
                else if (seed == TillsonSeed.FollowingPrefix && i < p)
                    stages[j][i] = PrefixNativeMean(input, i);
                else
                    stages[j][i] = Add(
                        Multiply(alpha, Subtract(input[i], stages[j][i - 1])),
                        stages[j][i - 1]
                    );
            }
        }
        var v = factor;
        double c1,
            c2,
            c3,
            c4;
        if (variant == 1)
        {
            var square = Multiply(v, v);
            c1 = Multiply(-square, v);
            c2 = Multiply(3, Subtract(square, c1));
            c3 = Subtract(Multiply(-6, square), Multiply(3, Subtract(v, c1)));
            c4 = Add(Subtract(Add(1, Multiply(3, v)), c1), Multiply(3, square));
        }
        else
        {
            c1 = Multiply(Multiply(-v, v), v);
            var threeSquare = Multiply(Multiply(3, v), v);
            var threeCube = Multiply(threeSquare, v);
            c2 = Add(threeSquare, threeCube);
            c3 = Subtract(Subtract(Multiply(Multiply(-6, v), v), Multiply(3, v)), threeCube);
            c4 = Add(Add(Add(1, Multiply(3, v)), Multiply(Multiply(v, v), v)), threeSquare);
        }
        var result = new double?[prices.Length];
        var first = (variant == 1 ? 6L * (p - 1) : 0) + suppression;
        for (var i = 0; i < prices.Length; i++)
            if (i >= first)
                result[i] =
                    variant == 0 && i == 0
                        ? prices[0]
                        : Add(
                            Add(
                                Add(Multiply(c1, stages[5][i]), Multiply(c2, stages[4][i])),
                                Multiply(c3, stages[3][i])
                            ),
                            Multiply(c4, stages[2][i])
                        );
        return result;
    }

    // Match the pinned buffer's lane-wise sum without calling its implementation.
    private static double PrefixNativeMean(double[] values, int count)
    {
        var width = Vector<double>.Count;
        var lanes = new double[width];
        var offset = 0;
        for (; offset <= count - width; offset += width)
        for (var j = 0; j < width; j++)
            lanes[j] = Add(lanes[j], values[1 + offset + j]);
        var sum = lanes.Aggregate(0d, Add);
        for (; offset < count; offset++)
            sum = Add(sum, values[1 + offset]);
        return Divide(sum, count);
    }
}
