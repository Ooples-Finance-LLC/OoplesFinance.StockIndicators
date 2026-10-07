using System.Reflection;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class StochasticSmaComparisonTests
{
    [Theory]
    [InlineData("Raw", 3, 1, 1)]
    [InlineData("Fast", 3, 1, 2)]
    [InlineData("Slow", 3, 3, 2)]
    [InlineData("Full", 3, 5, 2)]
    [InlineData("FastDifference", 3, 1, 2)]
    [InlineData("SlowDifference", 3, 3, 2)]
    [InlineData("FullDifference", 3, 5, 2)]
    [InlineData("Raw", int.MaxValue, 1, 1)]
    [InlineData("Full", int.MaxValue, int.MaxValue, int.MaxValue)]
    [InlineData("FullDifference", int.MaxValue, int.MaxValue, 2)]
    public async Task IndependentContractsVerifyArithmeticLifecycleAndLazyPeriods(
        string variant,
        int period,
        int k,
        int d
    )
    {
        var sample = StochasticSmaComparison.Indicator(period, variant, k, d);
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                sample.GetType(),
                $"{variant}/{period}/{k}/{d}",
                () => StochasticSmaComparison.Indicator(period, variant, k, d)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void StartupFiftyAndNullableSmoothingAreNotConventionalFullSeedWindows()
    {
        var data = TradyExtremaComparison.Fixture();
        foreach (var variant in StochasticSmaComparison.Variants)
            ComparisonVerifier.Check(StochasticSmaComparison.Create(variant, 5, 2), data, 14);
        var raw = StochasticSmaComparison.Create("Raw").Ooples(data, 14).Outputs["Value"];
        Assert.All(raw.Values, v => Assert.Equal(50, v));
        Assert.All(raw.Present!, Assert.True);
        var full = StochasticSmaComparison.Create("Full", 5, 2).Ooples(data, 14).Outputs;
        foreach (var name in new[] { "K", "D", "J" })
        {
            Assert.All(full[name].Present!.Take(4), Assert.False);
            Assert.All(full[name].Present!.Skip(4), Assert.True);
            Assert.All(full[name].Values.Skip(4), v => Assert.Equal(50, v));
        }
        var flat = CompetitorData.FromOhlcv([2, 2], [2, 2], [2, 2], [2, 2], [1, 1]);
        ComparisonVerifier.Check(StochasticSmaComparison.Create("Raw"), flat, 1);
        Assert.All(
            StochasticSmaComparison.Create("Raw").Ooples(flat, 1).Outputs["Value"].Values,
            v => Assert.Equal(50, v)
        );
    }

    [Theory]
    [InlineData("Raw")]
    [InlineData("Fast")]
    [InlineData("Slow")]
    [InlineData("Full")]
    [InlineData("FastDifference")]
    [InlineData("SlowDifference")]
    [InlineData("FullDifference")]
    public void TupleGenericRepeatedAndIndexedNativeRoutesMatchDecimalReference(string variant)
    {
        var data = TradyExtremaComparison.Fixture();
        var prices = data.Candles.Select(c => (c.High, c.Low, c.Close)).ToArray();
        var expected = StochasticSmaComparison.NativeReference(prices, 3, variant, 5, 2);
        var indexes = new[] { 7, 2, 2, 0, 6 };
        if (variant == "Raw" || StochasticSmaComparison.Difference(variant))
        {
            var tuple = StochasticSmaComparison.ScalarTuple(prices, 3, variant, 5, 2);
            Assert.Equal(expected[0], tuple.Compute());
            Assert.Equal(expected[0], tuple.Compute());
            Assert.Equal(expected[0].Skip(3).Take(4), tuple.Compute(startIndex: 3, endIndex: 6));
            Assert.Equal(
                indexes.Select(i => expected[0][i]),
                tuple.Compute((IEnumerable<int>)indexes)
            );
            foreach (var i in indexes)
                Assert.Equal(expected[0][i], tuple[i]);
            var generic = variant switch
            {
                "Raw" => new T.RawStochasticsValue<int, decimal?>(
                    Enumerable.Range(0, prices.Length),
                    i => prices[i],
                    3
                ).Compute(),
                "FastDifference" => new T.StochasticsOscillator.Fast<int, decimal?>(
                    Enumerable.Range(0, prices.Length),
                    i => prices[i],
                    3,
                    2
                ).Compute(),
                "SlowDifference" => new T.StochasticsOscillator.Slow<int, decimal?>(
                    Enumerable.Range(0, prices.Length),
                    i => prices[i],
                    3,
                    2
                ).Compute(),
                _ => new T.StochasticsOscillator.Full<int, decimal?>(
                    Enumerable.Range(0, prices.Length),
                    i => prices[i],
                    3,
                    5,
                    2
                ).Compute(),
            };
            Assert.Equal(expected[0], generic);
        }
        else
        {
            var rows = Enumerable
                .Range(0, prices.Length)
                .Select(i => (expected[0][i], expected[1][i], expected[2][i]))
                .ToArray();
            var tuple = StochasticSmaComparison.Tuple(prices, 3, variant, 5, 2);
            Assert.Equal(rows, tuple.Compute());
            Assert.Equal(rows, tuple.Compute());
            Assert.Equal(rows.Skip(3).Take(4), tuple.Compute(startIndex: 3, endIndex: 6));
            Assert.Equal(indexes.Select(i => rows[i]), tuple.Compute((IEnumerable<int>)indexes));
            foreach (var i in indexes)
                Assert.Equal(rows[i], tuple[i]);
            var generic = variant switch
            {
                "Fast" => new T.Stochastics.Fast<int, (decimal? K, decimal? D, decimal? J)>(
                    Enumerable.Range(0, prices.Length),
                    i => prices[i],
                    3,
                    2
                ).Compute(),
                "Slow" => new T.Stochastics.Slow<int, (decimal? K, decimal? D, decimal? J)>(
                    Enumerable.Range(0, prices.Length),
                    i => prices[i],
                    3,
                    2
                ).Compute(),
                _ => new T.Stochastics.Full<int, (decimal? K, decimal? D, decimal? J)>(
                    Enumerable.Range(0, prices.Length),
                    i => prices[i],
                    3,
                    5,
                    2
                ).Compute(),
            };
            Assert.Equal(rows, generic);
        }
        ComparisonVerifier.Check(StochasticSmaComparison.Create(variant, 5, 2), data, 3);
    }

    [Fact]
    public void NativeContainersHaveNoCalculationApiAndChildrenRemainPaired()
    {
        foreach (var type in new[] { typeof(T.Stochastics), typeof(T.StochasticsOscillator) })
        {
            Assert.Equal(typeof(object), type.BaseType);
            Assert.Empty(
                type.GetMethods(
                    BindingFlags.Public
                        | BindingFlags.Instance
                        | BindingFlags.Static
                        | BindingFlags.DeclaredOnly
                )
            );
            Assert.Empty(
                type.GetFields(
                    BindingFlags.Public
                        | BindingFlags.Instance
                        | BindingFlags.Static
                        | BindingFlags.DeclaredOnly
                )
            );
            var row = ComparisonManifest
                .Create()
                .Single(r => r.Id == "Trady.Indicator." + type.Name);
            Assert.Equal("utility", row.Status);
            foreach (var child in new[] { "Fast", "Slow", "Full" })
                Assert.Contains(ComparisonPairs.All, p => p.Id == row.Id + "+" + child);
        }
    }

    [Fact]
    public void InvalidPeriodsAndNativeZeroPeriodSemanticsAreExplicit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FiftySeedStochastic(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StochasticSmaKdj(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StochasticSmaKdj(3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StochasticSmaKdj(3, 3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StochasticSmaDifference(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StochasticSmaDifference(3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StochasticSmaDifference(3, 3, 0));
        (decimal High, decimal Low, decimal Close)[] prices = [(3, 1, 2), (4, 2, 3)];
        Assert.All(
            StochasticSmaComparison.ScalarTuple(prices, 0, "Raw", 1, 1).Compute(),
            v => Assert.Equal(50m, v)
        );
        Assert.All(
            StochasticSmaComparison.Tuple(prices, 3, "Fast", 1, 0).Compute(),
            v =>
            {
                Assert.Equal(50m, v.K);
                Assert.Null(v.D);
                Assert.Null(v.J);
            }
        );
    }

    [Fact]
    public async Task ExtremeRangesAndUnselectedJDoNotOverflowPrematurely()
    {
        foreach (var scale in new[] { double.Epsilon, double.MaxValue })
        {
            Bar[] bars =
            [
                new(DateTime.UnixEpoch, 0, scale, -scale, 0, 1),
                new(DateTime.UnixEpoch.AddDays(1), scale, scale, -scale, scale, 1),
            ];
            foreach (var variant in StochasticSmaComparison.Variants)
                ComparisonVerifier.Compare(
                    RetrospectivePriceComparison.Series(
                        StochasticSmaComparison.Names(variant),
                        StochasticSmaComparison.OwnedReference(bars, 1, variant, 1, 1)
                    ),
                    StochasticSmaComparison.Owned(bars, 1, variant, 1, 1),
                    "wide stochastic",
                    IndicatorErrorBudget.Exact
                );
        }
        // With one-price smoothing K=D, so the selected difference is zero even
        // when a naive staged 3*K product for the unselected J would overflow.
        var largeClose = double.MaxValue / 200;
        var huge = new[] { new Bar(DateTime.UnixEpoch, 0, 1, 0, largeClose, 1) };
        Assert.Equal(
            0,
            StochasticSmaComparison.Owned(huge, 1, "FastDifference", 1, 1).Outputs["Value"].Values[
                0
            ]
        );
        Assert.Equal(
            largeClose * 100,
            StochasticSmaComparison.Owned(huge, 1, "Fast", 1, 1).Outputs["J"].Values[0]
        );
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            VolumePriceComparisonTests.Run(new FiftySeedStochastic(1), (1, 0, double.MaxValue, 1))
        );
        var cancelled = await VolumePriceComparisonTests.Run(
            new StochasticSmaDifference(1, 1, 1),
            (1, 0, double.MaxValue, 1)
        );
        Assert.Equal(0, cancelled[0][0]);
        Assert.Equal(1, cancelled[1][0]);
        Bar[] oversized =
        [
            new(DateTime.UnixEpoch, 0, 1, 0, double.MaxValue / 25, 1),
            new(DateTime.UnixEpoch.AddDays(1), 0, 1, 0, -double.MaxValue / 25, 1),
        ];
        foreach (var variant in new[] { "Full", "FullDifference" })
            ComparisonVerifier.Compare(
                RetrospectivePriceComparison.Series(
                    StochasticSmaComparison.Names(variant),
                    StochasticSmaComparison.OwnedReference(oversized, 1, variant, 2, 1)
                ),
                StochasticSmaComparison.Owned(oversized, 1, variant, 2, 1),
                "cancelled unpublished ratios",
                IndicatorErrorBudget.Exact
            );
        (decimal High, decimal Low, decimal Close)[] wide =
        [
            (decimal.MaxValue, 0, decimal.MaxValue),
        ];
        Assert.Throws<OverflowException>(() =>
            StochasticSmaComparison.ScalarTuple(wide, 1, "Raw", 1, 1).Compute()
        );
        (decimal High, decimal Low, decimal Close)[] largeRatio = [(1, 0, decimal.MaxValue / 200)];
        Assert.Throws<OverflowException>(() =>
            StochasticSmaComparison.ScalarTuple(largeRatio, 1, "FastDifference", 1, 1).Compute()
        );
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            VolumePriceComparisonTests.Run(
                new StochasticSmaKdj(1, 1, 2),
                (1, 0, 0, 1),
                (1, 0, double.MaxValue / 125, 1)
            )
        );
        var finiteDifference = await VolumePriceComparisonTests.Run(
            new StochasticSmaDifference(1, 1, 2),
            (1, 0, 0, 1),
            (1, 0, double.MaxValue / 125, 1)
        );
        Assert.True(double.IsFinite(finiteDifference[0][1]));
        Assert.True(finiteDifference[0][1] > double.MaxValue / 3);
    }

    [Fact]
    public async Task OverflowContractsRetainFinitePrefixAndUnpublishedCancellation()
    {
        Bar[] bars =
        [
            new(DateTime.UnixEpoch, 0, 1, 0, 0, 1),
            new(DateTime.UnixEpoch.AddDays(1), 0, 1, 0, double.MaxValue / 80, 1),
            new(DateTime.UnixEpoch.AddDays(2), 0, 1, 0, -double.MaxValue / 80, 1),
        ];
        foreach (var variant in new[] { "Raw", "Full", "FullDifference" })
        {
            var sample = StochasticSmaComparison.Indicator(1, variant, 2, 1);
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    sample.GetType(),
                    "oversized-stage/" + variant,
                    () => StochasticSmaComparison.Indicator(1, variant, 2, 1)
                ),
                new IndicatorValidationOptions
                {
                    AdditionalFixtures =
                    [
                        new IndicatorValidationFixture("oversized-stage-cancellation", bars),
                    ],
                }
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
            if (variant == "Raw")
                Assert.True(report.OutputOverflowRejectionsChecked > 0);
        }
    }

    [Fact]
    public void DecimalConversionCanChangeFlatWindowDecision()
    {
        var data = CompetitorData.FromOhlcv([0], [double.Epsilon], [0], [double.Epsilon], [1]);
        var pair = StochasticSmaComparison.Create("Raw");
        ComparisonVerifier.Check(pair, data, 1);
        Assert.Equal(100, pair.Ooples(data, 1).Outputs["Value"].Values[0]);
        Assert.Equal(50, pair.Competitor(data, 1).Outputs["Value"].Values[0]);
    }

    [Fact]
    public async Task ChainingReplacesOnlyCloseAndOscillatorFeedsDownstream()
    {
        var data = TradyExtremaComparison.Fixture();
        var source = new PriceCircularTransform(PriceCircularOperation.Cosine);
        var indicator = new StochasticSmaDifference(3, 2, 3);
        indicator.Of(source);
        var downstream = new FirstValueEma(1);
        downstream.Of(indicator);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(source, indicator, downstream)
            .BuildAsync();
        var closes = run[source.Value].ToArray();
        var mapped = data
            .IndicatorBars.Select(
                (b, i) =>
                    new Bar(
                        DateTime.UnixEpoch.AddDays(i),
                        b.Open,
                        b.High,
                        b.Low,
                        closes[i],
                        b.Volume
                    )
            )
            .ToArray();
        var expected = StochasticSmaComparison
            .OwnedReference(mapped, 3, "FullDifference", 2, 3)[0]
            .Select(v => v ?? 0)
            .ToArray();
        Assert.Equal(expected, run[indicator.Value].ToArray());
        Assert.Equal(expected, run[downstream.Value].ToArray());
    }

    [Fact]
    public void EveryValueAndPresenceDetectsCorruption()
    {
        var data = TradyExtremaComparison.Fixture();
        foreach (var variant in StochasticSmaComparison.Variants)
        foreach (var name in StochasticSmaComparison.Names(variant))
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            var pair = StochasticSmaComparison.Create(variant, 2, 3);
            ComparisonSeries Bad(CompetitorData input, int period)
            {
                var result = native ? pair.Competitor(input, period) : pair.Ooples(input, period);
                var output = result.Outputs[name];
                if (presence)
                    output.Present![4] = false;
                else
                    output.Values[4] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    data,
                    3
                )
            );
        }
    }
}
