using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class AtrTrailingComparisonTests
{
    [Theory]
    [InlineData(AtrTrailBasis.Close, 3)]
    [InlineData(AtrTrailBasis.HighLow, 3)]
    [InlineData(AtrTrailBasis.Midpoint, 3)]
    [InlineData(AtrTrailBasis.Close, 20)]
    [InlineData(AtrTrailBasis.HighLow, 20)]
    [InlineData(AtrTrailBasis.Midpoint, 20)]
    public async Task RationalContractsCoverAllBandsAndLifecycle(AtrTrailBasis basis, int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(SeededAtrTrailingStop),
                "seeded ATR stop",
                () => new SeededAtrTrailingStop(period, 3, basis)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void ParametersStartupTiesAndLazyPeriodsAreExplicit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededAtrTrailingStop(1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SeededAtrTrailingStop(basis: (AtrTrailBasis)3)
        );
        foreach (var value in new[] { 0, -1, double.NaN, double.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new SeededAtrTrailingStop(multiplier: value)
            );
        var bars = Enumerable
            .Range(0, 12)
            .Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 5, 5, 5, 5, 0))
            .ToArray();
        foreach (var basis in Enum.GetValues<AtrTrailBasis>())
        {
            var actual = AtrTrailingComparison.Owned(bars, 3, 3, basis);
            Assert.Equal(
                Enumerable.Range(0, 12).Select(i => i >= 3),
                actual.Outputs["Stop"].Present!
            );
            Assert.Equal(
                Enumerable.Range(0, 12).Select(i => i >= 3),
                actual.Outputs["UpperBand"].Present!
            );
            Assert.All(actual.Outputs["LowerBand"].Present!, v => Assert.False(v));
            Assert.All(actual.Outputs["Stop"].Values.Skip(3), v => Assert.Equal(5, v));
            var huge = AtrTrailingComparison.Owned(bars, int.MaxValue, 3, basis);
            Assert.All(huge.Outputs.Values.SelectMany(v => v.Present!), v => Assert.False(v));
        }
    }

    [Fact]
    public void WideUnpublishedBandsCancelAndMidpointsRemainFinite()
    {
        foreach (var scale in new[] { double.MaxValue, double.Epsilon, 1e-200 })
        {
            var bars = Enumerable
                .Range(0, 50)
                .Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), scale, scale, -scale, scale, 0))
                .ToArray();
            var expected = AtrTrailingComparison.GridReference(bars, 3, 1, AtrTrailBasis.Close);
            Assert.All(
                expected.SelectMany(r => r).Where(v => v.HasValue),
                v => Assert.True(double.IsFinite(v!.Value))
            );
            ComparisonVerifier.Compare(
                AtrTrailingComparison.Series(expected),
                AtrTrailingComparison.Owned(bars, 3, 1, AtrTrailBasis.Close),
                "wide cancelling stops",
                IndicatorErrorBudget.Exact
            );
            if (scale == double.MaxValue)
                Assert.All(expected[0].Skip(3), v => Assert.Equal(-scale, v));
            var flat = bars.Select(b => new Bar(b.Time, scale, scale, scale, scale, 0)).ToArray();
            foreach (var basis in Enum.GetValues<AtrTrailBasis>())
                ComparisonVerifier.Compare(
                    AtrTrailingComparison.Series(
                        AtrTrailingComparison.GridReference(flat, 3, 3, basis)
                    ),
                    AtrTrailingComparison.Owned(flat, 3, 3, basis),
                    "wide flat stop",
                    IndicatorErrorBudget.Exact
                );
        }
    }

    [Fact]
    public void NativeDecimalBoundaryAndUndefinedMultipliersRemainVisible()
    {
        var data = CompetitorData.Create(30);
        foreach (var basis in Enum.GetValues<AtrTrailBasis>())
        {
            Assert.Throws<OverflowException>(() =>
                AtrTrailingComparison.Native(data, 3, double.MaxValue, basis)
            );
            Assert.Throws<OverflowException>(() =>
                AtrTrailingComparison.Native(data, 3, double.NaN, basis)
            );
            Assert.Throws<OverflowException>(() =>
                AtrTrailingComparison.NativeReference(data, 3, double.MaxValue, basis)
            );
            Assert.Throws<OverflowException>(() =>
                AtrTrailingComparison.NativeReference(data, 3, double.NaN, basis)
            );
        }
    }

    [Fact]
    public async Task ChainingAndEveryBandMaskAndBasisMutationAreDetected()
    {
        var data = CompetitorData.Create(100);
        foreach (var basis in Enum.GetValues<AtrTrailBasis>())
        {
            var owner = new SeededAtrTrailingStop(3, 1, basis);
            owner.Of(new FixedPeriodWma(3));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(owner)
                .BuildAsync();
            var closes = FixedWeightedComparison.Stage(data.Closes, 3, false);
            var bars = data
                .IndicatorBars.Select(
                    (b, i) => new Bar(b.Time, b.Open, b.High, b.Low, closes[i], b.Volume)
                )
                .ToArray();
            var expected = AtrTrailingComparison.GridReference(bars, 3, 1, basis);
            for (var j = 0; j < 3; j++)
            {
                Assert.Equal(expected[j].Select(v => v ?? 0), run[owner.Outputs[j]].ToArray());
                Assert.Equal(
                    expected[j].Select(v => v.HasValue ? 1d : 0),
                    run[owner.Outputs[j + 3]].ToArray()
                );
            }
            var pair = AtrTrailingComparison.Pair(basis, 1);
            foreach (var name in AtrTrailingComparison.Names)
            foreach (var native in new[] { false, true })
            foreach (var mask in new[] { false, true })
            {
                ComparisonSeries Bad(CompetitorData d, int p)
                {
                    var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                    var output = result.Outputs[name];
                    var index = Array.FindLastIndex(output.Present!, v => v);
                    Assert.True(index >= 0);
                    if (mask)
                        output.Present![index] = false;
                    else
                        output.Values[index] += 1;
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
            var other = basis == AtrTrailBasis.Close ? AtrTrailBasis.Midpoint : AtrTrailBasis.Close;
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    pair with
                    {
                        Library = AtrTrailingComparison.Pair(other, 1).Library,
                    },
                    data,
                    3
                )
            );
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    pair with
                    {
                        Library = AtrTrailingComparison.Pair(basis, 2).Library,
                    },
                    data,
                    3
                )
            );
        }
    }
}
