using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using Xunit;
using CloudTuple = (
    decimal? ConversionLine,
    decimal? BaseLine,
    decimal? LeadingSpanA,
    decimal? LeadingSpanB,
    decimal? LaggingSpan
);

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ExtendedCloudComparisonTests
{
    [Fact]
    public void EveryExtendedRowMatchesIndependentDecimalAndGridReferences()
    {
        foreach (var c in new[] { (1, 1, 1), (2, 3, 5), (5, 2, 3), (9, 26, 52) })
        {
            var pair = ExtendedCloudComparison.Pair(c.Item1, c.Item2, c.Item3);
            foreach (var count in new[] { 0, 1, 3, 100 })
            {
                ComparisonVerifier.Check(pair, CompetitorData.Create(count), 20);
                foreach (var shape in ComparisonVerifier.Shapes)
                    ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, count), 20);
            }
        }
    }

    [Fact]
    public void TupleGenericCandleAndIndexRoutesPreserveExtendedCoordinates()
    {
        var d = CompetitorData.Create(30);
        var native = new Trady.Analysis.Indicator.IchimokuCloud(d.Candles, 2, 3, 5);
        var tuple = new Trady.Analysis.Indicator.IchimokuCloudByTuple(
            d.Candles.Select(v => (v.High, v.Low, v.Close)),
            2,
            3,
            5
        );
        var generic = new Trady.Analysis.Indicator.IchimokuCloud<Bar, CloudTuple>(
            d.IndicatorBars,
            v => ((decimal)v.High, (decimal)v.Low, (decimal)v.Close),
            2,
            3,
            5
        );
        var expected = native.Compute().Select(v => v.Tick).ToArray();
        Assert.Equal(35, expected.Length);
        Assert.Equal(expected, tuple.Compute());
        Assert.Equal(expected, generic.Compute());
        Assert.Equal(tuple.Compute().Skip(5).Take(16), tuple.Compute(startIndex: 5, endIndex: 15));
        IEnumerable<int> indexes = new[] { -2, 0, 9, 29, 32 };
        Assert.Equal(indexes.Select(i => tuple[i]), tuple.Compute(indexes));
        Assert.Equal(tuple[9], tuple.Compute(new[] { 9, 12 }));
        var adjacent = tuple.ComputeNeighbour(9);
        Assert.Equal(tuple[8], adjacent.Prev);
        Assert.Equal(tuple[9], adjacent.Current);
        Assert.Equal(tuple[10], adjacent.Next);
        var owned = IchimokuCloudSnapshot.Extended(d.IndicatorBars, 2, 3, 5).ToArray();
        Assert.Equal(Enumerable.Range(-2, 35).Select(i => (long)i), owned.Select(v => v.Index));
        Assert.Equal(d.Closes[0], owned[0].Value.Lagging);
        Assert.Null(owned[0].Value.Conversion);
        Assert.Null(owned[^1].Value.Base);
        Assert.NotNull(owned[^1].Value.LeadingA);
        Assert.NotNull(owned[^1].Value.LeadingB);
        Assert.Equal(
            owned.Skip(5).Take(16),
            IchimokuCloudSnapshot.Extended(d.IndicatorBars, 2, 3, 5, 3, 18)
        );
    }

    [Fact]
    public void LargeShiftsAreLazyAndCoordinatesAreOverflowSafe()
    {
        var d = CompetitorData.Create(3);
        var rows = IchimokuCloudSnapshot
            .Extended(d.IndicatorBars, 1, int.MaxValue, 2)
            .Take(3)
            .ToArray();
        Assert.Equal(1L - int.MaxValue, rows[0].Index);
        Assert.Equal(d.Closes, rows.Select(r => r.Value.Lagging!.Value));
        foreach (var index in new[] { long.MinValue, long.MaxValue })
        {
            var one = IchimokuCloudSnapshot
                .Extended(d.IndicatorBars, 1, 2, 3, index, index)
                .Single();
            Assert.Equal(index, one.Index);
            Assert.Equal(new IchimokuCloudValue(null, null, null, null, null), one.Value);
        }
        var captured = IchimokuCloudSnapshot.Extended(d.IndicatorBars, 1, 2, 3);
        var expected = captured.ToArray();
        Array.Clear(d.IndicatorBars);
        Assert.Equal(expected, captured.ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            IchimokuCloudSnapshot.Extended(d.IndicatorBars, basePeriod: 0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            IchimokuCloudSnapshot.Extended(d.IndicatorBars, startIndex: 4, endIndex: 3)
        );
    }

    [Fact]
    public void ExtremeMidpointsAndFutureProjectionDifferFromInputRangeSnapshot()
    {
        foreach (var value in new[] { double.MaxValue, -double.MaxValue, double.Epsilon })
        {
            var bars = Enumerable
                .Range(0, 6)
                .Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), value, value, value, value, 0))
                .ToArray();
            var rows = IchimokuCloudSnapshot.Extended(bars, 1, 2, 3).ToArray();
            Assert.Equal(value, rows[0].Value.Lagging);
            Assert.Equal(value, rows[^1].Value.LeadingA);
            Assert.Equal(value, rows[^1].Value.LeadingB);
        }
        var d = CompetitorData.Create(6);
        var extended = IchimokuCloudSnapshot.Extended(d.IndicatorBars, 1, 2, 3).ToArray();
        var ordinary = IchimokuCloudSnapshot.Calculate(d.IndicatorBars, 1, 2, 3);
        Assert.Equal(d.Closes[1], extended.Single(r => r.Index == 0).Value.Lagging);
        Assert.Equal(d.Closes[2], ordinary[0].Lagging);
    }

    [Fact]
    public void AllFiveOutputsPresenceAndRangeLengthMutationsAreDetected()
    {
        var d = CompetitorData.Create(80);
        var pair = ExtendedCloudComparison.Pair(2, 3, 5);
        foreach (var name in ExtendedCloudComparison.Names)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int p)
            {
                var r = native ? pair.Competitor(data, p) : pair.Ooples(data, p);
                if (presence)
                    r.Outputs[name].Present![30] = false;
                else
                    r.Outputs[name].Values[30] += 1;
                return r;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    d,
                    20
                )
            );
        }
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                pair with
                {
                    Library = ExtendedCloudComparison.Pair(2, 4, 5).Ooples,
                },
                d,
                20
            )
        );
    }
}
