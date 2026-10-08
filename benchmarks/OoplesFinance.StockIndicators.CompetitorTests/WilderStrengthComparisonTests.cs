using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[CollectionDefinition("TA strength settings", DisableParallelization = true)]
public sealed class StrengthSettingsCollection { }

[Collection("TA strength settings")]
public sealed class WilderStrengthComparisonTests
{
    private sealed class Settings : IDisposable
    {
        private readonly TaCore.CompatibilityMode _mode = TaCore.CompatibilitySettings.Get();
        private readonly int _rsi = TaCore.UnstablePeriodSettings.Get(TaCore.UnstableFunc.Rsi),
            _cmo = TaCore.UnstablePeriodSettings.Get(TaCore.UnstableFunc.Cmo);

        internal Settings()
        {
            TaCore.CompatibilitySettings.Set(TaCore.CompatibilityMode.Default);
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Rsi, 0);
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Cmo, 0);
        }

        public void Dispose()
        {
            TaCore.CompatibilitySettings.Set(_mode);
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Rsi, _rsi);
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Cmo, _cmo);
        }
    }

    [Theory]
    [InlineData(WilderStrengthConvention.RsiZeroFlat, 1, 0)]
    [InlineData(WilderStrengthConvention.RsiHundredFlat, 3, 0)]
    [InlineData(WilderStrengthConvention.ChandeZeroFlat, 14, 0)]
    [InlineData(WilderStrengthConvention.RsiZeroFlat, 3, 5)]
    [InlineData(WilderStrengthConvention.RsiZeroFlat, int.MaxValue, 0)]
    [InlineData(WilderStrengthConvention.RsiZeroFlat, 2, int.MaxValue)]
    public async Task IndependentContractsVerifySeedRecurrenceLifecycleAndSuppression(
        WilderStrengthConvention convention,
        int period,
        int unstable
    )
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(WilderStrengthOscillator),
                $"{convention}/{period}/{unstable}",
                () => new WilderStrengthOscillator(period, convention, unstable)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    private static Bar[] BarsFor(double[] prices) =>
        prices.Select((x, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), x, x, x, x, 1)).ToArray();

    private static void CheckOwned(
        double[] prices,
        int period,
        WilderStrengthConvention convention,
        int unstable = 0
    ) =>
        ComparisonVerifier.Compare(
            WilderStrengthComparison.OwnedReference(prices, period, convention, unstable),
            WilderStrengthComparison.Owned(BarsFor(prices), period, convention, unstable),
            "strength full-range reference",
            IndicatorErrorBudget.Exact
        );

    [Fact]
    public void FlatConventionsAndExactStartupAreExplicit()
    {
        using var settings = new Settings();
        foreach (var convention in Enum.GetValues<WilderStrengthConvention>())
        {
            var pair = WilderStrengthComparison.Create(convention);
            var flat = CompetitorData.FromCloses([1, 1, 1, 1, 1]);
            ComparisonVerifier.Check(pair, flat, 3);
            var result = pair.Ooples(flat, 3).Outputs["Value"];
            Assert.Equal(new[] { false, false, false, true, true }, result.Present);
            Assert.Equal(
                convention == WilderStrengthConvention.RsiHundredFlat ? 100 : 0,
                result.Values[3]
            );
            CheckOwned([0, 1, 2, 3, 4], 3, convention, 1);
            CheckOwned([0, 1, 0], 1, convention);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new WilderStrengthOscillator(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WilderStrengthOscillator(2, (WilderStrengthConvention)99)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WilderStrengthOscillator(2, unstablePeriods: -1)
        );
    }

    [Fact]
    public void TinyUnpublishedMeansAndWideMovementsRemainMeaningful()
    {
        foreach (var convention in Enum.GetValues<WilderStrengthConvention>())
        {
            foreach (
                var prices in new[]
                {
                    new[] { 0d, double.Epsilon, 0, 2 * double.Epsilon, 0 },
                    new[]
                    {
                        -double.MaxValue,
                        double.MaxValue,
                        -double.MaxValue,
                        0,
                        double.MaxValue,
                    },
                    new[] { 1d, Math.BitIncrement(1), 1, Math.BitDecrement(1), 1 },
                }
            )
                CheckOwned(prices, 2, convention);
            var output = WilderStrengthComparison
                .Owned(BarsFor([0, double.Epsilon, 0]), 2, convention)
                .Outputs["Value"];
            Assert.Equal(
                convention == WilderStrengthConvention.ChandeZeroFlat ? 0 : 50,
                output.Values[2]
            );
        }
        using var settings = new Settings();
        var actual = new double[3];
        Assert.Equal(
            TaCore.RetCode.Success,
            WilderStrengthComparison.Call(
                false,
                [0d, double.Epsilon, 0],
                System.Range.All,
                actual,
                out _,
                2
            )
        );
        Assert.Equal(0, actual[0]);
        Assert.Equal(
            TaCore.RetCode.Success,
            WilderStrengthComparison.Call(
                false,
                [-double.MaxValue, double.MaxValue, -double.MaxValue],
                System.Range.All,
                actual,
                out _,
                2
            )
        );
        Assert.True(double.IsNaN(actual[0]));
    }

    [Fact]
    public void LongDecayPreservesRatiosAndDiscardedPositiveTailsBreakMidpointTies()
    {
        var halving = new List<double> { 0, 1, 0 };
        halving.AddRange(Enumerable.Repeat(0d, 5000));
        foreach (var convention in Enum.GetValues<WilderStrengthConvention>())
        {
            CheckOwned(halving.ToArray(), 2, convention);
            var output = WilderStrengthComparison
                .Owned(BarsFor(halving.ToArray()), 2, convention)
                .Outputs["Value"];
            Assert.Equal(
                convention == WilderStrengthConvention.ChandeZeroFlat ? 0 : 50,
                output.Values[^1]
            );
        }
        var midpoint = new List<double> { -2, -1, -1, -1, -1 };
        midpoint.AddRange(Enumerable.Repeat(-1d, 12000));
        midpoint.Add(Math.ScaleB(1, -53));
        midpoint.Add(Math.ScaleB(1, -53) - .75);
        CheckOwned(midpoint.ToArray(), 4, WilderStrengthConvention.RsiZeroFlat);
        Assert.True(
            WilderStrengthComparison
                .Owned(BarsFor(midpoint.ToArray()), 4, WilderStrengthConvention.RsiZeroFlat)
                .Outputs["Value"]
                .Values[^1] > 50
        );
    }

    [Fact]
    public async Task ChainingUsesSelectedClose()
    {
        var data = WilderStrengthComparison.Fixture();
        var source = new PriceCircularTransform(PriceCircularOperation.Cosine);
        var indicator = new WilderStrengthOscillator(3);
        indicator.Of(source);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(source, indicator)
            .BuildAsync();
        var expected = WilderStrengthComparison
            .OwnedReference(run[source.Value].ToArray(), 3, WilderStrengthConvention.RsiZeroFlat)
            .Outputs["Value"];
        var actual = run[indicator.Value].ToArray();
        var flags = run[indicator.IsDefined].ToArray();
        for (var i = 0; i < actual.Length; i++)
        {
            Assert.Equal(expected.Present![i], flags[i] > 0);
            if (flags[i] > 0)
                Assert.Equal(expected.Values[i], actual[i]);
        }
    }

    [Fact]
    public void NativeSettingsRangesAndMetastockDefectMatchIndependentReferences()
    {
        using var settings = new Settings();
        double[] prices = [1, 3, 2, 5, 4, 8, 1, 7, 6, 9];
        foreach (var signed in new[] { false, true })
        foreach (var metastock in new[] { false, true })
        foreach (var unstable in new[] { 0, 1, 3 })
        {
            TaCore.CompatibilitySettings.Set(
                metastock ? TaCore.CompatibilityMode.Metastock : TaCore.CompatibilityMode.Default
            );
            TaCore.UnstablePeriodSettings.Set(
                signed ? TaCore.UnstableFunc.Cmo : TaCore.UnstableFunc.Rsi,
                unstable
            );
            var convention = signed
                ? WilderStrengthConvention.ChandeZeroFlat
                : WilderStrengthConvention.RsiZeroFlat;
            foreach (var start in new[] { 0, 4 })
            {
                var expected = WilderStrengthComparison.NativeReference(
                    prices,
                    3,
                    convention,
                    start,
                    8,
                    unstable,
                    metastock
                );
                var actual = new double[prices.Length];
                Assert.Equal(
                    TaCore.RetCode.Success,
                    WilderStrengthComparison.Call(
                        signed,
                        prices,
                        new System.Range(start, 8),
                        actual,
                        out var range,
                        3
                    )
                );
                Assert.Equal(expected.Range, range);
                Assert.Equal(expected.Values, actual.Take(expected.Values.Length));
            }
        }
        foreach (var signed in new[] { false, true })
        {
            TaCore.CompatibilitySettings.Set(TaCore.CompatibilityMode.Metastock);
            TaCore.UnstablePeriodSettings.Set(
                signed ? TaCore.UnstableFunc.Cmo : TaCore.UnstableFunc.Rsi,
                0
            );
            Assert.Throws<IndexOutOfRangeException>(() =>
                WilderStrengthComparison.Call(
                    signed,
                    prices.Take(3).ToArray(),
                    System.Range.All,
                    new double[3],
                    out _,
                    3
                )
            );
            var output = new double[4];
            WilderStrengthComparison.Call(
                signed,
                prices.Take(4).ToArray(),
                System.Range.All,
                output,
                out var range,
                3
            );
            Assert.Equal(2..3, range);
            var changed = prices.Take(4).ToArray();
            changed[3] = 100;
            var changedOutput = new double[4];
            WilderStrengthComparison.Call(
                signed,
                changed,
                System.Range.All,
                changedOutput,
                out _,
                3
            );
            Assert.NotEqual(output[0], changedOutput[0]); // The value labelled bar 2 reads bar 3.
        }
    }

    [Fact]
    public void NativeFloatDoubleAliasesFailuresAndSuppressionArePinned()
    {
        using var settings = new Settings();
        double[] original = [1, 3, 2, 5, 4, 8, 1, 7, 6, 9];
        foreach (var signed in new[] { false, true })
        {
            var convention = signed
                ? WilderStrengthConvention.ChandeZeroFlat
                : WilderStrengthConvention.RsiZeroFlat;
            var expected = WilderStrengthComparison.NativeReference(original, 3, convention);
            var alias = (double[])original.Clone();
            Assert.Equal(
                TaCore.RetCode.Success,
                WilderStrengthComparison.Call(
                    signed,
                    alias,
                    System.Range.All,
                    alias,
                    out var range,
                    3
                )
            );
            Assert.Equal(expected.Range, range);
            Assert.Equal(expected.Values, alias.Take(expected.Values.Length));
            var floats = original.Select(x => (float)x).ToArray();
            Assert.Equal(
                TaCore.RetCode.Success,
                WilderStrengthComparison.Call(signed, floats, System.Range.All, floats, out _, 3)
            );
            for (var i = 0; i < expected.Values.Length; i++)
                Assert.True(Math.Abs(floats[i] - expected.Values[i]) < .0001);
            foreach (var input in new[] { Array.Empty<double>(), new[] { 1d } })
                Assert.Equal(
                    TaCore.RetCode.OutOfRangeParam,
                    WilderStrengthComparison.Call(
                        signed,
                        input,
                        System.Range.All,
                        new double[1],
                        out _,
                        3
                    )
                );
            Assert.Equal(
                TaCore.RetCode.BadParam,
                WilderStrengthComparison.Call(
                    signed,
                    original,
                    System.Range.All,
                    new double[10],
                    out _,
                    1
                )
            );
            Assert.Throws<IndexOutOfRangeException>(() =>
                WilderStrengthComparison.Call(
                    signed,
                    original,
                    System.Range.All,
                    new double[1],
                    out _,
                    3
                )
            );
            TaCore.UnstablePeriodSettings.Set(
                signed ? TaCore.UnstableFunc.Cmo : TaCore.UnstableFunc.Rsi,
                2
            );
            var actual = new double[10];
            WilderStrengthComparison.Call(signed, original, System.Range.All, actual, out range, 3);
            Assert.Equal(5..10, range);
            var baseline = WilderStrengthComparison.NativeReference(original, 3, convention).Values;
            Assert.Equal(baseline.Skip(2), actual.Take(5));
            CheckOwned(original, 3, convention, 2);
        }
    }

    [Fact]
    public void SkenderQuoteTupleReusableAndFlatHundredRoutesAgree()
    {
        var data = WilderStrengthComparison.Fixture();
        var expected = data.Quotes.GetRsi(3).ToArray();
        var tuples = data.Quotes.Select(q => (q.Date, (double)q.Close)).ToArray();
        var reusable = tuples
            .Select(q => new AwesomeResult(q.Date) { Oscillator = q.Item2 })
            .Cast<IReusableResult>();
        foreach (
            var rows in new[]
            {
                tuples.Reverse().GetRsi(3).ToArray(),
                reusable.GetRsi(3).ToArray(),
                data.Quotes.AsEnumerable().Reverse().GetRsi(3).ToArray(),
            }
        )
        {
            Assert.Equal(expected.Select(r => r.Date), rows.Select(r => r.Date));
            Assert.Equal(expected.Select(r => r.Rsi), rows.Select(r => r.Rsi));
        }
        Assert.Equal(expected[3].Rsi, ((IReusableResult)expected[3]).Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetRsi(0).ToArray());
    }

    [Fact]
    public void ValuesAndPresenceCannotBeCorruptedSilently()
    {
        using var settings = new Settings();
        var data = WilderStrengthComparison.Fixture();
        foreach (var pair in WilderStrengthComparison.Pairs)
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
                    3
                )
            );
        }
    }
}
