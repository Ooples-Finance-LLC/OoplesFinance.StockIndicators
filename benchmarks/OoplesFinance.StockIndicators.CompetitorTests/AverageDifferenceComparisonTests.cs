using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class AverageDifferenceComparisonTests
{
    [Theory]
    [InlineData(1, 3, false)]
    [InlineData(3, 1, false)]
    [InlineData(3, 3, false)]
    [InlineData(1, int.MaxValue, false)]
    [InlineData(1, 3, true)]
    [InlineData(3, 1, true)]
    [InlineData(3, 3, true)]
    [InlineData(1, int.MaxValue, true)]
    public async Task ContractsVerifyIndependentArithmeticAndLifecycle(
        int first,
        int second,
        bool exponential
    )
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(MovingAverageDifference),
                $"{first}/{second}/{exponential}",
                () => new MovingAverageDifference(first, second, exponential)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeRoutesAndReversedPeriodsPreserveAllValues(bool exponential)
    {
        decimal[] prices = [1, 5, 2, -4, 0, 7, 7, 3, 9];
        foreach (var (first, second) in new[] { (1, 3), (3, 1), (3, 3), (2, 5) })
        {
            var expected = AverageDifferenceComparison.NativeReference(
                prices,
                first,
                second,
                exponential
            );
            var tuple = AverageDifferenceComparison.Tuple(prices, first, second, exponential);
            Assert.Equal(expected, tuple.Compute());
            Assert.Equal(expected, tuple.Compute());
            Assert.Equal(expected.Skip(3).Take(4), tuple.Compute(startIndex: 3, endIndex: 6));
            int[] indexes = [8, 2, 2, 0, 7];
            Assert.Equal(
                indexes.Select(i => expected[i]),
                tuple.Compute((IEnumerable<int>)indexes)
            );
            foreach (var i in indexes)
                Assert.Equal(expected[i], tuple[i]);
            var mapped = prices.Select((v, i) => (Price: v, Index: i)).ToArray();
            var generic = exponential
                ? new T.ExponentialMovingAverageOscillator<(decimal Price, int Index), decimal?>(
                    mapped,
                    r => r.Price,
                    first,
                    second
                ).Compute()
                : new T.SimpleMovingAverageOscillator<(decimal Price, int Index), decimal?>(
                    mapped,
                    r => r.Price,
                    first,
                    second
                ).Compute();
            Assert.Equal(expected, generic);
            var pair = AverageDifferenceComparison.Create(exponential, first, second);
            ComparisonVerifier.Check(
                pair,
                CompetitorData.FromCloses(prices.Select(v => (double)v).ToArray()),
                20
            );
        }
    }

    [Fact]
    public void StartupAndNativeInvalidPeriodBehaviorAreExplicit()
    {
        var data = CompetitorData.FromCloses([1, 3, 5, 7]);
        Assert.Equal(
            new[] { false, false, true, true },
            AverageDifferenceComparison
                .Create(false, 1, 3)
                .Ooples(data, 20)
                .Outputs["Value"]
                .Present
        );
        Assert.Equal(
            new[] { true, true, true, true },
            AverageDifferenceComparison.Create(true, 1, 3).Ooples(data, 20).Outputs["Value"].Present
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new MovingAverageDifference(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MovingAverageDifference(2, 0));
        Assert.All(
            AverageDifferenceComparison.Tuple([1, 2, 3], 0, 2, false).Compute(),
            v => Assert.Null(v)
        );
        Assert.Equal(
            AverageDifferenceComparison.NativeReference([1, 2, 3], 0, 2, true),
            AverageDifferenceComparison.Tuple([1, 2, 3], 0, 2, true).Compute()
        );
        Assert.Throws<DivideByZeroException>(() =>
            AverageDifferenceComparison.Tuple([1, 2, 3], -1, 2, true).Compute()
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExtremePricesPreserveFiniteMeansAndRejectFinalOverflow(bool exponential)
    {
        foreach (
            double[] prices in new[]
            {
                new[] { double.MaxValue, double.MaxValue, double.MaxValue },
                new[] { -double.Epsilon, double.Epsilon, -double.Epsilon, double.Epsilon },
            }
        )
        {
            var bars = prices
                .Select((p, i) => new Bar(DateTime.UnixEpoch.AddDays(i), p, p, p, p, 1))
                .ToArray();
            ComparisonVerifier.Compare(
                VolumePriceComparison.Mask(
                    AverageDifferenceComparison.Reference(prices, 1, 3, exponential)
                ),
                AverageDifferenceComparison.Owned(bars, 1, 3, exponential),
                "wide average difference",
                IndicatorErrorBudget.Exact
            );
        }
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            VolumePriceComparisonTests.Run(
                new MovingAverageDifference(1, 5, exponential),
                (-double.MaxValue, -double.MaxValue, -double.MaxValue, 1),
                (-double.MaxValue, -double.MaxValue, -double.MaxValue, 1),
                (-double.MaxValue, -double.MaxValue, -double.MaxValue, 1),
                (-double.MaxValue, -double.MaxValue, -double.MaxValue, 1),
                (double.MaxValue, double.MaxValue, double.MaxValue, 1)
            )
        );
        Assert.Throws<OverflowException>(() =>
            AverageDifferenceComparison
                .Tuple([decimal.MaxValue, decimal.MaxValue, -decimal.MaxValue], 1, 2, exponential)
                .Compute()
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceChainingUsesCloseAndOutputCanBeChained(bool exponential)
    {
        var source = new PriceCircularTransform(PriceCircularOperation.Cosine);
        var indicator = new MovingAverageDifference(1, 3, exponential);
        indicator.Of(source);
        var downstream = new FirstValueEma(1);
        downstream.Of(indicator);
        double[] prices = [0, .5, 1, .25, 2];
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(
                Bars.From(
                    prices.Select(
                        (v, i) =>
                            new Bar(DateTime.UnixEpoch.AddDays(i), v + 10, v + 20, v - 20, v, 1)
                    )
                )
            )
            .ConfigureIndicators(source, indicator, downstream)
            .BuildAsync();
        var expected = AverageDifferenceComparison
            .Reference(run[source.Value].ToArray(), 1, 3, exponential)
            .Select(v => v ?? 0)
            .ToArray();
        Assert.Equal(expected, run[indicator.Value].ToArray());
        Assert.Equal(expected, run[downstream.Outputs[0]].ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothArmsDetectValueAndPresenceCorruption(bool exponential)
    {
        var pair = AverageDifferenceComparison.Create(exponential, 1, 3);
        var data = CompetitorData.FromCloses([1, 5, 2, 7, 4]);
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                var output = result.Outputs["Value"];
                if (presence)
                    output.Present![3] = false;
                else
                    output.Values[3] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    data,
                    20
                )
            );
        }
    }
}
