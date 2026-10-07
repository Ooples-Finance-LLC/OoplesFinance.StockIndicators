using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SeededMomentumComparisonTests
{
    private static Bar[] BarsOf(params double[] prices) =>
        prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentRationalContractsCoverLifecycleAndWidePeriods(bool pmo)
    {
        foreach (
            var config in new[]
            {
                (2, 1, 1),
                (3, 2, 2),
                (int.MaxValue, 2, 2),
                (2, int.MaxValue, 2),
                (2, 2, int.MaxValue),
            }
        )
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    pmo ? typeof(SeededPriceMomentum) : typeof(SeededTrueStrength),
                    "seeded momentum",
                    () =>
                        pmo
                            ? new SeededPriceMomentum(config.Item1, config.Item2, config.Item3)
                            : new SeededTrueStrength(config.Item1, config.Item2, config.Item3)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public void CompleteTrajectoriesCoverPeriodCombinationsAndNativeUndefinedStartup()
    {
        foreach (var pmo in new[] { false, true })
        foreach (
            var config in pmo
                ? new[] { (2, 1, 1), (3, 2, 2), (2, 5, 3), (35, 20, 10) }
                : new[] { (1, 1, 0), (2, 1, 1), (2, 1, 2), (2, 2, 1), (3, 2, 2), (25, 13, 7) }
        )
        {
            var pair = SeededMomentumComparison.Pair(pmo, config.Item2, config.Item3);
            SeededMomentumComparison.Check(pair, CompetitorData.Create(100), config.Item1);
            foreach (var shape in ComparisonVerifier.Shapes)
                SeededMomentumComparison.Check(
                    pair,
                    ComparisonVerifier.Fixture(shape, 80),
                    config.Item1
                );
        }
    }

    [Fact]
    public void TsiSignalOneAndShortenedSmoothingOneSeedAreExplicit()
    {
        var bars = BarsOf(Enumerable.Range(0, 10).Select(i => (double)i).ToArray());
        var normal = SeededMomentumComparison.Owned(bars, 2, 2, 2, false).Outputs;
        Assert.Equal(Enumerable.Range(0, 10).Select(i => i >= 3), normal["Tsi"].Present);
        Assert.Equal(Enumerable.Range(0, 10).Select(i => i >= 4), normal["Signal"].Present);
        Assert.All(normal["Tsi"].Values.Skip(3), v => Assert.Equal(100, v));
        Assert.All(normal["Signal"].Values.Skip(4), v => Assert.Equal(100, v));
        var shortened = SeededMomentumComparison.Owned(bars, 2, 1, 2, false).Outputs;
        Assert.Equal(100, shortened["Tsi"].Values[3]);
        Assert.Equal(50, shortened["Signal"].Values[3]);
        Assert.Equal(250d / 3, shortened["Signal"].Values[4]);
        foreach (var signal in new[] { 0, 1 })
            Assert.All(
                SeededMomentumComparison
                    .Owned(bars, 2, 2, signal, false)
                    .Outputs["Signal"]
                    .Present!,
                v => Assert.False(v)
            );
    }

    [Fact]
    public void NativePresentNanIsVerifiedWithoutMaskingItAsAbsent()
    {
        var data = CompetitorData.FromCloses(Enumerable.Repeat(1d, 10).ToArray());
        var pair = SeededMomentumComparison.Pair(false, 2, 2);
        var native = pair.Competitor(data, 2).Outputs;
        Assert.True(native["Tsi"].Present![3]);
        Assert.True(double.IsNaN(native["Tsi"].Values[3]));
        Assert.False(native["Tsi"].Present![4]);
        Assert.True(native["Signal"].Present![4]);
        Assert.True(double.IsNaN(native["Signal"].Values[4]));
        Assert.False(native["Signal"].Present![5]);
        Assert.All(
            pair.Ooples(data, 2).Outputs.Values,
            row => Assert.All(row.Present!, v => Assert.False(v))
        );
        Assert.True(SeededMomentumComparison.Check(pair, data, 2) > 0);
        foreach (var remove in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var r = pair.Competitor(d, p);
                if (remove)
                    r.Outputs["Tsi"].Present![3] = false;
                else
                    r.Outputs["Tsi"].Values[3] = 0;
                return r;
            }
            Assert.Throws<InvalidOperationException>(() =>
                SeededMomentumComparison.Check(pair with { Competitor = Bad }, data, 2)
            );
        }
        var recovered = SeededMomentumComparison
            .Owned(BarsOf(1, 1, 1, 1, 2, 3, 4, 5), 2, 2, 2, false)
            .Outputs;
        Assert.True(recovered["Tsi"].Present![4]);
        Assert.All(recovered["Signal"].Present!, v => Assert.False(v));
    }

    [Fact]
    public void PmoScaleOvershootAndMissingReturnPropagationAreKnown()
    {
        var exponential = SeededMomentumComparison
            .Owned(BarsOf(1, 2, 4, 8, 16, 32), 2, 2, 2, true)
            .Outputs;
        Assert.Equal(new[] { false, false, false, true, true, true }, exponential["Pmo"].Present);
        Assert.Equal(
            new[] { false, false, false, false, true, true },
            exponential["Signal"].Present
        );
        Assert.All(exponential["Pmo"].Values.Skip(3), v => Assert.Equal(1000, v));
        Assert.All(exponential["Signal"].Values.Skip(4), v => Assert.Equal(1000, v));
        var overshoot = SeededMomentumComparison
            .Owned(BarsOf(1, 2, 2, 4, 4), 2, 1, 1, true)
            .Outputs;
        Assert.Equal(new double[] { 500, 1500, -1500 }, overshoot["Pmo"].Values.Skip(2));
        Assert.Equal(overshoot["Pmo"].Values, overshoot["Signal"].Values);
        var poison = SeededMomentumComparison
            .Owned(BarsOf(1, 2, 4, 0, 1, 2, 4), 2, 1, 1, true)
            .Outputs;
        Assert.True(poison["Pmo"].Present![3]);
        Assert.All(poison["Pmo"].Present!.Skip(4), v => Assert.False(v));
        var initial = SeededMomentumComparison
            .Owned(BarsOf(0, 1, 2, 3, 4, 5), 2, 1, 1, true)
            .Outputs;
        Assert.All(initial["Pmo"].Present!, v => Assert.False(v));
    }

    [Fact]
    public void ExtendedChangesAndHiddenRateCancellationRemainFinite()
    {
        var huge = new[]
        {
            -double.MaxValue,
            double.MaxValue,
            -double.MaxValue,
            double.MaxValue,
            -double.MaxValue,
            double.MaxValue,
        };
        var tsi = SeededMomentumComparison.Owned(BarsOf(huge), 2, 2, 2, false);
        ComparisonVerifier.Compare(
            SeededMomentumComparison.Series(
                SeededMomentumComparison.GridReference(huge, 2, 2, 2, false),
                false
            ),
            tsi,
            "extended TSI",
            IndicatorErrorBudget.Exact
        );
        Assert.All(
            tsi.Outputs["Tsi"].Values.Where((_, i) => tsi.Outputs["Tsi"].Present![i]),
            v => Assert.InRange(v, -100, 100)
        );
        var cancellation = new[] { double.Epsilon, 1, -double.Epsilon, 1 };
        var pmo = SeededMomentumComparison.Owned(BarsOf(cancellation), 3, 1, 1, true);
        ComparisonVerifier.Compare(
            SeededMomentumComparison.Series(
                SeededMomentumComparison.GridReference(cancellation, 3, 1, 1, true),
                true
            ),
            pmo,
            "hidden return cancellation",
            IndicatorErrorBudget.Exact
        );
        Assert.True(double.IsFinite(pmo.Outputs["Pmo"].Values[3]));
        var tuples = cancellation.Select((v, i) => (DateTime.UnixEpoch.AddDays(i), v));
        Assert.True(double.IsNaN(tuples.GetPmo(3, 1, 1).Last().Pmo!.Value));
        Assert.Throws<IndicatorOutputException>(() =>
            SeededMomentumComparison.Owned(BarsOf(double.Epsilon, 1, 1), 2, 1, 1, true)
        );
    }

    [Fact]
    public void QuoteTupleReusableSortedDefaultAndParameterRoutesAreCovered()
    {
        var data = CompetitorData.Create(100);
        var q = data.Quotes;
        var t = q.Select(v => (v.Date, (double)v.Close)).ToArray();
        Assert.Equal(
            q.GetTsi().Select(v => (v.Tsi, v.Signal)),
            q.GetTsi(25, 13, 7).Select(v => (v.Tsi, v.Signal))
        );
        Assert.Equal(
            q.GetPmo().Select(v => (v.Pmo, v.Signal)),
            q.GetPmo(35, 20, 10).Select(v => (v.Pmo, v.Signal))
        );
        foreach (
            var rows in new[]
            {
                q.AsEnumerable().Reverse().GetTsi(3, 2, 2),
                t.Reverse().GetTsi(3, 2, 2),
                q.GetSma(1).GetTsi(3, 2, 2),
            }
        )
            Assert.Equal(
                q.GetTsi(3, 2, 2).Select(v => (v.Tsi, v.Signal)),
                rows.Select(v => (v.Tsi, v.Signal))
            );
        foreach (
            var rows in new[]
            {
                q.AsEnumerable().Reverse().GetPmo(3, 2, 2),
                t.Reverse().GetPmo(3, 2, 2),
                q.GetSma(1).GetPmo(3, 2, 2),
            }
        )
            Assert.Equal(
                q.GetPmo(3, 2, 2).Select(v => (v.Pmo, v.Signal)),
                rows.Select(v => (v.Pmo, v.Signal))
            );
        var sma = q.GetSma(3).ToArray();
        var prices = sma.Skip(2).Select(v => v.Sma!.Value).ToArray();
        Assert.Equal(
            SeededMomentumComparison.NativeReference(prices, 3, 2, 2, false)[0],
            sma.GetTsi(3, 2, 2).Skip(2).Select(v => v.Tsi)
        );
        Assert.Equal(
            SeededMomentumComparison.NativeReference(prices, 3, 2, 2, true)[1],
            sma.GetPmo(3, 2, 2).Skip(2).Select(v => v.Signal)
        );
        Assert.Throws<OverflowException>(() => q.GetTsi(int.MaxValue).ToArray());
        Assert.Throws<OverflowException>(() => q.GetPmo(int.MaxValue).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => q.GetTsi(0).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => q.GetTsi(2, 2, -1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => q.GetPmo(1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededTrueStrength(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededTrueStrength(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededTrueStrength(2, 2, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededPriceMomentum(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededPriceMomentum(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededPriceMomentum(2, 2, 0));
    }

    [Fact]
    public async Task ChainingAndEveryValuePresenceAndSignalMutationAreDetected()
    {
        var data = CompetitorData.Create(40);
        var closes = FixedWeightedComparison.Stage(data.Closes, 3, false);
        foreach (var pmo in new[] { false, true })
        {
            MultiOutputIndicatorBase indicator = pmo
                ? new SeededPriceMomentum(3, 2, 2)
                : new SeededTrueStrength(3, 2, 2);
            indicator.Of(new FixedPeriodWma(3));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            var expected = SeededMomentumComparison.GridReference(closes, 3, 2, 2, pmo);
            for (var j = 0; j < 2; j++)
            {
                Assert.Equal(expected[j].Select(v => v ?? 0), run[indicator.Outputs[j]].ToArray());
                Assert.Equal(
                    expected[j].Select(v => v.HasValue ? 1d : 0),
                    run[indicator.Outputs[j + 2]].ToArray()
                );
            }
            var pair = SeededMomentumComparison.Pair(pmo, 2, 2);
            foreach (var name in pair.OutputNames!)
            foreach (var native in new[] { false, true })
            foreach (var presence in new[] { false, true })
            {
                ComparisonSeries Bad(CompetitorData d, int p)
                {
                    var r = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                    if (presence)
                        r.Outputs[name].Present![^1] = false;
                    else
                        r.Outputs[name].Values[^1] += 1;
                    return r;
                }
                Assert.Throws<InvalidOperationException>(() =>
                    SeededMomentumComparison.Check(
                        native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                        data,
                        3
                    )
                );
            }
            Assert.Throws<InvalidOperationException>(() =>
                SeededMomentumComparison.Check(
                    pair with
                    {
                        Library = SeededMomentumComparison.Pair(pmo, 2, 3).Ooples,
                    },
                    data,
                    3
                )
            );
        }
    }
}
