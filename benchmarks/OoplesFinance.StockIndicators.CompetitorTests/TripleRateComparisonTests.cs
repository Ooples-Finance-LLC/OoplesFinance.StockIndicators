using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using TALib;
using Xunit;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[CollectionDefinition("TA TRIX settings", DisableParallelization = true)]
public sealed class TripleRateSettingsCollection { }

[Collection("TA TRIX settings")]
public sealed class TripleRateComparisonTests
{
    private static Bar[] BarsOf(params double[] prices) =>
        prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();

    [Theory]
    [InlineData(TripleRateSeed.SharedMean)]
    [InlineData(TripleRateSeed.CascadedMeans)]
    [InlineData(TripleRateSeed.CascadedFirstPrice)]
    public async Task IndependentRationalContractsCoverLifecycleAndWideLookbacks(
        TripleRateSeed mode
    )
    {
        foreach (
            var config in new[]
            {
                (1, 1, 0),
                (3, 2, 0),
                (int.MaxValue, 2, 0),
                (2, int.MaxValue, 0),
                (3, 2, mode == TripleRateSeed.SharedMean ? 0 : 2),
            }
        )
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(TripleExponentialRate),
                    "triple rate " + mode,
                    () => new TripleExponentialRate(config.Item1, mode, config.Item2, config.Item3)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public void AllOutputsMatchGridAndNativeReferencesAcrossSettings()
    {
        foreach (var p in new[] { 1, 2, 3, 14 })
        foreach (var signal in new int?[] { null, 1, 3, 20 })
        {
            var pair = TripleRateComparison.Pair(false, signal);
            ComparisonVerifier.Check(pair, CompetitorData.Create(100), p);
            foreach (var shape in ComparisonVerifier.Shapes)
                ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 60), p);
        }
        foreach (var p in new[] { 2, 3, 14 })
        foreach (var firstPrice in new[] { false, true })
        foreach (var suppression in new[] { 0, 2 })
        {
            using var settings = new TripleRateComparison.Settings(firstPrice, suppression);
            var pair = TripleRateComparison.Pair(
                true,
                firstPrice: firstPrice,
                suppression: suppression
            );
            ComparisonVerifier.Check(pair, CompetitorData.Create(100), p);
            foreach (var shape in ComparisonVerifier.Shapes)
                ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 80), p);
        }
    }

    [Fact]
    public void KnownRatesSignalAlignmentAndZeroRulesRemainDistinct()
    {
        var output = TripleRateComparison
            .Owned(BarsOf(1, 2, 4, 8, 16), 1, TripleRateSeed.SharedMean, 2)
            .Outputs;
        Assert.Equal(new[] { false, true, true, true, true }, output["Trix"].Present);
        Assert.All(output["Trix"].Values.Skip(1), v => Assert.Equal(100, v));
        Assert.Equal(new[] { false, false, true, true, true }, output["Signal"].Present);
        Assert.All(output["Signal"].Values.Skip(2), v => Assert.Equal(100, v));
        Assert.Equal(new double[] { 2, 4, 8, 16 }, output["Ema3"].Values.Skip(1));
        var shared = TripleRateComparison
            .Owned(BarsOf(new double[15]), 3, TripleRateSeed.SharedMean, 2)
            .Outputs;
        Assert.All(shared["Trix"].Present!, v => Assert.False(v));
        Assert.All(shared["Signal"].Present!, v => Assert.False(v));
        Assert.True(shared["Ema3"].Present![3]);
        var ta = TripleRateComparison
            .Owned(BarsOf(new double[15]), 3, TripleRateSeed.CascadedMeans)
            .Outputs;
        Assert.Equal(Enumerable.Range(0, 15).Select(i => i >= 7), ta["Trix"].Present);
        Assert.Equal(Enumerable.Range(0, 15).Select(i => i >= 6), ta["Ema3"].Present);
        Assert.Throws<IndicatorOutputException>(() =>
            TripleRateComparison.Owned(BarsOf(0, 1), 1, TripleRateSeed.SharedMean, 2)
        );
        var recovered = TripleRateComparison
            .Owned(BarsOf(1, 0, 0, 0), 1, TripleRateSeed.SharedMean, 1)
            .Outputs;
        Assert.Equal(-100, recovered["Trix"].Values[1]);
        Assert.False(recovered["Trix"].Present![2]);
        Assert.False(recovered["Signal"].Present![2]);
    }

    [Fact]
    public void ExactRatiosHandleSignsSubnormalsAndOversizedPriceDifferences()
    {
        foreach (
            var prices in new[]
            {
                new[] { -double.MaxValue, double.MaxValue },
                new[] { double.Epsilon, 2 * double.Epsilon, 3 * double.Epsilon },
                new[] { -2d, -4, -8 },
            }
        )
        {
            var actual = TripleRateComparison.Owned(
                BarsOf(prices),
                1,
                TripleRateSeed.SharedMean,
                1
            );
            ComparisonVerifier.Compare(
                TripleRateComparison.Series(
                    TripleRateComparison.Reference(prices, 1, TripleRateSeed.SharedMean, 1),
                    false
                ),
                actual,
                "finite exact rates",
                IndicatorErrorBudget.Exact
            );
        }
        Assert.Equal(
            -200,
            TripleRateComparison
                .Owned(BarsOf(-double.MaxValue, double.MaxValue), 1, TripleRateSeed.SharedMean)
                .Outputs["Trix"]
                .Values[1]
        );
        Assert.Throws<IndicatorOutputException>(() =>
            TripleRateComparison.Owned(BarsOf(double.Epsilon, 1), 1, TripleRateSeed.SharedMean)
        );
    }

    [Fact]
    public void SkenderRoutesAndInvalidSignalDefinitionsRemainVisible()
    {
        var data = CompetitorData.Create(40);
        var quotes = data.Quotes;
        var tuples = quotes.Select(q => (q.Date, (double)q.Close)).ToArray();
        var expected = TripleRateComparison.NativeSkender(
            tuples.Select(v => v.Item2).ToArray(),
            3,
            2
        );
        foreach (
            var rows in new[]
            {
                quotes.GetTrix(3, 2),
                tuples.Reverse().GetTrix(3, 2),
                quotes.AsEnumerable().Reverse().GetTrix(3, 2),
                quotes.GetSma(1).GetTrix(3, 2),
            }
        )
        {
            var r = rows.ToArray();
            Assert.Equal(expected[0], r.Select(v => v.Trix));
            Assert.Equal(expected[1], r.Select(v => v.Ema3));
            Assert.Equal(expected[2], r.Select(v => v.Signal));
        }
        Assert.All(quotes.GetTrix(3), v => Assert.Null(v.Signal));
        Assert.True(double.IsNaN(quotes.GetTrix(3, 0).ElementAt(2).Signal!.Value));
        Assert.Equal(0, quotes.GetTrix(3, -1).Last().Signal);
        Assert.Throws<OverflowException>(() => quotes.GetTrix(int.MaxValue).ToArray());
        Assert.Throws<OverflowException>(() => quotes.GetTrix(3, int.MaxValue).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TripleExponentialRate(3, signalPeriod: 0)
        );
        var inf = new[] { (DateTime.UnixEpoch, 0d), (DateTime.UnixEpoch.AddDays(1), 1d) }
            .GetTrix(1)
            .Last();
        Assert.True(double.IsPositiveInfinity(inf.Trix!.Value));
        var sma = quotes.GetSma(3).ToArray();
        var chained = sma.GetTrix(3, 2).ToArray();
        Assert.All(chained.Take(2), v => Assert.Null(v.Trix));
        Assert.Equal(
            TripleRateComparison.NativeSkender(
                sma.Skip(2).Select(v => v.Sma!.Value).ToArray(),
                3,
                2
            )[0],
            chained.Skip(2).Select(v => v.Trix)
        );
    }

    [Fact]
    public void TaSubrangesAliasingFloatsAndInheritedEmaSettingsAreVerified()
    {
        var data = CompetitorData.Create(80);
        var buffer = new double[80];
        foreach (var first in new[] { false, true })
        foreach (var unstable in new[] { 0, 2 })
        {
            using var settings = new TripleRateComparison.Settings(first, unstable);
            var lookback = 3 * (2 + unstable) + 1;
            Assert.Equal(lookback, Functions.TrixLookback(3));
            Assert.Equal(
                TaCore.RetCode.Success,
                Functions.Trix<double>(data.Closes, 30..65, buffer, out var range, 3)
            );
            Assert.Equal(30..66, range);
            Assert.Equal(
                TripleRateComparison.NativeTaPacked(data.Closes, 3, first, unstable, 30, 65),
                buffer.Take(36)
            );
            var alias = (double[])data.Closes.Clone();
            Assert.Equal(
                TaCore.RetCode.Success,
                Functions.Trix<double>(alias, System.Range.All, alias, out _, 3)
            );
            Assert.Equal(
                TripleRateComparison
                    .NativeTa(data.Closes, 3, first, unstable)
                    .Skip(lookback)
                    .Select(v => v!.Value),
                alias.Take(80 - lookback)
            );
            var floats = Enumerable.Repeat(8f, 30).ToArray();
            var f = new float[30];
            Assert.Equal(
                TaCore.RetCode.Success,
                Functions.Trix<float>(floats, System.Range.All, f, out _, 3)
            );
            Assert.All(f.Take(30 - lookback), v => Assert.Equal(0, v));
        }
    }

    [Fact]
    public void TaPeriodOneAndLargeLookbackDefectsAndZeroPolicyAreExplicit()
    {
        using var settings = new TripleRateComparison.Settings(false, 0);
        var prices = new[] { 1d, 2, 3, 4, 5, 6 };
        var output = new double[6];
        Assert.Equal(-2, Functions.TrixLookback(1));
        Assert.Throws<IndexOutOfRangeException>(() =>
            Functions.Trix<double>(prices, System.Range.All, output, out _, 1)
        );
        Assert.Equal(-1, Functions.TrixLookback(0));
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.Trix<double>(prices, System.Range.All, output, out _, 0)
        );
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.Trix<double>(new[] { 1d }, System.Range.All, new double[1], out _, 2)
        );
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Trix<double>(new double[6], System.Range.All, output, out var range, 2)
        );
        Assert.Equal(4..6, range);
        Assert.All(output.Take(2), v => Assert.Equal(0, v));
        Assert.Equal(int.MaxValue - 4, Functions.TrixLookback(int.MaxValue));
    }

    [Fact]
    public async Task ChainingEveryOutputAndSignalMutationsAreCovered()
    {
        var data = CompetitorData.Create(40);
        var closes = FixedWeightedComparison.Stage(data.Closes, 3, false);
        foreach (var mode in Enum.GetValues<TripleRateSeed>())
        {
            var indicator = new TripleExponentialRate(3, mode, 2);
            indicator.Of(new FixedPeriodWma(3));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            var expected = TripleRateComparison.Reference(closes, 3, mode, 2);
            for (var j = 0; j < 3; j++)
            {
                Assert.Equal(expected[j].Select(v => v ?? 0), run[indicator.Outputs[j]].ToArray());
                Assert.Equal(
                    expected[j].Select(v => v.HasValue ? 1d : 0),
                    run[indicator.Outputs[j + 3]].ToArray()
                );
            }
        }
        foreach (
            var pair in new[]
            {
                TripleRateComparison.Pair(false, 2),
                TripleRateComparison.Pair(true),
            }
        )
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
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    data,
                    3
                )
            );
        }
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                TripleRateComparison.Pair(false, 2) with
                {
                    Library = TripleRateComparison.Pair(false, 3).Ooples,
                },
                data,
                3
            )
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new TripleExponentialRate(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TripleExponentialRate(3, (TripleRateSeed)3)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TripleExponentialRate(3, suppression: 1)
        );
    }
}
