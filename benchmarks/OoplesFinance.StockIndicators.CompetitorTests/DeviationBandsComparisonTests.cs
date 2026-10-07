using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class DeviationBandsComparisonTests
{
    [Theory]
    [InlineData(3, 2, false)]
    [InlineData(1, -2, true)]
    [InlineData(3, 0, true)]
    public async Task RationalContractCoversEveryOutputAndLifecycle(
        int period,
        double factor,
        bool percent
    )
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(WindowDeviationBands),
                "full deviation bands",
                () => new WindowDeviationBands(period, factor, percent)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void WholeWindowsSignedFactorsAndFlatMasksMatchBothIndependentContracts()
    {
        foreach (var id in DeviationBandsComparison.Ids)
        foreach (
            var period in (
                id == DeviationBandsComparison.Ids[0] ? new[] { 2, 3, 20 } : new[] { 1, 3, 20 }
            )
        )
        foreach (
            var factor in (
                id == DeviationBandsComparison.Ids[0] ? new[] { .1, 2d } : new[] { -2d, 0d, 2d }
            )
        )
        {
            var pair = DeviationBandsComparison.Pair(id, factor);
            ComparisonVerifier.Check(pair, CompetitorData.Create(65), period);
            foreach (var shape in ComparisonVerifier.Shapes)
                ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 35), period);
            ComparisonVerifier.Check(
                pair,
                SkenderDeviationComparison.FlatRoundingFixture(),
                period
            );
            ComparisonVerifier.Check(
                pair,
                SkenderDeviationComparison.CollapsedQuoteFixture(),
                period
            );
        }
        var d = CompetitorData.FromCloses([1, 3, 3, 3, -3, 3]);
        var actual = DeviationBandsComparison
            .Owned(DeviationBandsComparison.Ids[0], d.IndicatorBars, 2, 2)
            .Outputs;
        Assert.Equal(new[] { false, true, true, true, true, true }, actual["Sma"].Present);
        Assert.Equal(new[] { false, true, false, false, true, true }, actual["PercentB"].Present);
        Assert.Equal(new[] { false, true, true, true, false, false }, actual["Width"].Present);
        Assert.Equal(.75, actual["PercentB"].Values[1]);
        Assert.Equal(2, actual["Width"].Values[1]);
    }

    [Fact]
    public void SkenderQuoteTupleReusableAndSortRoutesPreserveAllSixOutputs()
    {
        var d = CompetitorData.Create(65);
        const int p = 3;
        var native = DeviationBandsComparison.SkenderSeries(
            d.Quotes.GetBollingerBands(p).ToArray()
        );
        var tuple = d.Quotes.Select(q => (q.Date, (double)q.Close));
        ComparisonVerifier.Compare(
            native,
            DeviationBandsComparison.SkenderSeries(tuple.GetBollingerBands(p).ToArray()),
            "tuple",
            IndicatorErrorBudget.Exact
        );
        ComparisonVerifier.Compare(
            native,
            DeviationBandsComparison.SkenderSeries(
                d.Quotes.AsEnumerable().Reverse().GetBollingerBands(p).ToArray()
            ),
            "sort",
            IndicatorErrorBudget.Exact
        );
        var reusable = d.Quotes.GetSma(2).ToArray();
        var values = reusable.Where(v => v.Sma.HasValue).Select(v => v.Sma!.Value).ToArray();
        var reference = DeviationBandsComparison
            .NativeReference(values, p, 2, false, false)
            .Select(v =>
                new double?[] { null }
                    .Concat(v)
                    .ToArray()
            )
            .ToArray();
        ComparisonVerifier.Compare(
            DeviationBandsComparison.Series(DeviationBandsComparison.Ids[0], reference),
            DeviationBandsComparison.SkenderSeries(reusable.GetBollingerBands(p).ToArray()),
            "reusable",
            IndicatorErrorBudget.Exact
        );
    }

    [Fact]
    public void TradyObjectTupleAndGenericRoutesIncludeWidthPercent()
    {
        var d = CompetitorData.Create(35);
        var closes = d.Candles.Select(v => v.Close).ToArray();
        foreach (var factor in new[] { -2m, 0m, 2m })
        {
            var tuples = new Trady.Analysis.Indicator.BollingerBandsByTuple(closes, 3, factor);
            var generic = new Trady.Analysis.Indicator.BollingerBands<
                decimal,
                (decimal? LowerBand, decimal? MiddleBand, decimal? UpperBand)
            >(closes, v => v, 3, factor);
            var expected = DeviationBandsComparison
                .Pair(DeviationBandsComparison.Ids[1], (double)factor)
                .Competitor(d, 3);
            ComparisonVerifier.Compare(
                expected,
                DeviationBandsComparison.TradySeries(tuples.Compute().ToArray()),
                "tuple bands",
                IndicatorErrorBudget.Exact
            );
            ComparisonVerifier.Compare(
                expected,
                DeviationBandsComparison.TradySeries(generic.Compute().ToArray()),
                "generic bands",
                IndicatorErrorBudget.Exact
            );
            var width = new Trady.Analysis.Indicator.BollingerBandWidthByTuple(closes, 3, factor);
            var genericWidth = new Trady.Analysis.Indicator.BollingerBandWidth<decimal, decimal?>(
                closes,
                v => v,
                3,
                factor
            );
            var expectedWidth = DeviationBandsComparison
                .Pair(DeviationBandsComparison.Ids[2], (double)factor)
                .Competitor(d, 3);
            ComparisonVerifier.Compare(
                expectedWidth,
                DeviationBandsComparison.WidthSeries(width.Compute().ToArray()),
                "tuple width",
                IndicatorErrorBudget.Exact
            );
            ComparisonVerifier.Compare(
                expectedWidth,
                DeviationBandsComparison.WidthSeries(genericWidth.Compute().ToArray()),
                "generic width",
                IndicatorErrorBudget.Exact
            );
        }
    }

    [Fact]
    public void NarrowBandsRetainPositionBeforePublicationRounding()
    {
        var values = new[] { 1d, Math.BitIncrement(1d), 1d, Math.BitIncrement(1d) };
        var bars = BarsOf(values);
        var id = DeviationBandsComparison.Ids[0];
        var actual = DeviationBandsComparison.Owned(id, bars, 2, .1);
        ComparisonVerifier.Compare(
            DeviationBandsComparison.Series(
                id,
                DeviationBandsComparison.GridReference(values, 2, .1, false)
            ),
            actual,
            "narrow bands",
            IndicatorErrorBudget.Exact
        );
        Assert.Equal(actual.Outputs["UpperBand"].Values[1], actual.Outputs["LowerBand"].Values[1]);
        Assert.True(actual.Outputs["PercentB"].Present![1]);
        var tuple = values
            .Select((v, i) => (DateTime.UnixEpoch.AddDays(i), v))
            .GetBollingerBands(2, .1)
            .ToArray();
        Assert.Null(tuple[1].PercentB);
    }

    [Fact]
    public void ExtremeRangesSubnormalsAndHugePeriodsAreBounded()
    {
        var id = DeviationBandsComparison.Ids[0];
        foreach (
            var values in new[]
            {
                new[] { -double.MaxValue, double.MaxValue, -double.MaxValue },
                new[] { 0d, double.Epsilon, 2 * double.Epsilon, 3 * double.Epsilon },
                new[] { 1e15, 1e15 + 1, 1e15 + 2 },
            }
        )
            ComparisonVerifier.Compare(
                DeviationBandsComparison.Series(
                    id,
                    DeviationBandsComparison.GridReference(values, 2, .25, false)
                ),
                DeviationBandsComparison.Owned(id, BarsOf(values), 2, .25),
                "extreme bands",
                IndicatorErrorBudget.Exact
            );
        var large = DeviationBandsComparison.Owned(id, BarsOf([1, 2, 3]), int.MaxValue, 2);
        Assert.All(large.Outputs.Values, v => Assert.All(v.Present!, p => Assert.False(p)));
        var prices = Enumerable.Repeat(double.MaxValue, 4).ToArray();
        var native = prices
            .Select((v, i) => (DateTime.UnixEpoch.AddDays(i), v))
            .GetBollingerBands(2)
            .ToArray();
        var reference = DeviationBandsComparison.NativeReference(prices, 2, 2, false, false);
        Assert.Equal(reference[0], native.Select(v => v.Sma));
        Assert.Equal(reference[1], native.Select(v => v.UpperBand));
        Assert.Contains(native, v => v.Sma.HasValue && double.IsInfinity(v.Sma.Value));
        Assert.Equal(reference[2], native.Select(v => v.LowerBand));
        Assert.Equal(reference[3], native.Select(v => v.PercentB));
        Assert.Equal(reference[4], native.Select(v => v.ZScore));
        Assert.Equal(reference[5], native.Select(v => v.Width));
        var decimalOverflow = CompetitorData.FromCloses([1e20, -1e20, 1e20]);
        foreach (var tradyId in DeviationBandsComparison.Ids.Skip(1))
        {
            Assert.Throws<OverflowException>(() =>
                DeviationBandsComparison.Pair(tradyId).Competitor(decimalOverflow, 2)
            );
            ComparisonVerifier.Compare(
                DeviationBandsComparison.Series(
                    tradyId,
                    DeviationBandsComparison.GridReference(decimalOverflow.Closes, 2, 2, true)
                ),
                DeviationBandsComparison.Owned(tradyId, decimalOverflow.IndicatorBars, 2, 2),
                "decimal overflow counterpart",
                IndicatorErrorBudget.Exact
            );
        }
    }

    [Fact]
    public async Task ChainingAndAllValuesMasksAndFactorsHaveMutationChecks()
    {
        var d = CompetitorData.Create(40);
        var indicator = new WindowDeviationBands(3);
        indicator.Of(new FixedPeriodWma(3));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(d.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        var expected = DeviationBandsComparison.GridReference(
            FixedWeightedComparison.Stage(d.Closes, 3, false),
            3,
            2,
            false
        );
        for (var slot = 0; slot < 6; slot++)
        {
            Assert.Equal(
                expected[slot].Select(v => v ?? 0),
                run[indicator.Outputs[slot]].ToArray()
            );
            Assert.Equal(
                expected[slot].Select(v => v.HasValue ? 1d : 0),
                run[indicator.Outputs[slot + 6]].ToArray()
            );
        }
        foreach (var id in DeviationBandsComparison.Ids)
        {
            var pair = DeviationBandsComparison.Pair(id);
            foreach (var name in DeviationBandsComparison.Names(id))
            foreach (var native in new[] { false, true })
            foreach (var mask in new[] { false, true })
            {
                ComparisonSeries Bad(CompetitorData data, int p)
                {
                    var result = native ? pair.Competitor(data, p) : pair.Ooples(data, p);
                    if (mask)
                        result.Outputs[name].Present![^1] = false;
                    else
                        result.Outputs[name].Values[^1] += 1;
                    return result;
                }
                Assert.Throws<InvalidOperationException>(() =>
                    ComparisonVerifier.Check(
                        native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                        d,
                        3
                    )
                );
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    pair with
                    {
                        Library = DeviationBandsComparison.Pair(id, 3).Library,
                    },
                    d,
                    3
                )
            );
        }
    }

    [Fact]
    public void ParameterBoundariesRemainExplicit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowDeviationBands(0));
        foreach (
            var factor in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }
        )
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WindowDeviationBands(factor: factor)
            );
        var d = CompetitorData.Create(4);
        Assert.Throws<ArgumentOutOfRangeException>(() => d.Quotes.GetBollingerBands(1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            d.Quotes.GetBollingerBands(2, 0).ToArray()
        );
        var nan = d.Quotes.GetBollingerBands(2, double.NaN).ToArray();
        Assert.True(double.IsNaN(nan[^1].UpperBand!.Value));
        var nanReference = DeviationBandsComparison.NativeReference(d.Closes, 2, double.NaN, false);
        Assert.Equal(nanReference[1], nan.Select(v => v.UpperBand));
        Assert.Equal(nanReference[2], nan.Select(v => v.LowerBand));
        Assert.Equal(nanReference[3], nan.Select(v => v.PercentB));
        Assert.Equal(nanReference[5], nan.Select(v => v.Width));
        ComparisonVerifier.Check(
            DeviationBandsComparison.Pair(DeviationBandsComparison.Ids[1], 0),
            d,
            1
        );
    }

    private static Bar[] BarsOf(double[] values) =>
        values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();
}
