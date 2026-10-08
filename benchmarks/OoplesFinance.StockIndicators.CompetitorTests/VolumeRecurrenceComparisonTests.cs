using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Trady.Analysis;
using Trady.Core.Infrastructure;
using Xunit;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class VolumeRecurrenceComparisonTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(20)]
    public async Task ForceIndexIndependentRationalLifecycle(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(SeededForceIndex),
                "force seed/EMA",
                () => new SeededForceIndex(period)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VolumeIndexIndependentRationalLifecycle(bool positive)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(VolumeConditionedIndex),
                "volume-selected returns",
                () => new VolumeConditionedIndex(positive)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public async Task ForceSeedCountsChangesAndPeriodOnePublishesRawChanges()
    {
        var values = new[]
        {
            (0d, 0d, 0d, 0d),
            (1d, 1d, 1d, 2d),
            (2d, 2d, 2d, 4d),
            (3d, 3d, 3d, 6d),
            (4d, 4d, 4d, 8d),
        };
        var result = await VolumePriceComparisonTests.Run(new SeededForceIndex(2), values);
        Assert.Equal(new[] { 0d, 0, 3, 5, 7 }, result[0]);
        Assert.Equal(new[] { 0d, 0, 1, 1, 1 }, result[1]);
        var identity = await VolumePriceComparisonTests.Run(new SeededForceIndex(1), values);
        Assert.Equal(new[] { 0d, 2, 4, 6, 8 }, identity[0]);
        Assert.Equal(new[] { 0d, 1, 1, 1, 1 }, identity[1]);
        var cancellation = VolumeRecurrenceComparison.ForceCancellationFixture();
        var pair = ComparisonPairs.Get("Skender.GetForceIndex");
        ComparisonVerifier.Check(pair, cancellation, 1);
        Assert.Equal(1, pair.Ooples(cancellation, 1).Outputs["Value"].Values[2]);
        Assert.Equal(0, pair.Competitor(cancellation, 1).Outputs["Value"].Values[2]);
    }

    [Fact]
    public async Task ForceUnpublishedProductsAndSeedSumsCanExceedBinary64()
    {
        var maximum = double.MaxValue;
        var cancellation = await VolumePriceComparisonTests.Run(
            new SeededForceIndex(2),
            (0, 0, 0, 0),
            (maximum, maximum, maximum, maximum),
            (0, 0, 0, maximum)
        );
        Assert.Equal(new[] { 0d, 0, 0 }, cancellation[0]);
        Assert.Equal(new[] { 0d, 0, 1 }, cancellation[1]);
        var difference = await VolumePriceComparisonTests.Run(
            new SeededForceIndex(1),
            (-maximum, -maximum, -maximum, 0),
            (maximum, maximum, maximum, .25)
        );
        Assert.Equal(maximum / 2, difference[0][1]);
        var normalized = await VolumePriceComparisonTests.Run(
            new SeededForceIndex(2),
            (0, 0, 0, 0),
            (maximum, maximum, maximum, 2),
            (maximum, maximum, maximum, 0),
            (maximum, maximum, maximum, 0)
        );
        Assert.Equal(maximum, normalized[0][2]);
        Assert.Equal(maximum / 3, normalized[0][3]);
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            VolumePriceComparisonTests.Run(
                new SeededForceIndex(1),
                (0, 0, 0, 0),
                (maximum, maximum, maximum, maximum)
            )
        );
        var lazy = await VolumePriceComparisonTests.Run(
            new SeededForceIndex(int.MaxValue),
            (0, 0, 0, 0),
            (maximum, maximum, maximum, maximum)
        );
        Assert.All(lazy, row => Assert.All(row, v => Assert.Equal(0, v)));
    }

    [Fact]
    public async Task VolumeIndexUsesSignedPriceRatiosAndUpdatesPreviousPriceOnHolds()
    {
        var values = new[]
        {
            (-2d, -2d, -2d, 1d),
            (-4d, -4d, -4d, 2d),
            (8d, 8d, 8d, 2d),
            (4d, 4d, 4d, 1d),
            (2d, 2d, 2d, 3d),
        };
        var positive = await VolumePriceComparisonTests.Run(
            new VolumeConditionedIndex(true),
            values
        );
        var negative = await VolumePriceComparisonTests.Run(new VolumeConditionedIndex(), values);
        Assert.Equal(new[] { 100d, 200, 200, 200, 100 }, positive[0]);
        Assert.Equal(new[] { 100d, 100, 100, 50, 50 }, negative[0]);
        Assert.All(positive[1], v => Assert.Equal(1, v));
        Assert.All(negative[1], v => Assert.Equal(1, v));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedZeroDenominatorIsAbsentUntilResetButHoldsNeverDivide(bool positive)
    {
        var volumes = positive ? new[] { 1d, 2, 3, 4 } : new[] { 4d, 3, 2, 1 };
        var values = Enumerable
            .Range(0, 4)
            .Select(i => ((double)i, (double)i, (double)i, volumes[i]))
            .ToArray();
        var result = await VolumePriceComparisonTests.Run(
            new VolumeConditionedIndex(positive),
            values
        );
        Assert.Equal(new[] { 100d, 0, 0, 0 }, result[0]);
        Assert.Equal(new[] { 1d, 0, 0, 0 }, result[1]);
        var data = CompetitorData.FromOhlcv(
            [0, 1, 2, 3],
            [0, 1, 2, 3],
            [0, 1, 2, 3],
            [0, 1, 2, 3],
            volumes
        );
        Assert.Throws<DivideByZeroException>(() =>
        {
            if (positive)
                new T.PositiveVolumeIndex(data.Candles).Compute();
            else
                new T.NegativeVolumeIndex(data.Candles).Compute();
        });
        var held = await VolumePriceComparisonTests.Run(
            new VolumeConditionedIndex(positive),
            (0, 0, 0, 2),
            (2, 2, 2, 2),
            (4, 4, 4, positive ? 3 : 1)
        );
        Assert.Equal(new[] { 100d, 100, 200 }, held[0]);
        Assert.All(held[1], v => Assert.Equal(1, v));
    }

    [Fact]
    public async Task VolumeIndexNormalizesLargeProductsAndPreservesSubnormalPrices()
    {
        var maximum = double.MaxValue;
        var huge = await VolumePriceComparisonTests.Run(
            new VolumeConditionedIndex(true),
            (maximum, maximum, maximum, 1),
            (maximum, maximum, maximum, 2)
        );
        Assert.Equal(new[] { 100d, 100 }, huge[0]);
        var tiny = await VolumePriceComparisonTests.Run(
            new VolumeConditionedIndex(true),
            (double.Epsilon, double.Epsilon, double.Epsilon, 1),
            (2 * double.Epsilon, 2 * double.Epsilon, 2 * double.Epsilon, 2)
        );
        Assert.Equal(new[] { 100d, 200 }, tiny[0]);
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            VolumePriceComparisonTests.Run(
                new VolumeConditionedIndex(true),
                (double.Epsilon, double.Epsilon, double.Epsilon, 1),
                (maximum, maximum, maximum, 2)
            )
        );
    }

    [Fact]
    public async Task NativeDecimalPriceDifferenceCanOverflowBeforeAFiniteIndex()
    {
        foreach (var positive in new[] { false, true })
        {
            var volumes = positive ? new[] { 1d, 2 } : new[] { 2d, 1 };
            var data = CompetitorData.FromOhlcv(
                [4e28, -4e28],
                [4e28, -4e28],
                [4e28, -4e28],
                [4e28, -4e28],
                volumes
            );
            Assert.Throws<OverflowException>(() =>
            {
                if (positive)
                    new T.PositiveVolumeIndex(data.Candles).Compute();
                else
                    new T.NegativeVolumeIndex(data.Candles).Compute();
            });
            var owned = await VolumePriceComparisonTests.Run(
                new VolumeConditionedIndex(positive),
                (4e28, 4e28, 4e28, volumes[0]),
                (-4e28, -4e28, -4e28, volumes[1])
            );
            Assert.Equal(new[] { 100d, -100 }, owned[0]);
        }
    }

    [Fact]
    public void QuoteVolumeConversionCanTurnStrictChangesIntoTies()
    {
        var data = VolumeRecurrenceComparison.CollapsedVolumeFixture();
        foreach (var id in VolumeRecurrenceComparison.Ids.Skip(1))
        {
            var pair = ComparisonPairs.Get(id);
            ComparisonVerifier.Check(pair, data, 20);
            Assert.All(
                pair.Competitor(data, 20).Outputs["Value"].Values,
                v => Assert.Equal(100, v)
            );
            Assert.Contains(pair.Ooples(data, 20).Outputs["Value"].Values, v => v > 100);
        }
    }

    [Fact]
    public void IndependentFullTrajectoriesAndEveryNativeVolumeIndexRouteAgree()
    {
        foreach (var pair in VolumeRecurrenceComparison.Pairs)
        foreach (var period in new[] { 1, 2, 3, 20 })
            ComparisonVerifier.Check(pair, VolumeRecurrenceComparison.Fixture(), period);
        var data = VolumeRecurrenceComparison.Fixture();
        var tuples = data.Candles.Select(c => (c.Close, c.Volume)).ToArray();
        var positive = new T.PositiveVolumeIndex(data.Candles)
            .Compute()
            .Select(r => r.Tick)
            .ToArray();
        Assert.Equal(positive, new T.PositiveVolumeIndexByTuple(tuples).Compute().ToArray());
        Assert.Equal(
            positive,
            new T.PositiveVolumeIndex<IOhlcv, AnalyzableTick<decimal?>>(
                data.Candles,
                c => (c.Close, c.Volume)
            )
                .Compute()
                .Select(r => r.Tick)
                .ToArray()
        );
        var negative = new T.NegativeVolumeIndex(data.Candles)
            .Compute()
            .Select(r => r.Tick)
            .ToArray();
        Assert.Equal(negative, new T.NegativeVolumeIndexByTuple(tuples).Compute().ToArray());
        Assert.Equal(
            negative,
            new T.NegativeVolumeIndex<IOhlcv, AnalyzableTick<decimal?>>(
                data.Candles,
                c => (c.Close, c.Volume)
            )
                .Compute()
                .Select(r => r.Tick)
                .ToArray()
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededForceIndex(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetForceIndex(0).ToArray());
        Assert.Throws<OverflowException>(() => data.Quotes.GetForceIndex(int.MaxValue).ToArray());
    }

    [Fact]
    public void CorruptValuesAndPresenceFailBothArmsForEveryRecurrence()
    {
        foreach (var pair in VolumeRecurrenceComparison.Pairs)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                var output = result.Outputs["Value"];
                var i = data.Count - 1;
                if (presence)
                    output.Present![i] = !output.Present[i];
                else
                    output.Values[i] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    VolumeRecurrenceComparison.Fixture(),
                    3
                )
            );
        }
    }
}
