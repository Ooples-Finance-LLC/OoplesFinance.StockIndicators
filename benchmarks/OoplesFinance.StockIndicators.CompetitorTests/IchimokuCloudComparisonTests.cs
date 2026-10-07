using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class IchimokuCloudComparisonTests
{
    [Fact]
    public void IndependentWindowScansCoverAllOutputsOffsetsAndSigns()
    {
        foreach (
            var c in new[]
            {
                (1, 1, 2, 0, 0),
                (2, 3, 5, 3, 2),
                (3, 2, 4, 20, 0),
                (9, 26, 52, 26, 26),
                (2, 3, 5, 0, 20),
            }
        )
        {
            var pair = IchimokuCloudComparison.Pair(c.Item1, c.Item2, c.Item3, c.Item4, c.Item5);
            ComparisonVerifier.Check(pair, CompetitorData.Create(120), 20);
            foreach (var shape in ComparisonVerifier.Shapes)
                ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 100), 20);
        }
    }

    [Fact]
    public void NativeOverloadsSortingAndMappingsAgree()
    {
        var d = CompetitorData.Create(100);
        var expected = d.Quotes.GetIchimoku().ToArray();
        Assert.Equal(
            expected.Select(r => r.SenkouSpanA),
            d.Quotes.GetIchimoku(9, 26, 52, 26).Select(r => r.SenkouSpanA)
        );
        var custom = d.Quotes.GetIchimoku(2, 3, 5, 4).ToArray();
        var full = d.Quotes.GetIchimoku(2, 3, 5, 4, 4).ToArray();
        var reversed = d.Quotes.AsEnumerable().Reverse().GetIchimoku(2, 3, 5, 4, 4).ToArray();
        Assert.Equal(
            IchimokuCloudComparison.NativeSeries(full).Outputs.SelectMany(p => p.Value.Values),
            IchimokuCloudComparison.NativeSeries(custom).Outputs.SelectMany(p => p.Value.Values)
        );
        Assert.Equal(
            IchimokuCloudComparison.NativeSeries(full).Outputs.SelectMany(p => p.Value.Values),
            IchimokuCloudComparison.NativeSeries(reversed).Outputs.SelectMany(p => p.Value.Values)
        );
        var mapped = d
            .Quotes.Select(q => new TestQuote
            {
                Date = q.Date,
                High = q.High,
                Low = q.Low,
                Close = q.Close,
            })
            .GetIchimoku(2, 3, 5, 4, 4);
        Assert.Equal(
            IchimokuCloudComparison.NativeSeries(full).Outputs.SelectMany(p => p.Value.Values),
            IchimokuCloudComparison.NativeSeries(mapped).Outputs.SelectMany(p => p.Value.Values)
        );
    }

    private sealed class TestQuote : IQuote
    {
        public DateTime Date { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
    }

    [Fact]
    public void ShiftedCoordinatesAndExtraLeadingStartupArePinned()
    {
        var bars = CompetitorData
            .FromCloses(Enumerable.Range(1, 16).Select(i => (double)i).ToArray())
            .IndicatorBars;
        var r = IchimokuCloudSnapshot.Calculate(bars, 1, 1, 2, 4, 2);
        Assert.Null(r[6].LeadingA);
        Assert.Equal(r[3].Conversion, r[7].LeadingA);
        Assert.Equal(bars[2].Close, r[0].Lagging);
        Assert.Null(r[^2].Lagging);
        var prefix = IchimokuCloudSnapshot.Calculate(bars.Take(10).ToArray(), 1, 1, 2, 4, 2);
        Assert.Null(prefix[^1].Lagging);
        Assert.NotNull(r[9].Lagging);
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(prefix[i].Conversion, r[i].Conversion);
            Assert.Equal(prefix[i].Base, r[i].Base);
            Assert.Equal(prefix[i].LeadingA, r[i].LeadingA);
            Assert.Equal(prefix[i].LeadingB, r[i].LeadingB);
        }
        var a = IchimokuCloudSnapshot.Calculate(bars, 1, 1, 2, 0, 0);
        Assert.Equal(a[0].Conversion, a[0].LeadingA);
        Assert.Equal(bars[0].Close, a[0].Lagging);
    }

    [Fact]
    public void ExactMidpointsPreserveSubnormalAndExtremeValues()
    {
        foreach (
            var value in new[]
            {
                double.MaxValue,
                -double.MaxValue,
                double.Epsilon,
                -double.Epsilon,
            }
        )
        {
            var bars = Enumerable
                .Range(0, 5)
                .Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), value, value, value, value, 0))
                .ToArray();
            var r = IchimokuCloudSnapshot.Calculate(bars, 1, 2, 3, 0, 0);
            Assert.Equal(new IchimokuCloudValue(value, value, value, value, value), r[^1]);
        }
        var d = ComparisonVerifier.Fixture("negative", 5);
        var native = d.Quotes.GetIchimoku(1, 2, 3, 0, 0).ToArray();
        var owned = IchimokuCloudSnapshot.Calculate(d.IndicatorBars, 1, 2, 3, 0, 0);
        Assert.NotEqual((double?)native[0].TenkanSen, owned[0].Conversion);
        var sentinel = new[]
        {
            new Quote
            {
                Date = DateTime.UnixEpoch,
                High = decimal.MaxValue,
                Low = decimal.MaxValue,
                Close = 0,
            },
        };
        Assert.Null(sentinel.GetIchimoku(1, 1, 2, 0, 0).First().TenkanSen);
    }

    [Fact]
    public void InvalidInputsLargePeriodsAndNativeOffsetOverflowAreExplicit()
    {
        var d = CompetitorData.Create(8);
        Assert.Throws<ArgumentNullException>(() => IchimokuCloudSnapshot.Calculate(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            IchimokuCloudSnapshot.Calculate(d.IndicatorBars, 0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            IchimokuCloudSnapshot.Calculate(d.IndicatorBars, basePeriod: 0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            IchimokuCloudSnapshot.Calculate(d.IndicatorBars, spanBPeriod: 0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            IchimokuCloudSnapshot.Calculate(d.IndicatorBars, forwardOffset: -1)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            IchimokuCloudSnapshot.Calculate(d.IndicatorBars, backwardOffset: -1)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            IchimokuCloudSnapshot.Calculate([new Bar(DateTime.UnixEpoch, 0, double.NaN, 0, 0, 0)])
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => d.Quotes.GetIchimoku(0, 2, 3).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => d.Quotes.GetIchimoku(1, 2, 2).ToArray());
        Assert.Throws<OverflowException>(() =>
            d.Quotes.GetIchimoku(1, 2, 3, int.MaxValue, 0).ToArray()
        );
        Assert.Throws<OverflowException>(() =>
            d.Quotes.GetIchimoku(1, 2, 3, 0, int.MaxValue).ToArray()
        );
        var r = IchimokuCloudSnapshot.Calculate(
            d.IndicatorBars,
            int.MaxValue,
            int.MaxValue,
            int.MaxValue,
            int.MaxValue,
            int.MaxValue
        );
        Assert.All(r, v => Assert.Equal(new IchimokuCloudValue(null, null, null, null, null), v));
    }

    [Fact]
    public void AllValueMasksAndShiftMutationsAreDetected()
    {
        var d = CompetitorData.Create(100);
        var pair = IchimokuCloudComparison.Pair(2, 3, 5, 3, 2);
        foreach (var name in IchimokuCloudComparison.Names)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int p)
            {
                var r = native ? pair.Competitor(data, p) : pair.Ooples(data, p);
                if (presence)
                    r.Outputs[name].Present![50] = false;
                else
                    r.Outputs[name].Values[50] += 1;
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
        foreach (
            var wrong in new[]
            {
                IchimokuCloudComparison.Pair(2, 3, 5, 4, 2),
                IchimokuCloudComparison.Pair(2, 3, 5, 3, 3),
                IchimokuCloudComparison.Pair(2, 4, 5, 3, 2),
            }
        )
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(pair with { Library = wrong.Ooples }, d, 20)
            );
    }
}
