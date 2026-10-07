using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class EmaDifferenceSignalComparisonTests
{
    private static Bar[] BarsOf(params double[] values) =>
        values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, v)).ToArray();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task IndependentRationalContractsCoverLifecycleSelectionsAndWidePeriods(
        bool first,
        bool percentage
    )
    {
        foreach (
            var config in new[]
            {
                (1, 3, 1),
                (3, 2, 4),
                (int.MaxValue, 3, 2),
                (2, int.MaxValue, 2),
                (2, 3, int.MaxValue),
            }
        )
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(EmaDifferenceSignal),
                    "EMA difference",
                    () =>
                        new EmaDifferenceSignal(
                            config.Item1,
                            config.Item2,
                            config.Item3,
                            first,
                            percentage,
                            percentage
                        )
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
        foreach (
            var selection in new[]
            {
                EmaDifferenceSelection.Histogram,
                EmaDifferenceSelection.Signal,
                EmaDifferenceSelection.FastAverage,
                EmaDifferenceSelection.SlowAverage,
                EmaDifferenceSelection.Oscillator,
            }
        )
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(EmaDifferenceSignal),
                    "selected EMA difference",
                    () => new EmaDifferenceSignal(2, 3, 2, first, percentage, percentage, selection)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public void CompleteValuesAndMasksHaveIndependentGridAndNativeReferences()
    {
        for (var variant = 0; variant < 4; variant++)
            foreach (
                var config in variant < 2
                    ? new[] { (1, 2, 1), (2, 3, 2), (3, 7, 4), (12, 26, 9) }
                    : new[] { (1, 1, 1), (3, 2, 2), (3, 3, 4), (2, 7, 3), (12, 26, 9) }
            )
            {
                var pair = EmaDifferenceSignalComparison.Pair(
                    variant,
                    config.Item1,
                    config.Item2,
                    config.Item3
                );
                ComparisonVerifier.Check(pair, CompetitorData.Create(100), 20);
                foreach (var shape in ComparisonVerifier.Shapes)
                    ComparisonVerifier.Check(
                        pair,
                        EmaDifferenceSignalComparison.Fixture(variant, shape, 80),
                        20
                    );
            }
    }

    [Fact]
    public void KnownStartupAndMissingPercentageSignalZeroAreExplicit()
    {
        var bars = BarsOf(1, 2, 3, 4, 5, 6, 7, 8);
        var rows = EmaDifferenceSignalComparison.Owned(bars, 0, 2, 3, 2).Outputs;
        Assert.Equal(
            new[] { false, true, true, true, true, true, true, true },
            rows["FastEma"].Present
        );
        Assert.Equal(
            new[] { false, false, true, true, true, true, true, true },
            rows["Macd"].Present
        );
        Assert.Equal(
            new[] { false, false, false, true, true, true, true, true },
            rows["Signal"].Present
        );
        Assert.All(rows["Macd"].Values.Skip(2), v => Assert.Equal(.5, v));
        Assert.All(rows["Histogram"].Values.Skip(3), v => Assert.Equal(0, v));
        var first = EmaDifferenceSignalComparison.Owned(bars, 2, 1, 3, 1).Outputs;
        Assert.All(first.Values, r => Assert.All(r.Present!, Assert.True));
        Assert.Equal(.5, first["Macd"].Values[1]);
        Assert.All(first["Histogram"].Values, v => Assert.Equal(0, v));
        var missing = EmaDifferenceSignalComparison
            .Owned(BarsOf(0, 0, 0, 2, 2), 1, 1, 2, 2)
            .Outputs;
        Assert.Equal(new[] { false, false, false, true, true }, missing["Pvo"].Present);
        Assert.True(missing["Signal"].Present![2]);
        Assert.Equal(0, missing["Signal"].Values[2]);
        Assert.False(missing["Histogram"].Present![2]);
        Assert.True(missing["Histogram"].Present![3]);
    }

    [Fact]
    public void HiddenUnrepresentableDifferenceAndSignalCancelInSelectedHistogram()
    {
        var bars = BarsOf(-double.MaxValue, double.MaxValue, double.MaxValue);
        var histogram = EmaDifferenceSignalComparison.Owned(bars, 3, 1, 10, 1).Outputs["Histogram"];
        Assert.All(histogram.Values, v => Assert.Equal(0, v));
        Assert.Throws<IndicatorOutputException>(() =>
            EmaDifferenceSignalComparison.Owned(bars, 2, 1, 10, 1)
        );
        var volumeBars = BarsOf(-double.MaxValue, -double.MaxValue, double.MaxValue);
        var percentage = EmaDifferenceSignalComparison.Owned(volumeBars, 1, 1, 3, 1);
        ComparisonVerifier.Compare(
            EmaDifferenceSignalComparison.Series(
                EmaDifferenceSignalComparison.GridReference(
                    volumeBars.Select(v => v.Volume).ToArray(),
                    1,
                    3,
                    1,
                    false,
                    true
                ),
                1
            ),
            percentage,
            "extended percentage difference",
            IndicatorErrorBudget.Exact
        );
        Assert.All(
            percentage
                .Outputs["Histogram"]
                .Values.Where((_, i) => percentage.Outputs["Histogram"].Present![i]),
            v => Assert.Equal(0, v)
        );
        foreach (var variant in new[] { 0, 1, 2, 3 })
        {
            var constant = EmaDifferenceSignalComparison.Owned(
                BarsOf(double.MaxValue, double.MaxValue, double.MaxValue),
                variant,
                1,
                2,
                1
            );
            Assert.All(
                constant.Outputs.Values,
                r =>
                    Assert.All(
                        r.Values.Where((_, i) => r.Present![i]),
                        v => Assert.True(double.IsFinite(v))
                    )
            );
        }
    }

    [Fact]
    public void SkenderDefaultsTupleReusableSortingAndVolumeSourceAreCovered()
    {
        var data = CompetitorData.Create(80);
        var q = data.Quotes;
        var t = q.Select(v => (v.Date, (double)v.Close)).ToArray();
        var expected = q.GetMacd(2, 4, 3)
            .Select(v => (v.Macd, v.Signal, v.Histogram, v.FastEma, v.SlowEma))
            .ToArray();
        foreach (
            var rows in new[]
            {
                q.AsEnumerable().Reverse().GetMacd(2, 4, 3),
                t.Reverse().GetMacd(2, 4, 3),
                q.GetSma(1).GetMacd(2, 4, 3),
            }
        )
            Assert.Equal(
                expected,
                rows.Select(v => (v.Macd, v.Signal, v.Histogram, v.FastEma, v.SlowEma))
            );
        Assert.Equal(
            q.GetMacd().Select(v => (v.Macd, v.Signal, v.Histogram)),
            q.GetMacd(12, 26, 9).Select(v => (v.Macd, v.Signal, v.Histogram))
        );
        Assert.Equal(
            q.GetPvo().Select(v => (v.Pvo, v.Signal, v.Histogram)),
            q.GetPvo(12, 26, 9).Select(v => (v.Pvo, v.Signal, v.Histogram))
        );
        Assert.Equal(
            q.GetPvo(2, 4, 3).Select(v => (v.Pvo, v.Signal, v.Histogram)),
            q.AsEnumerable().Reverse().GetPvo(2, 4, 3).Select(v => (v.Pvo, v.Signal, v.Histogram))
        );
        var changed = q.Select(v => new Quote
            {
                Date = v.Date,
                Close = 0,
                Open = 0,
                High = 0,
                Low = 0,
                Volume = v.Volume,
            })
            .ToArray();
        Assert.Equal(
            q.GetPvo(2, 4, 3).Select(v => (v.Pvo, v.Signal, v.Histogram)),
            changed.GetPvo(2, 4, 3).Select(v => (v.Pvo, v.Signal, v.Histogram))
        );
        var sma = q.GetSma(3).ToArray();
        var native = EmaDifferenceSignalComparison.NativeReference(
            sma.Skip(2).Select(v => v.Sma!.Value).ToArray(),
            2,
            4,
            3,
            false
        );
        var chain = sma.GetMacd(2, 4, 3).Skip(2).ToArray();
        Assert.Equal(native[0], chain.Select(v => v.Macd));
        Assert.Equal(native[1], chain.Select(v => v.Signal));
        Assert.Equal(native[4], chain.Select(v => v.SlowEma));
        foreach (var sig in new[] { 0, -1 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => q.GetMacd(2, 4, sig).ToArray());
            Assert.Throws<ArgumentOutOfRangeException>(() => q.GetPvo(2, 4, sig).ToArray());
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => q.GetMacd(2, 2).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => q.GetPvo(2, 1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => q.GetMacd(0, 2).ToArray());
        Assert.Throws<OverflowException>(() => q.GetMacd(1, int.MaxValue).ToArray());
        Assert.Throws<OverflowException>(() => q.GetPvo(1, 2, int.MaxValue).ToArray());
    }

    [Fact]
    public void TradyTupleMappedRangeIndexesAndDecimalBoundaries()
    {
        decimal[] prices = [1, 5, 2, -4, 0, 7, 7, 3, 9];
        foreach (
            var c in new[]
            {
                (1, 3, 1),
                (3, 1, 3),
                (3, 3, 2),
                (2, 5, 3),
                (0, 2, 0),
                (int.MaxValue, 2, 3),
            }
        )
        {
            var reference = EmaDifferenceSignalComparison.DecimalReference(
                prices,
                c.Item1,
                c.Item2,
                c.Item3
            );
            var expected = prices
                .Select((_, i) => (reference[0][i], reference[1][i], reference[2][i]))
                .ToArray();
            var tuple = new T.MovingAverageConvergenceDivergenceByTuple(
                prices,
                c.Item1,
                c.Item2,
                c.Item3
            );
            var histogram = new T.MovingAverageConvergenceDivergenceHistogramByTuple(
                prices,
                c.Item1,
                c.Item2,
                c.Item3
            );
            Assert.Equal(expected, tuple.Compute());
            Assert.Equal(expected, tuple.Compute());
            Assert.Equal(reference[2], histogram.Compute());
            Assert.Equal(expected.Skip(2).Take(4), tuple.Compute(startIndex: 2, endIndex: 5));
            Assert.Equal(
                reference[2].Skip(2).Take(4),
                histogram.Compute(startIndex: 2, endIndex: 5)
            );
            int[] indexes = [8, 2, 2, 0, 7];
            Assert.Equal(
                indexes.Select(i => expected[i]),
                tuple.Compute((IEnumerable<int>)indexes)
            );
            Assert.Equal(
                indexes.Select(i => reference[2][i]),
                histogram.Compute((IEnumerable<int>)indexes)
            );
            foreach (var i in indexes)
            {
                Assert.Equal(expected[i], tuple[i]);
                Assert.Equal(reference[2][i], histogram[i]);
            }
            var mapped = prices.Select((v, i) => (Price: v, Index: i)).ToArray();
            Assert.Equal(
                expected,
                new T.MovingAverageConvergenceDivergence<
                    (decimal Price, int Index),
                    (decimal?, decimal?, decimal?)
                >(mapped, v => v.Price, c.Item1, c.Item2, c.Item3).Compute()
            );
            Assert.Equal(
                reference[2],
                new T.MovingAverageConvergenceDivergenceHistogram<
                    (decimal Price, int Index),
                    decimal?
                >(mapped, v => v.Price, c.Item1, c.Item2, c.Item3).Compute()
            );
        }
        Assert.Throws<DivideByZeroException>(() =>
            new T.MovingAverageConvergenceDivergenceByTuple(prices, 1, 2, -1).Compute().ToArray()
        );
        Assert.Throws<DivideByZeroException>(() =>
            new T.MovingAverageConvergenceDivergenceHistogramByTuple(prices, -1, 2, 2)
                .Compute()
                .ToArray()
        );
        Assert.Single(
            new T.MovingAverageConvergenceDivergenceByTuple(new[] { 1m }, -1, -1, -1).Compute()
        );
        Assert.Throws<OverflowException>(() =>
            new T.MovingAverageConvergenceDivergenceByTuple(
                new[] { -decimal.MaxValue, decimal.MaxValue },
                1,
                3,
                1
            )
                .Compute()
                .ToArray()
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new EmaDifferenceSignal(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EmaDifferenceSignal(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EmaDifferenceSignal(1, 2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EmaDifferenceSignal(selection: (EmaDifferenceSelection)0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EmaDifferenceSignal(selection: (EmaDifferenceSelection)32)
        );
    }

    [Fact]
    public async Task ChainingEveryPublicOutputPresenceAndParameterMutation()
    {
        var data = CompetitorData.Create(60);
        var closes = FixedWeightedComparison.Stage(data.Closes, 3, false);
        for (var variant = 0; variant < 4; variant++)
        {
            var selection = EmaDifferenceSignalComparison.Selection(variant);
            var indicator = new EmaDifferenceSignal(
                2,
                4,
                3,
                variant >= 2,
                variant == 1,
                variant == 1,
                selection
            );
            indicator.Of(new FixedPeriodWma(3));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            var expected = EmaDifferenceSignalComparison.GridReference(
                variant == 1 ? data.Volumes : closes,
                2,
                4,
                3,
                variant >= 2,
                variant == 1,
                selection
            );
            for (var j = 0; j < 5; j++)
            {
                Assert.Equal(expected[j].Select(v => v ?? 0), run[indicator.Outputs[j]].ToArray());
                Assert.Equal(
                    expected[j].Select(v => v.HasValue ? 1d : 0),
                    run[indicator.Outputs[j + 5]].ToArray()
                );
            }
            var pair = EmaDifferenceSignalComparison.Pair(variant, 2, 4, 3);
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
                        20
                    )
                );
            }
            foreach (var c in new[] { (3, 4, 3), (2, 5, 3), (2, 4, 4) })
                Assert.Throws<InvalidOperationException>(() =>
                    ComparisonVerifier.Check(
                        pair with
                        {
                            Library = EmaDifferenceSignalComparison
                                .Pair(variant, c.Item1, c.Item2, c.Item3)
                                .Ooples,
                        },
                        data,
                        20
                    )
                );
        }
    }
}
