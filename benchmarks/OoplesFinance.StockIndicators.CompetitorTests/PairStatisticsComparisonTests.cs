using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using TALib;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class PairStatisticsComparisonTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 3)]
    [InlineData(true, 1)]
    [InlineData(true, 3)]
    public async Task IndependentLifecycleContracts(bool full, int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                full ? typeof(WindowPairStatistics) : typeof(WindowCorrelation),
                "paired statistics",
                () =>
                    full
                        ? new WindowPairStatistics(period)
                        : new WindowCorrelation(period, flatZero: true)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void KnownMomentsFlatPoliciesAndAllPresenceFlags()
    {
        var data = CompetitorData.FromBodies([6, 4, 2], [1, 2, 3]);
        var full = PairStatisticsComparison.Create(true).Ooples(data, 3);
        foreach (var name in PairStatisticsComparison.FullNames)
            Assert.Equal(new[] { false, false, true }, full.Outputs[name].Present);
        Assert.Equal(-1, full.Outputs["Correlation"].Values[2]);
        Assert.Equal(1, full.Outputs["RSquared"].Values[2]);
        Assert.Equal(-4d / 3, full.Outputs["Covariance"].Values[2]);
        Assert.Equal(2d / 3, full.Outputs["VarianceA"].Values[2]);
        Assert.Equal(8d / 3, full.Outputs["VarianceB"].Values[2]);
        var flat = CompetitorData.FromBodies([3, 3, 3, 4], [1, 2, 3, 4]);
        var stats = PairStatisticsComparison.Create(true).Ooples(flat, 3);
        Assert.False(stats.Outputs["Correlation"].Present![2]);
        Assert.True(stats.Outputs["Correlation"].Present![3]);
        Assert.Equal(0, stats.Outputs["VarianceB"].Values[2]);
        Assert.Equal(
            0,
            PairStatisticsComparison.Create(false).Ooples(flat, 3).Outputs["Correlation"].Values[2]
        );
        foreach (var pair in PairStatisticsComparison.Pairs)
        foreach (var period in new[] { 1, 2, 3, 20 })
            ComparisonVerifier.Check(pair, PairStatisticsComparison.Fixture(), period);
    }

    private static Bar[] BarsFor(double[] x, double[] y) =>
        x.Select(
                (v, i) =>
                    new Bar(
                        DateTime.UnixEpoch.AddDays(i),
                        y[i],
                        Math.Max(v, y[i]),
                        Math.Min(v, y[i]),
                        v,
                        0
                    )
            )
            .ToArray();

    [Fact]
    public void TinyAndAdjacentValuesRetainExactCorrelation()
    {
        foreach (
            var x in new[]
            {
                new[] { 0d, double.Epsilon, 2 * double.Epsilon },
                new[] { 1d, Math.BitIncrement(1), Math.BitIncrement(Math.BitIncrement(1)) },
            }
        )
        {
            var y = x.Reverse().ToArray();
            var bars = BarsFor(x, y);
            var result = PairStatisticsComparison.Owned(bars, 3, true);
            Assert.Equal(-1, result.Outputs["Correlation"].Values[2]);
            Assert.Equal(1, result.Outputs["RSquared"].Values[2]);
            var expected = PairStatisticsComparison.Reference(x, y, 3, true);
            ComparisonVerifier.Compare(
                RetrospectivePriceComparison.Series(PairStatisticsComparison.FullNames, expected),
                result,
                "tiny pair"
            );
        }
    }

    [Fact]
    public void ScalarCorrelationSurvivesUnrepresentableVariances()
    {
        var x = new[] { double.MaxValue, -double.MaxValue, 0 };
        var y = x.Reverse().ToArray();
        var bars = BarsFor(x, y);
        var result = PairStatisticsComparison.Owned(bars, 3, false);
        Assert.Equal(.5, result.Outputs["Correlation"].Values[2]);
        Assert.Throws<IndicatorOutputException>(() =>
            PairStatisticsComparison.Owned(bars, 3, true)
        );
        Assert.All(
            PairStatisticsComparison.Owned(bars, int.MaxValue, true).Outputs.Values,
            v => Assert.All(v.Present!, p => Assert.False(p))
        );
    }

    [Fact]
    public void SkenderQuoteTupleReusableSortingAndDateValidation()
    {
        var data = PairStatisticsComparison.Fixture();
        var a = data.Dates.Zip(data.Closes, (d, v) => (d, v)).ToArray();
        var b = data.Dates.Zip(data.Opens, (d, v) => (d, v)).ToArray();
        var expected = PairStatisticsComparison.FromSkender(a.GetCorrelation(b, 3));
        var quotesB = data
            .Quotes.Select(q => new Quote
            {
                Date = q.Date,
                Open = q.Open,
                High = q.High,
                Low = q.Low,
                Close = q.Open,
                Volume = q.Volume,
            })
            .ToArray();
        IReusableResult[] ra = a.Select(p => (IReusableResult)new SmaResult(p.d) { Sma = p.v })
            .ToArray();
        IReusableResult[] rb = b.Select(p => (IReusableResult)new SmaResult(p.d) { Sma = p.v })
            .ToArray();
        foreach (
            var rows in new[]
            {
                a.Reverse().GetCorrelation(b.Reverse(), 3),
                data.Quotes.GetCorrelation(quotesB, 3),
                ra.GetCorrelation(rb, 3),
            }
        )
            ComparisonVerifier.Compare(
                expected,
                PairStatisticsComparison.FromSkender(rows),
                "Skender route"
            );
        Assert.Throws<InvalidQuotesException>(() => a.GetCorrelation(b.Skip(1), 3).ToArray());
        var changed = (ValueTuple<DateTime, double>[])b.Clone();
        changed[3].Item1 = changed[3].Item1.AddHours(1);
        Assert.Throws<InvalidQuotesException>(() => a.GetCorrelation(changed, 3).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => a.GetCorrelation(b, 0).ToArray());
    }

    [Fact]
    public void NativeOneElementRangeIsRejectedWhileOwnedStartupIsDefined()
    {
        foreach (var period in new[] { 1, 3 })
        {
            Assert.Equal(
                TALib.Core.RetCode.OutOfRangeParam,
                Functions.Correl<double>(
                    new[] { 2d },
                    new[] { 3d },
                    System.Range.All,
                    new double[1],
                    out _,
                    period
                )
            );
            var owned = PairStatisticsComparison
                .Create(false)
                .Ooples(CompetitorData.FromBodies([3], [2]), period)
                .Outputs["Correlation"];
            Assert.Equal(period == 1, owned.Present![0]);
            if (period == 1)
                Assert.Equal(0, owned.Values[0]);
        }
    }

    [Fact]
    public void TaLibRangesAliasingAndParameterCodes()
    {
        var data = PairStatisticsComparison.Fixture();
        var expected = PairStatisticsComparison
            .Create(false)
            .Competitor(data, 3)
            .Outputs["Correlation"]
            .Values;
        var output = new double[data.Count];
        Assert.Equal(
            TALib.Core.RetCode.Success,
            Functions.Correl<double>(data.Closes, data.Opens, 5..12, output, out var range, 3)
        );
        // TA-Lib treats a concrete input end as inclusive; output ranges are exclusive.
        Assert.Equal(5..13, range);
        var ranged = PairStatisticsComparison.NativeReference(
            data.Closes.Skip(3).Take(10).ToArray(),
            data.Opens.Skip(3).Take(10).ToArray(),
            3,
            false
        )[0];
        Assert.Equal(ranged.Skip(2).Select(v => v!.Value), output.Take(8));
        var aliased = (double[])data.Closes.Clone();
        Assert.Equal(
            TALib.Core.RetCode.Success,
            Functions.Correl<double>(aliased, data.Opens, System.Range.All, aliased, out range, 3)
        );
        Assert.Equal(expected.Skip(2), aliased.Take(data.Count - 2));
        Assert.Equal(
            TALib.Core.RetCode.BadParam,
            Functions.Correl<double>(data.Closes, data.Opens, System.Range.All, output, out _, 0)
        );
        Assert.Equal(-1, Functions.CorrelLookback(0));
        Assert.Equal(2, Functions.CorrelLookback(3));
    }

    [Fact]
    public void NativeCancellationAndOverflowAreVisible()
    {
        var dates = Enumerable.Range(0, 3).Select(i => DateTime.UnixEpoch.AddDays(i)).ToArray();
        var tiny = new[] { 0d, double.Epsilon, 2 * double.Epsilon };
        var native = dates
            .Zip(tiny, (d, v) => (d, v))
            .GetCorrelation(dates.Zip(tiny.Reverse(), (d, v) => (d, v)), 3)
            .Last();
        Assert.Null(native.Correlation);
        var huge = new[] { double.MaxValue, -double.MaxValue, 0d };
        var packed = new double[3];
        Assert.Equal(
            TALib.Core.RetCode.Success,
            Functions.Correl<double>(huge, huge, System.Range.All, packed, out _, 3)
        );
        Assert.True(double.IsNaN(packed[0]));
        var skender = dates
            .Zip(huge, (d, v) => (d, v))
            .GetCorrelation(dates.Zip(huge, (d, v) => (d, v)), 3)
            .Last();
        Assert.True(double.IsPositiveInfinity(skender.VarianceA!.Value));
        var pair = PairStatisticsComparison.Create(true);
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                pair with
                {
                    Competitor = (_, _) =>
                        PairStatisticsComparison.FromSkender([skender, skender, skender]),
                },
                CompetitorData.FromBodies([1, 2, 3], [1, 2, 3]),
                3
            )
        );
    }

    [Fact]
    public async Task SelectedFieldsAndChainedCloseHaveIndependentReferences()
    {
        var data = PairStatisticsComparison.Fixture();
        foreach (var left in Enum.GetValues<CandlePriceField>())
        foreach (var right in Enum.GetValues<CandlePriceField>())
        {
            var indicator = new WindowPairStatistics(3, left, right);
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            double[] Field(CandlePriceField f) =>
                f switch
                {
                    CandlePriceField.Open => data.Opens,
                    CandlePriceField.High => data.Highs,
                    CandlePriceField.Low => data.Lows,
                    _ => data.Closes,
                };
            var expected = PairStatisticsComparison.Reference(Field(left), Field(right), 3, true);
            for (var slot = 0; slot < 5; slot++)
                Assert.Equal(
                    expected[slot].Select(v => v ?? 0),
                    run[indicator.Outputs[slot]].ToArray()
                );
        }
        var chained = new WindowPairStatistics(3);
        chained.Of(new FixedPeriodWma(2));
        using var pipeline = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(chained)
            .BuildAsync();
        var reference = PairStatisticsComparison.Reference(
            FixedWeightedComparison.Stage(data.Closes, 2, false),
            data.Opens,
            3,
            true
        );
        for (var slot = 0; slot < 5; slot++)
            Assert.Equal(
                reference[slot].Select(v => v ?? 0),
                pipeline[chained.Outputs[slot]].ToArray()
            );
    }

    [Fact]
    public void InvalidParametersAndEveryFieldPresenceMutationAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowCorrelation(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowPairStatistics(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WindowCorrelation(3, (CandlePriceField)99)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WindowPairStatistics(3, right: (CandlePriceField)99)
        );
        foreach (var pair in PairStatisticsComparison.Pairs)
        foreach (var name in pair.OutputNames!)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                if (presence)
                    result.Outputs[name].Present![^1] = false;
                else
                    result.Outputs[name].Values[^1] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    PairStatisticsComparison.Fixture(),
                    3
                )
            );
        }
    }
}
