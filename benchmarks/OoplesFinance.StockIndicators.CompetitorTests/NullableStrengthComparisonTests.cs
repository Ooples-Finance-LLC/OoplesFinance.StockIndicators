using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class NullableStrengthComparisonTests
{
    [Theory]
    [InlineData(NullableStrengthConvention.RelativeStrength, 3, 1)]
    [InlineData(NullableStrengthConvention.RelativeStrengthIndex, 3, 1)]
    [InlineData(NullableStrengthConvention.NetMomentum, 3, 1)]
    [InlineData(NullableStrengthConvention.RelativeMomentum, 3, 2)]
    [InlineData(NullableStrengthConvention.RelativeMomentumIndex, 3, 2)]
    [InlineData(NullableStrengthConvention.RelativeStrengthIndex, 1, 1)]
    [InlineData(NullableStrengthConvention.RelativeMomentumIndex, 1, 1)]
    [InlineData(NullableStrengthConvention.RelativeStrengthIndex, int.MaxValue, 1)]
    [InlineData(NullableStrengthConvention.RelativeMomentumIndex, int.MaxValue, int.MaxValue)]
    public async Task IndependentContractsVerifyLifecycleSeedAndLazyPeriods(
        NullableStrengthConvention convention,
        int period,
        int lag
    )
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(NullableStrengthOscillator),
                $"{convention}/{period}/{lag}",
                () => new NullableStrengthOscillator(period, convention, lag)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    private static Bar[] ToBars(double[] input) =>
        input.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();

    private static void CheckOwned(
        double?[] input,
        int period,
        NullableStrengthConvention convention,
        int lag
    )
    {
        var expected = NullableStrengthComparison.OwnedReference(input, period, convention, lag);
        var actual = NullableStrengthOscillator
            .FromValues(input, period, convention, lag)
            .ToArray();
        Assert.Equal(expected, actual);
        if (input.All(v => v.HasValue))
            ComparisonVerifier.Compare(
                VolumePriceComparison.Mask(expected),
                NullableStrengthComparison.Owned(
                    ToBars(input.Select(v => v!.Value).ToArray()),
                    period,
                    convention,
                    lag
                ),
                "nullable strength raw range",
                IndicatorErrorBudget.Exact
            );
    }

    [Fact]
    public void MonotonicAndFlatAbsenceRulesAreNotConventionalRsiEndpoints()
    {
        foreach (var convention in Enum.GetValues<NullableStrengthConvention>())
        {
            foreach (var prices in new[] { new[] { 1d, 1, 1, 1, 1 }, new[] { 1d, 2, 3, 4, 5 } })
            {
                var pair = NullableStrengthComparison.Create(convention);
                var data = CompetitorData.FromCloses(prices);
                ComparisonVerifier.Check(pair, data, 2);
                Assert.All(pair.Ooples(data, 2).Outputs["Value"].Present!, p => Assert.False(p));
            }
            var down = NullableStrengthOscillator
                .FromValues([5, 4, 3, 2, 1], 2, convention)
                .ToArray();
            Assert.Null(down[0]);
            Assert.Null(down[1]);
            for (var i = 2; i < down.Length; i++)
                Assert.Equal(
                    convention == NullableStrengthConvention.RelativeMomentumIndex ? null
                        : convention == NullableStrengthConvention.NetMomentum ? -100d
                        : 0d,
                    down[i]
                );
        }
    }

    [Fact]
    public void NullableSeedAveragesSkipMissingChangesThenLaterGapsRemainAbsent()
    {
        decimal?[][] fixtures =
        [
            [5, 3, 4, null, 2, 3, 4, 5],
            [null, 3, 2, 4, 5, 6, 2, null, 3, 4, 5, 6],
            [null, null, null, null, 1, 2, 3, 4],
            [5, 3, 7, 2, 4, 1, 5, 3, 6, 2],
        ];
        foreach (var convention in Enum.GetValues<NullableStrengthConvention>())
        foreach (
            var lag in NullableStrengthComparison.Momentum(convention)
                ? new[] { 1, 2 }
                : new[] { 1 }
        )
        foreach (var input in fixtures)
        {
            var expected = NullableStrengthComparison.NativeReference(input, 3, convention, lag);
            var native = NullableStrengthComparison.Tuple(input, 3, convention, lag);
            Assert.Equal(expected, native.Compute());
            Assert.Equal(expected, native.Compute());
            Assert.Equal(
                expected,
                NullableStrengthComparison.Generic(input, 3, convention, lag).Compute()
            );
            Assert.Equal(expected.Skip(3).Take(3), native.Compute(startIndex: 3, endIndex: 5));
            Assert.Equal(
                new[] { expected[^1], expected[3], expected[^1] },
                native.Compute((IEnumerable<int>)new[] { input.Length - 1, 3, input.Length - 1 })
            );
            Assert.Equal(expected[3], native.Compute(3, 5));
            var values = input.Select(v => (double?)v).ToArray();
            CheckOwned(values, 3, convention, lag);
            Assert.Equal(
                expected.Select(v => v.HasValue),
                NullableStrengthOscillator
                    .FromValues(values, 3, convention, lag)
                    .Select(v => v.HasValue)
            );
        }
        var partial = NullableStrengthOscillator.FromValues([5, 3, 4, null, 2, 3, 4], 3).ToArray();
        Assert.NotNull(partial[3]);
        Assert.All(partial.Skip(4), v => Assert.Null(v));
    }

    [Fact]
    public void IndependentEnumerationAndInvalidInputsAreExplicit()
    {
        var input = new double?[] { 5, 3, 7, 2, 4, 1 };
        var series = NullableStrengthOscillator.FromValues(input, 2);
        Assert.Equal(series.ToArray(), series.ToArray());
        using var first = series.GetEnumerator();
        using var second = series.GetEnumerator();
        for (var i = 0; i < 3; i++)
            Assert.True(first.MoveNext());
        Assert.NotNull(first.Current);
        Assert.True(second.MoveNext());
        Assert.Null(second.Current);
        Assert.Empty(NullableStrengthOscillator.FromValues([]));
        Assert.Null(NullableStrengthOscillator.FromValues([1]).Single());
        Assert.Throws<ArgumentNullException>(() => NullableStrengthOscillator.FromValues(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NullableStrengthOscillator(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NullableStrengthOscillator(2, (NullableStrengthConvention)100)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NullableStrengthOscillator(2, momentumPeriod: 2)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NullableStrengthOscillator(2, NullableStrengthConvention.RelativeMomentum, 0)
        );
        foreach (
            var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }
        )
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NullableStrengthOscillator.FromValues([1, invalid], 1).ToArray()
            );
        decimal?[] values = [5, 3, 7, 2, 4, 1];
        foreach (var c in Enum.GetValues<NullableStrengthConvention>())
        {
            if (NullableStrengthComparison.Momentum(c))
            {
                Assert.All(
                    NullableStrengthComparison.Tuple(values, 0, c, 1).Compute(),
                    v => Assert.Null(v)
                );
                Assert.All(
                    NullableStrengthComparison.Tuple(values, 3, c, 0).Compute(),
                    v => Assert.Null(v)
                );
            }
            else
                Assert.Throws<DivideByZeroException>(() =>
                    NullableStrengthComparison.Tuple(values, 0, c, 1).Compute()
                );
        }
    }

    [Fact]
    public async Task BoundedOutputsDoNotEvaluateAnOverflowingUnselectedRatio()
    {
        foreach (var c in Enum.GetValues<NullableStrengthConvention>())
        {
            if (
                c
                is NullableStrengthConvention.RelativeStrength
                    or NullableStrengthConvention.RelativeMomentum
            )
            {
                Assert.Throws<ArithmeticException>(() =>
                    NullableStrengthOscillator
                        .FromValues([double.Epsilon, 0, double.MaxValue], 2, c)
                        .ToArray()
                );
                await Assert.ThrowsAsync<IndicatorOutputException>(() =>
                    VolumePriceComparisonTests.Run(
                        new NullableStrengthOscillator(2, c),
                        (double.Epsilon, double.Epsilon, double.Epsilon, 1),
                        (0, 0, 0, 1),
                        (double.MaxValue, double.MaxValue, double.MaxValue, 1)
                    )
                );
            }
            else
            {
                CheckOwned([double.Epsilon, 0, double.MaxValue], 2, c, 1);
                Assert.Equal(
                    100,
                    NullableStrengthOscillator
                        .FromValues([double.Epsilon, 0, double.MaxValue], 2, c)
                        .Last()
                );
            }
            Assert.Throws<OverflowException>(() =>
                NullableStrengthComparison
                    .Tuple([.0000000001m, 0, 10000000000000000000000000000m], 2, c, 1)
                    .Compute()
            );
        }
    }

    [Fact]
    public void TinyWideAndLongDecayingAveragesMatchIndependentScaledReferences()
    {
        foreach (var c in Enum.GetValues<NullableStrengthConvention>())
        {
            CheckOwned([0, double.Epsilon, 0, 2 * double.Epsilon, 0], 2, c, 1);
            CheckOwned(
                [-double.MaxValue, double.MaxValue, -double.MaxValue, 0, double.MaxValue],
                2,
                c,
                1
            );
            if (NullableStrengthComparison.Momentum(c))
                CheckOwned(
                    [0, double.Epsilon, 0, 2 * double.Epsilon, 0, 3 * double.Epsilon, 0],
                    3,
                    c,
                    2
                );
        }
        var prices = new List<double?> { 0, 2, 1, 1 };
        prices.AddRange(Enumerable.Repeat<double?>(1, 5000));
        foreach (
            var c in new[]
            {
                NullableStrengthConvention.RelativeMomentum,
                NullableStrengthConvention.RelativeMomentumIndex,
            }
        )
            CheckOwned(prices.ToArray(), 3, c, 1);
        var native = NullableStrengthComparison
            .Tuple(
                prices.Select(x => (decimal?)x).ToArray(),
                3,
                NullableStrengthConvention.RelativeMomentum,
                1
            )
            .Compute();
        Assert.NotNull(native[3]);
        Assert.Equal(1m, native[^1]);
        Assert.Equal(
            2d,
            NullableStrengthOscillator
                .FromValues(prices, 3, NullableStrengthConvention.RelativeMomentum)
                .Last()
        );
    }

    [Fact]
    public async Task ChainingAndBothPresenceFieldsUseSelectedClose()
    {
        var data = NullableStrengthComparison.Fixture();
        var source = new PriceCircularTransform(PriceCircularOperation.Cosine);
        var indicator = new NullableStrengthOscillator(
            3,
            NullableStrengthConvention.RelativeMomentumIndex,
            2
        );
        indicator.Of(source);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(source, indicator)
            .BuildAsync();
        var expected = NullableStrengthComparison.OwnedReference(
            run[source.Value].ToArray().Select(v => (double?)v).ToArray(),
            3,
            NullableStrengthConvention.RelativeMomentumIndex,
            2
        );
        var actual = run[indicator.Value].ToArray();
        var flags = run[indicator.IsDefined].ToArray();
        for (var i = 0; i < actual.Length; i++)
        {
            Assert.Equal(expected[i].HasValue, flags[i] > 0);
            if (flags[i] > 0)
                Assert.Equal(expected[i]!.Value, actual[i]);
        }
    }

    [Fact]
    public void EveryFamilyRejectsValueAndPresenceCorruption()
    {
        var data = NullableStrengthComparison.Fixture();
        foreach (var pair in NullableStrengthComparison.Pairs)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                var output = result.Outputs["Value"];
                if (presence)
                    output.Present![^1] = false;
                else
                    output.Values[^1] += 1;
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
