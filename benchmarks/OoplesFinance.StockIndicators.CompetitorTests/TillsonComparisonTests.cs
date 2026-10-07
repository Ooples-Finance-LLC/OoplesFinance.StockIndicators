using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using TALib;
using Xunit;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[CollectionDefinition("TA T3 settings", DisableParallelization = true)]
public sealed class TillsonSettingsCollection { }

[Collection("TA T3 settings")]
public sealed class TillsonComparisonTests
{
    private static Bar[] BarsOf(params double[] values) =>
        values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();

    [Theory]
    [InlineData(TillsonSeed.FirstPrice)]
    [InlineData(TillsonSeed.FollowingPrefix)]
    [InlineData(TillsonSeed.CascadedMeans)]
    public async Task IndependentRationalContractsCoverLifecycleAndWideParameters(TillsonSeed seed)
    {
        foreach (
            var config in new[]
            {
                (1, .7, 0),
                (3, .7, 0),
                (3, -.5, 2),
                (int.MaxValue, 2d, 0),
                (2, 0d, int.MaxValue),
            }
        )
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(TillsonAverage),
                    "T3 " + seed,
                    () => new TillsonAverage(config.Item1, config.Item2, seed, config.Item3)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public void IndependentGridAndNativeReferencesCoverAllSeedAndFactorConfigurations()
    {
        for (var variant = 0; variant < 3; variant++)
            foreach (
                var factor in variant == 1 ? new[] { 0, .7, 1d }
                : variant == 0 ? new[] { .2, .7, 2 }
                : new[] { -.5, 0, .7, 2 }
            )
            foreach (var p in variant == 1 ? new[] { 2, 3, 7, 14 } : new[] { 1, 2, 3, 7, 14 })
            foreach (var sma in variant == 2 ? new[] { true, false } : new[] { true })
            {
                var pair = TillsonComparison.Pair(variant, factor, sma);
                ComparisonVerifier.Check(pair, CompetitorData.Create(100), p);
                foreach (var shape in ComparisonVerifier.Shapes)
                    ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 90), p);
            }
    }

    [Fact]
    public void PrefixExcludesFirstPriceAndCascadeHasSixSeedWindows()
    {
        var bars = BarsOf(4, 8, 2, 12, 6, 3, 9, 2, 4, 5, 6, 7, 8, 9);
        var prefix = TillsonComparison.Owned(bars, 3, 0, TillsonSeed.FollowingPrefix).Outputs[
            "Value"
        ];
        Assert.Equal(4, prefix.Values[0]);
        Assert.Equal(8, prefix.Values[1]);
        Assert.Equal(7.25, prefix.Values[2]);
        var cascade = TillsonComparison.Owned(bars, 3, .7, TillsonSeed.CascadedMeans).Outputs[
            "Value"
        ];
        Assert.Equal(Enumerable.Range(0, bars.Length).Select(i => i >= 12), cascade.Present);
        foreach (var seed in Enum.GetValues<TillsonSeed>())
        {
            var one = TillsonComparison.Owned(bars, 1, 2, seed).Outputs["Value"];
            Assert.Equal(bars.Select(b => b.Close), one.Values);
            Assert.All(one.Present!, Assert.True);
        }
    }

    [Fact]
    public void ExactPolynomialRetainsConstantCancellationAndRejectsTrueOverflow()
    {
        foreach (var seed in Enum.GetValues<TillsonSeed>())
        foreach (var value in new[] { double.MaxValue, -double.MaxValue, double.Epsilon })
        {
            var bars = BarsOf(Enumerable.Repeat(value, 15).ToArray());
            var result = TillsonComparison.Owned(bars, 3, double.MaxValue, seed).Outputs["Value"];
            Assert.All(
                result.Values.Where((_, i) => result.Present![i]),
                v => Assert.Equal(value, v)
            );
            ComparisonVerifier.Compare(
                VolumePriceComparison.Mask(
                    TillsonComparison.Reference(
                        bars.Select(b => b.Close).ToArray(),
                        3,
                        double.MaxValue,
                        seed
                    )
                ),
                new ComparisonSeries(
                    new Dictionary<string, ComparisonOutput> { { "Value", result } }
                ),
                "exact T3 cancellation",
                IndicatorErrorBudget.Exact
            );
        }
        Assert.Throws<IndicatorOutputException>(() =>
            TillsonComparison.Owned(BarsOf(0, 1), 3, double.MaxValue, TillsonSeed.FirstPrice)
        );
        var alternating = BarsOf(
            double.MaxValue,
            -double.MaxValue,
            double.MaxValue,
            -double.MaxValue
        );
        ComparisonVerifier.Compare(
            VolumePriceComparison.Mask(
                TillsonComparison.Reference(
                    alternating.Select(b => b.Close).ToArray(),
                    3,
                    0,
                    TillsonSeed.FirstPrice
                )
            ),
            TillsonComparison.Owned(alternating, 3, 0, TillsonSeed.FirstPrice),
            "finite oversized differences",
            IndicatorErrorBudget.Exact
        );
    }

    [Fact]
    public void SkenderQuoteTupleReusableSortingDefaultsAndNonfiniteLimits()
    {
        var data = CompetitorData.Create(30);
        var quotes = data.Quotes;
        var tuples = quotes.Select(q => (q.Date, (double)q.Close)).ToArray();
        var expected = TillsonComparison.NativeReference(
            tuples.Select(v => v.Item2).ToArray(),
            3,
            .7,
            0
        );
        Assert.Equal(expected, quotes.GetT3(3).Select(v => v.T3));
        Assert.Equal(expected, tuples.Reverse().GetT3(3).Select(v => v.T3));
        Assert.Equal(expected, quotes.AsEnumerable().Reverse().GetT3(3).Select(v => v.T3));
        Assert.Equal(expected, quotes.GetSma(1).GetT3(3).Select(v => v.T3));
        Assert.Equal(quotes.GetT3(5, .7).Select(v => v.T3), quotes.GetT3().Select(v => v.T3));
        var sma = quotes.GetSma(3).ToArray();
        var chain = sma.GetT3(3).Select(v => v.T3).ToArray();
        Assert.All(chain.Take(2), v => Assert.Null(v));
        Assert.Equal(
            TillsonComparison.NativeReference(
                sma.Skip(2).Select(v => v.Sma!.Value).ToArray(),
                3,
                .7,
                0
            ),
            chain.Skip(2)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => quotes.GetT3(3, 0).ToArray());
        var nan = quotes.GetT3(3, double.NaN).ToArray();
        Assert.NotNull(nan[0].T3);
        Assert.All(nan.Skip(1), v => Assert.Null(v.T3));
        Assert.Throws<OverflowException>(() => tuples.GetT3(int.MaxValue).ToArray());
    }

    [Fact]
    public void TaSettingsSubrangesAliasingFloatsAndParameterBoundaries()
    {
        var old = TaCore.UnstablePeriodSettings.Get(TaCore.UnstableFunc.T3);
        try
        {
            var data = CompetitorData.Create(60);
            var buffer = new double[60];
            foreach (var unstable in new[] { 0, 3 })
            {
                TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.T3, unstable);
                ComparisonVerifier.Check(
                    TillsonComparison.Pair(1, .7, suppression: unstable),
                    data,
                    3
                );
                var lookback = 12 + unstable;
                Assert.Equal(lookback, Functions.T3Lookback(3));
                Assert.Equal(
                    TaCore.RetCode.Success,
                    Functions.T3<double>(data.Closes, 20..45, buffer, out var range, 3, .7)
                );
                Assert.Equal(20..46, range);
                Assert.Equal(
                    TillsonComparison
                        .NativeReference(
                            data.Closes.Skip(20 - lookback).Take(26 + lookback).ToArray(),
                            3,
                            .7,
                            1,
                            suppression: unstable
                        )
                        .Skip(lookback)
                        .Select(v => v!.Value),
                    buffer.Take(26)
                );
                var alias = (double[])data.Closes.Clone();
                Assert.Equal(
                    TaCore.RetCode.Success,
                    Functions.T3<double>(alias, System.Range.All, alias, out _, 3, .7)
                );
                Assert.Equal(
                    TillsonComparison
                        .NativeReference(data.Closes, 3, .7, 1, suppression: unstable)
                        .Skip(lookback)
                        .Select(v => v!.Value),
                    alias.Take(60 - lookback)
                );
                var floats = Enumerable.Repeat(8f, 30).ToArray();
                var result = new float[30];
                Assert.Equal(
                    TaCore.RetCode.Success,
                    Functions.T3<float>(floats, System.Range.All, result, out _, 3, 0)
                );
                Assert.All(result.Take(30 - lookback), v => Assert.Equal(8, v));
            }
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.T3, 0);
            Assert.Equal(-1, Functions.T3Lookback(1));
            Assert.Equal(-12, Functions.T3Lookback(int.MaxValue));
            Assert.Equal(
                TaCore.RetCode.BadParam,
                Functions.T3<double>(data.Closes, System.Range.All, buffer, out _, 1, .7)
            );
            Assert.Equal(
                TaCore.RetCode.BadParam,
                Functions.T3<double>(data.Closes, System.Range.All, buffer, out _, 3, 2)
            );
            Assert.Equal(
                TaCore.RetCode.Success,
                Functions.T3<double>(data.Closes, System.Range.All, buffer, out _, 3, double.NaN)
            );
            Assert.True(double.IsNaN(buffer[0]));
        }
        finally
        {
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.T3, old);
        }
    }

    [Fact]
    public void QuanSubscriptionRevisionHotStateAndResetDefectAreExplicit()
    {
        foreach (var useSma in new[] { true, false })
        {
            var source = new QuanTAlib.TSeries();
            var subscribed = new QuanTAlib.T3(source, 3, .7, useSma);
            var direct = new QuanTAlib.T3(3, .7, useSma);
            var values = new[] { 4d, 8, 2, 12, 3 };
            for (var i = 0; i < values.Length; i++)
            {
                var tick = new QuanTAlib.TValue(values[i], true, false);
                source.Add(tick);
                var row = direct.Calc(tick);
                Assert.Equal(row.Value, subscribed.Value);
                Assert.Equal(i >= 2, row.IsHot);
                var revised = direct.Calc(new QuanTAlib.TValue(values[i] + 1, false, false)).Value;
                var expected = TillsonComparison.NativeReference(
                    values.Take(i).Append(values[i] + 1).ToArray(),
                    3,
                    .7,
                    2,
                    useSma
                )[^1];
                Assert.Equal(expected, revised);
                direct.Calc(new QuanTAlib.TValue(values[i], false, false));
            }
            direct.Init();
            var next = direct.Calc(new QuanTAlib.TValue(8, true, false));
            Assert.Equal(TillsonComparison.NativeReference([0, 8], 3, .7, 2, false)[1], next.Value);
            Assert.True(next.IsHot);
            Assert.NotEqual(
                new QuanTAlib.T3(3, .7, useSma).Calc(new QuanTAlib.TValue(8, true, false)).Value,
                next.Value
            );
        }
        Assert.True(
            double.IsNaN(
                new QuanTAlib.T3(3, double.NaN).Calc(new QuanTAlib.TValue(1, true, false)).Value
            )
        );
    }

    [Fact]
    public async Task OwnedChainingAndAllMutationsAreVerified()
    {
        var data = CompetitorData.Create(50);
        var closes = FixedWeightedComparison.Stage(data.Closes, 3, false);
        foreach (var seed in Enum.GetValues<TillsonSeed>())
        {
            var indicator = new TillsonAverage(3, .7, seed);
            indicator.Of(new FixedPeriodWma(3));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            var expected = TillsonComparison.Reference(closes, 3, .7, seed);
            Assert.Equal(expected.Select(v => v ?? 0), run[indicator.Value].ToArray());
            Assert.Equal(
                expected.Select(v => v.HasValue ? 1d : 0),
                run[indicator.IsDefined].ToArray()
            );
        }
        foreach (var pair in TillsonComparison.Pairs)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                if (presence)
                    result.Outputs["Value"].Present![^1] = false;
                else
                    result.Outputs["Value"].Values[^1] += 1;
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
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                TillsonComparison.Pair(2) with
                {
                    Library = TillsonComparison.Pair(2, useSma: false).Ooples,
                },
                data,
                3
            )
        );
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                TillsonComparison.Pair(0) with
                {
                    Library = TillsonComparison.Pair(0, .5).Ooples,
                },
                data,
                3
            )
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new TillsonAverage(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TillsonAverage(3, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TillsonAverage(3, .7, (TillsonSeed)3));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TillsonAverage(3, .7, suppression: -1)
        );
    }
}
